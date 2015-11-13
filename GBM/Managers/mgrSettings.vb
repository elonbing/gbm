﻿Imports System.IO

Public Class mgrSettings
    Private bStartWithWindows As Boolean = False
    Private bMonitoronStartup As Boolean = True
    Private bStartToTray As Boolean = False
    Private bShowDetectionToolTips As Boolean = True
    Private bDisableConfirmation As Boolean = False
    Private bCreateSubFolder As Boolean = False
    Private bShowOverwriteWarning As Boolean = True
    Private bRestoreOnLaunch As Boolean = False
    Private bSync As Boolean = True
    Private bCheckSum As Boolean = True
    Private bTimeTracking As Boolean = True
    Private sBackupFolder As String = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments).TrimEnd(New Char() {"\", "/"})

    Property StartWithWindows As Boolean
        Get
            Return bStartWithWindows
        End Get
        Set(value As Boolean)
            bStartWithWindows = value
        End Set
    End Property

    Property MonitorOnStartup As Boolean
        Get
            Return bMonitoronStartup
        End Get
        Set(value As Boolean)
            bMonitoronStartup = value
        End Set
    End Property

    Property StartToTray As Boolean
        Get
            Return bStartToTray
        End Get
        Set(value As Boolean)
            bStartToTray = value
        End Set
    End Property

    Property ShowDetectionToolTips As Boolean
        Get
            Return bShowDetectionToolTips
        End Get
        Set(value As Boolean)
            bShowDetectionToolTips = value
        End Set
    End Property

    Property DisableConfirmation As Boolean
        Get
            Return bDisableConfirmation
        End Get
        Set(value As Boolean)
            bDisableConfirmation = value
        End Set
    End Property

    Property CreateSubFolder As Boolean
        Get
            Return bCreateSubFolder
        End Get
        Set(value As Boolean)
            bCreateSubFolder = value
        End Set
    End Property

    Property ShowOverwriteWarning As Boolean
        Get
            Return bShowOverwriteWarning
        End Get
        Set(value As Boolean)
            bShowOverwriteWarning = value
        End Set
    End Property

    Property RestoreOnLaunch As Boolean
        Get
            Return bRestoreOnLaunch
        End Get
        Set(value As Boolean)
            bRestoreOnLaunch = value
        End Set
    End Property

    Property Sync As Boolean
        Get
            Return bSync
        End Get
        Set(value As Boolean)
            bSync = value
        End Set
    End Property

    Property CheckSum As Boolean
        Get
            Return bCheckSum
        End Get
        Set(value As Boolean)
            bCheckSum = value
        End Set
    End Property

    Property TimeTracking As Boolean
        Get
            Return bTimeTracking
        End Get
        Set(value As Boolean)
            bTimeTracking = value
        End Set
    End Property

    Property BackupFolder As String
        Get
            Return sBackupFolder
        End Get
        Set(value As String)
            sBackupFolder = value.TrimEnd(New Char() {"\", "/"})
        End Set
    End Property

    Private Sub SaveFromClass()
        Dim oDatabase As New mgrSQLite(mgrSQLite.Database.Local)
        Dim sSQL As String
        Dim hshParams As New Hashtable

        sSQL = "DELETE FROM settings WHERE SettingsID = 1"
        oDatabase.RunParamQuery(sSQL, New Hashtable)

        sSQL = "INSERT INTO settings VALUES (1, @MonitorOnStartup, @StartToTray, @ShowDetectionToolTips, @DisableConfirmation, "
        sSQL &= "@CreateSubFolder, @ShowOverwriteWarning, @RestoreOnLaunch, @BackupFolder, @Sync, @CheckSum, @StartWithWindows, @TimeTracking)"

        hshParams.Add("MonitorOnStartup", MonitorOnStartup)
        hshParams.Add("StartToTray", StartToTray)
        hshParams.Add("ShowDetectionToolTips", ShowDetectionToolTips)
        hshParams.Add("DisableConfirmation", DisableConfirmation)
        hshParams.Add("CreateSubFolder", CreateSubFolder)
        hshParams.Add("ShowOverwriteWarning", ShowOverwriteWarning)
        hshParams.Add("RestoreOnLaunch", RestoreOnLaunch)
        hshParams.Add("BackupFolder", BackupFolder)
        hshParams.Add("Sync", Sync)
        hshParams.Add("CheckSum", CheckSum)
        hshParams.Add("StartWithWindows", StartWithWindows)
        hshParams.Add("TimeTracking", TimeTracking)

        oDatabase.RunParamQuery(sSQL, hshParams)
    End Sub

    Private Sub MapToClass()
        Dim oDatabase As New mgrSQLite(mgrSQLite.Database.Local)
        Dim oData As DataSet
        Dim sSQL As String

        oDatabase.Connect()

        sSQL = "SELECT * FROM settings WHERE SettingsID = 1"
        oData = oDatabase.ReadParamData(sSQL, New Hashtable)

        For Each dr As DataRow In oData.Tables(0).Rows
            MonitorOnStartup = CBool(dr(1))
            StartToTray = CBool(dr(2))
            ShowDetectionToolTips = CBool(dr(3))
            DisableConfirmation = CBool(dr(4))
            CreateSubFolder = CBool(dr(5))
            ShowOverwriteWarning = CBool(dr(6))
            RestoreOnLaunch = CBool(dr(7))
            BackupFolder = CStr(dr(8))
            Sync = CBool(dr(9))
            CheckSum = CBool(dr(10))
            StartWithWindows = CBool(dr(11))
            TimeTracking = CBool(dr(12))
        Next

        oDatabase.Disconnect()
    End Sub

    Public Sub LoadSettings()
        MapToClass()

        'Set Remote Manifest Location     
        mgrPath.RemoteDatabaseLocation = Me.BackupFolder
    End Sub

    Public Sub SaveSettings()
        SaveFromClass()

        'Set Remote Manifest Location        
        mgrPath.RemoteDatabaseLocation = Me.BackupFolder
    End Sub
End Class