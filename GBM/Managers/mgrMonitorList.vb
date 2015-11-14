﻿Imports System.IO

Public Class mgrMonitorList

    Public Enum eListTypes As Integer
        FullList = 1
        ScanList = 2
        ListByKey = 3
    End Enum

    Public Shared Event UpdateLog(sLogUpdate As String, bTrayUpdate As Boolean, objIcon As System.Windows.Forms.ToolTipIcon, bTimeStamp As Boolean)

    Public Shared Sub HandleBackupLocationChange()
        Dim oDatabase As New mgrSQLite(mgrSQLite.Database.Remote)
        Dim iGameCount As Integer

        'Check if a remote database already exists in the new backup location
        If oDatabase.CheckDB() Then
            'Make sure database is the latest version
            oDatabase.DatabaseUpgrade()

            'See if the remote database is empty
            iGameCount = mgrMonitorList.ReadList(eListTypes.FullList, mgrSQLite.Database.Remote).Count

            'If the remote database actually contains a list, then ask what to do
            If iGameCount > 0 Then
                If MsgBox("GBM data already exists in the backup folder." & vbCrLf & vbCrLf & _
                          "Do you want to make your local game list the new master game list in this folder? (Recommended)" & vbCrLf & vbCrLf & _
                          "Choosing No will sync your local game list to the current master game list in this folder.", MsgBoxStyle.YesNo, "Game Backup Monitor") = MsgBoxResult.Yes Then
                    mgrMonitorList.SyncMonitorLists()
                Else
                    mgrMonitorList.SyncMonitorLists(False)
                End If
            Else
                mgrMonitorList.SyncMonitorLists()
            End If
        Else
            mgrMonitorList.SyncMonitorLists()
        End If
    End Sub

    Public Shared Sub ExportMonitorList(ByVal sLocation As String)
        'Dim hshList As Hashtable = ReadList(eListTypes.FullList, mgrSQLite.Database.Local)
        'Dim bSuccess As Boolean
        'bSuccess = mgrXML.ExportMonitorList(hshList, sLocation)

        Dim oList As List(Of Game) = ReadListForExport()
        Dim bSuccess As Boolean
        bSuccess = mgrXML.SerializeAndExport(oList, sLocation)

        If bSuccess Then
            MsgBox("Export Complete.  " & oList.Count & " entries have been exported.", MsgBoxStyle.Information, "Game Backup Monitor")
        End If
    End Sub

    Public Shared Sub DoListAddUpdateSync(ByVal hshGames As Hashtable, Optional ByVal iSelectDB As mgrSQLite.Database = mgrSQLite.Database.Local)
        Dim oDatabase As New mgrSQLite(iSelectDB)
        Dim sSQL As String
        Dim hshParams As Hashtable
        Dim oParamList As New List(Of Hashtable)

        sSQL = "INSERT OR REPLACE INTO monitorlist (MonitorID, Name, Process, Path, AbsolutePath, FolderSave, FileType, TimeStamp, ExcludeList, Hours, Enabled, MonitorOnly) "
        sSQL &= "VALUES (COALESCE((SELECT MonitorID FROM monitorlist WHERE Name = @Name AND Process = @Process), @ID), @Name, @Process, @Path, @AbsolutePath, @FolderSave, @FileType, "
        sSQL &= "@TimeStamp, @ExcludeList, @Hours, @Enabled, @MonitorOnly);"

        For Each oGame As clsGame In hshGames.Values
            hshParams = New Hashtable

            'Parameters
            hshParams.Add("ID", oGame.ID)
            hshParams.Add("Name", oGame.Name)
            hshParams.Add("Process", oGame.TrueProcess)
            hshParams.Add("Path", oGame.TruePath)
            hshParams.Add("AbsolutePath", oGame.AbsolutePath)
            hshParams.Add("FolderSave", oGame.FolderSave)
            hshParams.Add("FileType", oGame.FileType)
            hshParams.Add("TimeStamp", oGame.AppendTimeStamp)
            hshParams.Add("ExcludeList", oGame.ExcludeList)
            hshParams.Add("Hours", oGame.Hours)

            'Required Defaults
            hshParams.Add("Enabled", True)
            hshParams.Add("MonitorOnly", False)

            oParamList.Add(hshParams)
        Next

        oDatabase.RunMassParamQuery(sSQL, oParamList)

    End Sub

    Public Shared Sub DoListDeleteSync(ByVal hshGames As Hashtable, Optional ByVal iSelectDB As mgrSQLite.Database = mgrSQLite.Database.Local)
        Dim oDatabase As New mgrSQLite(iSelectDB)
        Dim sSQL As String
        Dim hshParams As Hashtable
        Dim oParamList As New List(Of Hashtable)

        sSQL = "DELETE FROM monitorlist "
        sSQL &= "WHERE Name = @Name AND Process= @Process;"

        For Each oGame As clsGame In hshGames.Values
            hshParams = New Hashtable
            hshParams.Add("Name", oGame.Name)
            hshParams.Add("Process", oGame.TrueProcess)
            oParamList.Add(hshParams)
        Next

        oDatabase.RunMassParamQuery(sSQL, oParamList)
    End Sub

    Public Shared Sub SyncMonitorLists(Optional ByVal bToRemote As Boolean = True)
        Dim hshCompareFrom As Hashtable
        Dim hshCompareTo As Hashtable
        Dim hshSyncItems As Hashtable
        Dim hshDeleteItems As Hashtable
        Dim oFromItem As clsGame
        Dim oToItem As clsGame
        Dim iChanges As Integer

        Cursor.Current = Cursors.WaitCursor

        If bToRemote Then
            RaiseEvent UpdateLog("A sync to the master game list has been triggered.", False, ToolTipIcon.Info, True)
        Else
            RaiseEvent UpdateLog("A sync from the master game list has been triggered.", False, ToolTipIcon.Info, True)
        End If

        'Delete Sync
        If bToRemote Then
            hshCompareFrom = ReadList(eListTypes.FullList, mgrSQLite.Database.Local)
            hshCompareTo = ReadList(eListTypes.FullList, mgrSQLite.Database.Remote)
        Else
            hshCompareFrom = ReadList(eListTypes.FullList, mgrSQLite.Database.Remote)
            hshCompareTo = ReadList(eListTypes.FullList, mgrSQLite.Database.Local)
        End If

        hshDeleteItems = hshCompareTo.Clone

        For Each oToItem In hshCompareTo.Values
            If hshCompareFrom.Contains(oToItem.ProcessName) Then
                oFromItem = DirectCast(hshCompareFrom(oToItem.ProcessName), clsGame)
                If oToItem.CoreEquals(oFromItem) Then
                    hshDeleteItems.Remove(oToItem.ProcessName)
                End If
            End If
        Next

        If bToRemote Then
            DoListDeleteSync(hshDeleteItems, mgrSQLite.Database.Remote)
        Else
            DoListDeleteSync(hshDeleteItems, mgrSQLite.Database.Local)
        End If


        'Add / Update Sync
        If bToRemote Then
            hshCompareFrom = ReadList(eListTypes.FullList, mgrSQLite.Database.Local)
            hshCompareTo = ReadList(eListTypes.FullList, mgrSQLite.Database.Remote)
        Else
            hshCompareFrom = ReadList(eListTypes.FullList, mgrSQLite.Database.Remote)
            hshCompareTo = ReadList(eListTypes.FullList, mgrSQLite.Database.Local)
        End If

        hshSyncItems = hshCompareFrom.Clone

        For Each oFromItem In hshCompareFrom.Values
            If hshCompareTo.Contains(oFromItem.ProcessName) Then
                oToItem = DirectCast(hshCompareTo(oFromItem.ProcessName), clsGame)
                If oFromItem.SyncEquals(oToItem) Then
                    hshSyncItems.Remove(oFromItem.ProcessName)
                End If
            End If
        Next

        If bToRemote Then
            DoListAddUpdateSync(hshSyncItems, mgrSQLite.Database.Remote)
            
        Else
            DoListAddUpdateSync(hshSyncItems, mgrSQLite.Database.Local)
        End If

        'Sync Tags
        iChanges = mgrTags.SyncTags(bToRemote)
        iChanges += mgrGameTags.SyncGameTags(bToRemote)

        RaiseEvent UpdateLog(hshDeleteItems.Count + hshSyncItems.Count + iChanges & " change(s) synced.", False, ToolTipIcon.Info, True)
        Cursor.Current = Cursors.Default
        Application.DoEvents()
    End Sub

    Private Shared Sub ImportMonitorList(ByVal sLocation As String, Optional ByVal bWebRead As Boolean = False)
        Dim hshCompareFrom As Hashtable
        Dim hshCompareTo As Hashtable
        Dim hshSyncItems As Hashtable
        Dim oFromItem As clsGame
        Dim oToItem As clsGame

        Cursor.Current = Cursors.WaitCursor

        'Add / Update Sync
        hshCompareFrom = mgrXML.ReadMonitorList(sLocation, bWebRead)
        hshCompareTo = ReadList(eListTypes.FullList, mgrSQLite.Database.Local)

        hshSyncItems = hshCompareFrom.Clone

        For Each oFromItem In hshCompareFrom.Values
            If hshCompareTo.Contains(oFromItem.ProcessName) Then
                oToItem = DirectCast(hshCompareTo(oFromItem.ProcessName), clsGame)
                If oFromItem.CoreEquals(oToItem) Then
                    hshSyncItems.Remove(oFromItem.ProcessName)
                End If
            End If
        Next

        Cursor.Current = Cursors.Default

        If hshSyncItems.Count > 0 Then
            Dim frm As New frmAdvancedImport
            frm.ImportData = hshSyncItems
            If frm.ShowDialog() = DialogResult.OK Then
                Cursor.Current = Cursors.WaitCursor

                DoListAddUpdateSync(frm.ImportData)
                mgrTags.DoTagAddImport(frm.ImportData)

                Cursor.Current = Cursors.Default
                MsgBox("Import Complete.", MsgBoxStyle.Information, "Game Backup Monitor")
            End If
        Else
            MsgBox("This list does not contain any new games to import.", MsgBoxStyle.Information, "Game Backup Monitor")
        End If

        Application.DoEvents()
    End Sub

    Public Shared Function DoImport(ByVal sPath As String) As Boolean
        If (sPath.IndexOf("http://", 0, StringComparison.CurrentCultureIgnoreCase) > -1) Or _
           (sPath.IndexOf("https://", 0, StringComparison.CurrentCultureIgnoreCase) > -1) Then
            If mgrCommon.CheckAddress(sPath) Then
                ImportMonitorList(sPath, True)
                Return True
            Else
                MsgBox("There's no response from:" & vbCrLf & vbCrLf & sPath & vbCrLf & vbCrLf & "Either the server is not responding or the URL is invalid.")
                Return False
            End If
        Else
            If File.Exists(sPath) Then
                ImportMonitorList(sPath)
                Return True
            Else
                MsgBox("The file:" & vbCrLf & sPath & vbCrLf & "cannot be found.")
                Return False
            End If
        End If
        Return True
    End Function

    Public Shared Function ReadList(ByVal eListType As eListTypes, Optional ByVal iSelectDB As mgrSQLite.Database = mgrSQLite.Database.Local) As Hashtable
        Dim oDatabase As New mgrSQLite(iSelectDB)
        Dim oData As DataSet
        Dim sSQL As String
        Dim hshList As New Hashtable
        Dim hshDupeList As New Hashtable
        Dim oGame As clsGame
        Dim oDupeGame As clsGame

        sSQL = "SELECT * from monitorlist ORDER BY Name Asc"
        oData = oDatabase.ReadParamData(sSQL, New Hashtable)

        For Each dr As DataRow In oData.Tables(0).Rows
            oGame = New clsGame
            oGame.ID = CStr(dr(0))
            oGame.Name = CStr(dr(1))
            oGame.ProcessName = CStr(dr(2))
            If Not IsDBNull(dr(3)) Then oGame.Path = CStr(dr(3))
            oGame.AbsolutePath = CBool(dr(4))
            oGame.FolderSave = CBool(dr(5))
            If Not IsDBNull(dr(6)) Then oGame.FileType = CStr(dr(6))
            oGame.AppendTimeStamp = CBool(dr(7))
            If Not IsDBNull(dr(8)) Then oGame.ExcludeList = CStr(dr(8))
            If Not IsDBNull(dr(9)) Then oGame.ProcessPath = CStr(dr(9))
            If Not IsDBNull(dr(10)) Then oGame.Icon = CStr(dr(10))
            oGame.Hours = CDbl(dr(11))
            If Not IsDBNull(dr(12)) Then oGame.Version = CStr(dr(12))
            If Not IsDBNull(dr(13)) Then oGame.Company = CStr(dr(13))
            oGame.Enabled = CBool(dr(14))
            oGame.MonitorOnly = CBool(dr(15))

            Select Case eListType
                Case eListTypes.FullList
                    If hshList.Contains(oGame.ProcessName) Or hshDupeList.Contains(oGame.ProcessName) Then
                        oDupeGame = DirectCast(hshList.Item(oGame.ProcessName), clsGame)
                        If Not hshDupeList.Contains(oGame.ProcessName) Then
                            hshDupeList.Add(oGame.ProcessName, oDupeGame)
                            hshList.Remove(oDupeGame.ProcessName)
                            oDupeGame.Duplicate = True
                            oDupeGame.ProcessName = oDupeGame.ProcessName & ":" & oDupeGame.Name
                            hshList.Add(oDupeGame.ProcessName, oDupeGame)
                        End If
                        oGame.ProcessName = oGame.ProcessName & ":" & oGame.Name
                        oGame.Duplicate = True
                    End If

                    hshList.Add(oGame.ProcessName, oGame)
                Case eListTypes.ScanList
                    If hshList.Contains(oGame.ProcessName) Then
                        DirectCast(hshList.Item(oGame.ProcessName), clsGame).Duplicate = True
                        oGame.ProcessName = oGame.ProcessName & ":" & oGame.Name
                        oGame.Duplicate = True
                    End If

                    If oGame.Enabled Then hshList.Add(oGame.ProcessName, oGame)
                Case eListTypes.ListByKey
                    hshList.Add(oGame.ID, oGame)
            End Select
        Next

        Return hshList
    End Function

    Public Shared Function ReadListForExport(Optional ByVal iSelectDB As mgrSQLite.Database = mgrSQLite.Database.Local) As List(Of Game)
        Dim oDatabase As New mgrSQLite(iSelectDB)
        Dim oData As DataSet
        Dim sSQL As String
        Dim sID As String
        Dim oList As New List(Of Game)
        Dim oGame As Game

        sSQL = "SELECT * from monitorlist ORDER BY Name Asc"
        oData = oDatabase.ReadParamData(sSQL, New Hashtable)

        For Each dr As DataRow In oData.Tables(0).Rows
            oGame = New Game
            sID = CStr(dr(0))
            oGame.Name = CStr(dr(1))
            oGame.ProcessName = CStr(dr(2))
            If Not IsDBNull(dr(3)) Then oGame.Path = CStr(dr(3))
            oGame.AbsolutePath = CBool(dr(4))
            oGame.FolderSave = CBool(dr(5))
            If Not IsDBNull(dr(6)) Then oGame.FileType = CStr(dr(6))
            If Not IsDBNull(dr(8)) Then oGame.ExcludeList = CStr(dr(8))
            oGame.Tags = mgrGameTags.GetTagsByGameForExport(sID)
            oList.Add(oGame)
        Next

        Return oList
    End Function

    Public Shared Sub DoListAdd(ByVal oGame As clsGame, Optional ByVal iSelectDB As mgrSQLite.Database = mgrSQLite.Database.Local)
        Dim oDatabase As New mgrSQLite(iSelectDB)
        Dim sSQL As String
        Dim hshParams As New Hashtable

        sSQL = "INSERT INTO monitorlist VALUES (@ID, @Name, @Process, @Path, @AbsolutePath, @FolderSave, @FileType, @TimeStamp, "
        sSQL &= "@ExcludeList, @ProcessPath, @Icon, @Hours, @Version, @Company, @Enabled, @MonitorOnly)"

        'Parameters
        hshParams.Add("ID", oGame.ID)
        hshParams.Add("Name", oGame.Name)
        hshParams.Add("Process", oGame.TrueProcess)
        hshParams.Add("Path", oGame.TruePath)
        hshParams.Add("AbsolutePath", oGame.AbsolutePath)
        hshParams.Add("FolderSave", oGame.FolderSave)
        hshParams.Add("FileType", oGame.FileType)
        hshParams.Add("TimeStamp", oGame.AppendTimeStamp)
        hshParams.Add("ExcludeList", oGame.ExcludeList)
        hshParams.Add("ProcessPath", oGame.ProcessPath)
        hshParams.Add("Icon", oGame.Icon)
        hshParams.Add("Hours", oGame.Hours)
        hshParams.Add("Version", oGame.Version)
        hshParams.Add("Company", oGame.Company)
        hshParams.Add("Enabled", oGame.Enabled)
        hshParams.Add("MonitorOnly", oGame.MonitorOnly)

        oDatabase.RunParamQuery(sSQL, hshParams)

    End Sub

    Public Shared Sub DoListUpdate(ByVal oGame As clsGame, Optional ByVal iSelectDB As mgrSQLite.Database = mgrSQLite.Database.Local)
        Dim oDatabase As New mgrSQLite(iSelectDB)
        Dim sSQL As String
        Dim hshParams As New Hashtable

        sSQL = "UPDATE monitorlist SET Name=@Name, Process=@Process, Path=@Path, AbsolutePath=@AbsolutePath, FolderSave=@FolderSave, "
        sSQL &= "FileType=@FileType, TimeStamp=@TimeStamp, ExcludeList=@ExcludeList, ProcessPath=@ProcessPath, Icon=@Icon, "
        sSQL &= "Hours=@Hours, Version=@Version, Company=@Company, Enabled=@Enabled, MonitorOnly=@MonitorOnly WHERE MonitorID=@ID"

        'Parameters
        hshParams.Add("Name", oGame.Name)
        hshParams.Add("Process", oGame.TrueProcess)
        hshParams.Add("Path", oGame.TruePath)
        hshParams.Add("AbsolutePath", oGame.AbsolutePath)
        hshParams.Add("FolderSave", oGame.FolderSave)
        hshParams.Add("FileType", oGame.FileType)
        hshParams.Add("TimeStamp", oGame.AppendTimeStamp)
        hshParams.Add("ExcludeList", oGame.ExcludeList)
        hshParams.Add("ProcessPath", oGame.ProcessPath)
        hshParams.Add("Icon", oGame.Icon)
        hshParams.Add("Hours", oGame.Hours)
        hshParams.Add("Version", oGame.Version)
        hshParams.Add("Company", oGame.Company)
        hshParams.Add("Enabled", oGame.Enabled)
        hshParams.Add("MonitorOnly", oGame.MonitorOnly)
        hshParams.Add("ID", oGame.ID)

        oDatabase.RunParamQuery(sSQL, hshParams)

    End Sub

    Public Shared Sub DoListUpdateMulti(ByVal sMonitorIDs As List(Of String), ByVal oGame As clsGame, Optional ByVal iSelectDB As mgrSQLite.Database = mgrSQLite.Database.Local)
        Dim oDatabase As New mgrSQLite(iSelectDB)
        Dim sSQL As String
        Dim hshParams As New Hashtable
        Dim iCounter As Integer

        sSQL = "UPDATE monitorlist SET Enabled=@Enabled, MonitorOnly=@MonitorOnly WHERE MonitorID IN ("

        'Parameters
        hshParams.Add("Enabled", oGame.Enabled)
        hshParams.Add("MonitorOnly", oGame.MonitorOnly)

        For Each s As String In sMonitorIDs
            sSQL &= "@MonitorID" & iCounter & ","
            hshParams.Add("MonitorID" & iCounter, s)
            iCounter += 1
        Next

        sSQL = sSQL.TrimEnd(",")
        sSQL &= ")"

        oDatabase.RunParamQuery(sSQL, hshParams)

    End Sub

    Public Shared Sub DoListDelete(ByVal sMonitorID As String, Optional ByVal iSelectDB As mgrSQLite.Database = mgrSQLite.Database.Local)
        Dim oDatabase As New mgrSQLite(iSelectDB)
        Dim sSQL As String
        Dim hshParams As New Hashtable

        sSQL = "DELETE FROM gametags "
        sSQL &= "WHERE MonitorID = @MonitorID;"
        sSQL &= "DELETE FROM monitorlist "
        sSQL &= "WHERE MonitorID = @MonitorID;"

        hshParams.Add("MonitorID", sMonitorID)

        oDatabase.RunParamQuery(sSQL, hshParams)

    End Sub

    Public Shared Sub DoListDeleteMulti(ByVal sMonitorIDs As List(Of String), Optional ByVal iSelectDB As mgrSQLite.Database = mgrSQLite.Database.Local)
        Dim oDatabase As New mgrSQLite(iSelectDB)
        Dim sSQL As String
        Dim hshParams As New Hashtable
        Dim iCounter As Integer

        sSQL = "DELETE FROM gametags "
        sSQL &= "WHERE MonitorID IN ("

        For Each s As String In sMonitorIDs
            sSQL &= "@MonitorID" & iCounter & ","
            hshParams.Add("MonitorID" & iCounter, s)
            iCounter += 1
        Next

        sSQL = sSQL.TrimEnd(",")
        sSQL &= ");"

        sSQL &= "DELETE FROM monitorlist "
        sSQL &= "WHERE MonitorID IN ("

        For Each s As String In sMonitorIDs
            sSQL &= "@MonitorID" & iCounter & ","
            hshParams.Add("MonitorID" & iCounter, s)
            iCounter += 1
        Next

        sSQL = sSQL.TrimEnd(",")
        sSQL &= ");"

        oDatabase.RunParamQuery(sSQL, hshParams)
    End Sub

    Public Shared Function DoListGetbyID(ByVal iMonitorID As Integer, Optional ByVal iSelectDB As mgrSQLite.Database = mgrSQLite.Database.Local) As clsGame
        Dim oDatabase As New mgrSQLite(iSelectDB)
        Dim sSQL As String
        Dim oData As DataSet
        Dim oGame As New clsGame
        Dim hshParams As New Hashtable

        sSQL = "SELECT * from monitorlist "
        sSQL &= "WHERE MonitorID = @MonitorID"

        hshParams.Add("MonitorID", iMonitorID)

        oData = oDatabase.ReadParamData(sSQL, hshParams)

        For Each dr As DataRow In oData.Tables(0).Rows
            oGame = New clsGame
            oGame.ID = CStr(dr(0))
            oGame.Name = CStr(dr(1))
            oGame.ProcessName = CStr(dr(2))
            If Not IsDBNull(dr(3)) Then oGame.Path = CStr(dr(3))
            oGame.AbsolutePath = CBool(dr(4))
            oGame.FolderSave = CBool(dr(5))
            If Not IsDBNull(dr(6)) Then oGame.FileType = CStr(dr(6))
            oGame.AppendTimeStamp = CBool(dr(7))
            If Not IsDBNull(dr(8)) Then oGame.ExcludeList = CStr(dr(8))
            If Not IsDBNull(dr(9)) Then oGame.ProcessPath = CStr(dr(9))
            If Not IsDBNull(dr(10)) Then oGame.Icon = CStr(dr(10))
            oGame.Hours = CDbl(dr(11))
            If Not IsDBNull(dr(12)) Then oGame.Version = CStr(dr(12))
            If Not IsDBNull(dr(13)) Then oGame.Company = CStr(dr(13))
            oGame.Enabled = CBool(dr(14))
            oGame.MonitorOnly = CBool(dr(15))
        Next

        Return oGame
    End Function

    Public Shared Function DoListGetbyName(ByVal sName As String, Optional ByVal iSelectDB As mgrSQLite.Database = mgrSQLite.Database.Local) As Hashtable
        Dim oDatabase As New mgrSQLite(iSelectDB)
        Dim sSQL As String
        Dim oData As DataSet
        Dim oGame As New clsGame
        Dim hshGames As New Hashtable
        Dim hshParams As New Hashtable
        Dim iCounter As Integer = 0

        sSQL = "SELECT * from monitorlist "
        sSQL &= "WHERE Name = @Name"

        hshParams.Add("Name", sName)

        oData = oDatabase.ReadParamData(sSQL, hshParams)

        For Each dr As DataRow In oData.Tables(0).Rows
            oGame = New clsGame
            oGame.ID = CStr(dr(0))
            oGame.Name = CStr(dr(1))
            oGame.ProcessName = CStr(dr(2))
            If Not IsDBNull(dr(3)) Then oGame.Path = CStr(dr(3))
            oGame.AbsolutePath = CBool(dr(4))
            oGame.FolderSave = CBool(dr(5))
            If Not IsDBNull(dr(6)) Then oGame.FileType = CStr(dr(6))
            oGame.AppendTimeStamp = CBool(dr(7))
            If Not IsDBNull(dr(8)) Then oGame.ExcludeList = CStr(dr(8))
            If Not IsDBNull(dr(9)) Then oGame.ProcessPath = CStr(dr(9))
            If Not IsDBNull(dr(10)) Then oGame.Icon = CStr(dr(10))
            oGame.Hours = CDbl(dr(11))
            If Not IsDBNull(dr(12)) Then oGame.Version = CStr(dr(12))
            If Not IsDBNull(dr(13)) Then oGame.Company = CStr(dr(13))
            oGame.Enabled = CBool(dr(14))
            oGame.MonitorOnly = CBool(dr(15))
            hshGames.Add(iCounter, oGame)
            iCounter += 1
        Next

        Return hshGames
    End Function

    Public Shared Function DoDuplicateListCheck(ByVal sName As String, ByVal sProcess As String, Optional ByVal iSelectDB As mgrSQLite.Database = mgrSQLite.Database.Local, Optional ByVal sExcludeID As String = "") As Boolean
        Dim oDatabase As New mgrSQLite(iSelectDB)
        Dim sSQL As String
        Dim oData As DataSet
        Dim hshParams As New Hashtable

        sSQL = "SELECT * FROM monitorlist WHERE Name = @Name AND Process= @Process"

        hshParams.Add("Name", sName)
        hshParams.Add("Process", sProcess)

        If sExcludeID <> String.Empty Then
            sSQL &= " AND MonitorID <> @MonitorID"
            hshParams.Add("MonitorID", sExcludeID)
        End If

        oData = oDatabase.ReadParamData(sSQL, hshParams)

        If oData.Tables(0).Rows.Count > 0 Then
            Return True
        Else
            Return False
        End If
    End Function

End Class
