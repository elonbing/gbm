﻿Imports System.IO

Public Class frmVariableManager
    Dim hshVariableData As Hashtable
    Private bIsDirty As Boolean = False
    Private bIsLoading As Boolean = False
    Private oCurrentVariable As clsPathVariable

    Private Property IsDirty As Boolean
        Get
            Return bIsDirty
        End Get
        Set(value As Boolean)
            bIsDirty = value
        End Set
    End Property

    Private Property IsLoading As Boolean
        Get
            Return bIsLoading
        End Get
        Set(value As Boolean)
            bIsLoading = value
        End Set
    End Property

    Private Enum eModes As Integer
        View = 1
        Edit = 2
        Add = 3
        Disabled = 4
    End Enum

    Private eCurrentMode As eModes = eModes.Disabled

    Private Property VariableData As Hashtable
        Get
            Return hshVariableData
        End Get
        Set(value As Hashtable)
            hshVariableData = value
        End Set
    End Property

    Private Sub PathBrowse()
        Dim sDefaultFolder As String = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments)
        Dim sCurrentPath As String = txtPath.Text
        Dim sNewPath As String

        If txtPath.Text <> String.Empty Then
            If Directory.Exists(sCurrentPath) Then
                sDefaultFolder = sCurrentPath
            End If
        End If

        sNewPath = mgrCommon.OpenFolderBrowser("Choose the path the variable represents:", sDefaultFolder, False)

        If sNewPath <> String.Empty Then txtPath.Text = sNewPath
    End Sub

    Private Sub LoadData()
        VariableData = mgrVariables.ReadVariables
        lstVariables.Items.Clear()
        FormatAndFillList()
    End Sub

    Private Function HandleDirty() As MsgBoxResult
        Dim oResult As MsgBoxResult

        oResult = MsgBox("There are unsaved changes on this form.  Do you want to save?", MsgBoxStyle.YesNoCancel, "Game Backup Monitor")

        Select Case oResult
            Case MsgBoxResult.Yes
                IsDirty = False
            Case MsgBoxResult.No
                IsDirty = False
            Case MsgBoxResult.Cancel
                'No Change
        End Select

        Return oResult

    End Function

    Private Sub FormatAndFillList()
        IsLoading = True

        For Each oCustomVariable As clsPathVariable In VariableData.Values
            lstVariables.Items.Add(oCustomVariable.Name)
        Next

        IsLoading = False
    End Sub

    Private Sub FillData()
        IsLoading = True

        oCurrentVariable = DirectCast(VariableData(lstVariables.SelectedItems(0).ToString), clsPathVariable)

        txtID.Text = oCurrentVariable.ID
        txtName.Text = oCurrentVariable.Name
        txtPath.Text = oCurrentVariable.Path

        IsLoading = False
    End Sub

    Private Sub DirtyCheck_ValueChanged(sender As Object, e As EventArgs)
        If Not IsLoading Then
            IsDirty = True
            If Not eCurrentMode = eModes.Add Then EditVariable()
        End If
    End Sub

    Private Sub AssignDirtyHandlers(ByVal oCtls As GroupBox.ControlCollection)
        For Each ctl As Control In oCtls
            If TypeOf ctl Is TextBox Then
                AddHandler DirectCast(ctl, TextBox).TextChanged, AddressOf DirtyCheck_ValueChanged
            End If
        Next
    End Sub

    Private Sub WipeControls(ByVal oCtls As GroupBox.ControlCollection)
        For Each ctl As Control In oCtls
            If TypeOf ctl Is TextBox Then
                DirectCast(ctl, TextBox).Text = String.Empty
            End If
        Next
        txtID.Text = String.Empty
    End Sub

    Private Sub ModeChange()
        IsLoading = True

        Select Case eCurrentMode
            Case eModes.Add
                grpVariable.Enabled = True
                WipeControls(grpVariable.Controls)
                btnSave.Enabled = True
                btnCancel.Enabled = True
                btnAdd.Enabled = False
                btnDelete.Enabled = False
                lstVariables.Enabled = False
            Case eModes.Edit
                lstVariables.Enabled = False
                grpVariable.Enabled = True
                btnSave.Enabled = True
                btnCancel.Enabled = True
                btnAdd.Enabled = False
                btnDelete.Enabled = False
            Case eModes.View
                lstVariables.Enabled = True
                grpVariable.Enabled = True
                btnSave.Enabled = False
                btnCancel.Enabled = False
                btnAdd.Enabled = True
                btnDelete.Enabled = True
            Case eModes.Disabled
                lstVariables.Enabled = True
                WipeControls(grpVariable.Controls)
                grpVariable.Enabled = False
                btnSave.Enabled = False
                btnCancel.Enabled = False
                btnAdd.Enabled = True
                btnDelete.Enabled = True
        End Select

        IsLoading = False
    End Sub

    Private Sub EditVariable()
        eCurrentMode = eModes.Edit
        ModeChange()
    End Sub

    Private Sub AddVariable()
        eCurrentMode = eModes.Add
        ModeChange()
        txtName.Focus()
    End Sub

    Private Sub CancelEdit()
        If bIsDirty Then
            Select Case HandleDirty()
                Case MsgBoxResult.Yes
                    SaveVariable()
                Case MsgBoxResult.No
                    If lstVariables.SelectedItems.Count > 0 Then
                        eCurrentMode = eModes.View
                        ModeChange()
                        FillData()
                        lstVariables.Focus()
                    Else
                        eCurrentMode = eModes.Disabled
                        ModeChange()
                    End If
                Case MsgBoxResult.Cancel
                    'Do Nothing
            End Select
        Else
            If lstVariables.SelectedItems.Count > 0 Then
                eCurrentMode = eModes.View
                ModeChange()
                FillData()
                lstVariables.Focus()
            Else
                eCurrentMode = eModes.Disabled
                ModeChange()
            End If
        End If
    End Sub

    Private Sub SaveVariable()
        Dim oCustomVariable As New clsPathVariable
        Dim bSuccess As Boolean = False

        If txtID.Text <> String.Empty Then
            oCustomVariable.ID = txtID.Text
        End If
        oCustomVariable.Name = txtName.Text
        oCustomVariable.Path = txtPath.Text

        Select Case eCurrentMode
            Case eModes.Add
                If CoreValidatation(oCustomVariable) Then
                    bSuccess = True
                    mgrVariables.DoVariableAdd(oCustomVariable)
                    mgrVariables.DoPathUpdate(oCustomVariable.Path, oCustomVariable.FormattedName)
                    eCurrentMode = eModes.View
                End If
            Case eModes.Edit
                If CoreValidatation(oCustomVariable) Then
                    bSuccess = True
                    mgrVariables.DoVariableUpdate(oCustomVariable)
                    mgrVariables.DoPathUpdate(oCurrentVariable.FormattedName, oCurrentVariable.Path)
                    mgrVariables.DoPathUpdate(oCustomVariable.Path, oCustomVariable.FormattedName)
                    eCurrentMode = eModes.View
                End If
        End Select

        If bSuccess Then
            IsDirty = False
            LoadData()
            ModeChange()
            If eCurrentMode = eModes.View Then lstVariables.SelectedIndex = lstVariables.Items.IndexOf(oCustomVariable.Name)
        End If
    End Sub

    Private Sub DeleteVariable()
        Dim oCustomVariable As clsPathVariable

        If lstVariables.SelectedItems.Count > 0 Then
            oCustomVariable = DirectCast(VariableData(lstVariables.SelectedItems(0).ToString), clsPathVariable)

            If MsgBox("Are you sure you want to delete " & oCustomVariable.Name & "?  This cannot be undone.", MsgBoxStyle.YesNo, "Game Backup Monitor") = MsgBoxResult.Yes Then
                mgrVariables.DoVariableDelete(oCustomVariable.ID)
                mgrVariables.DoPathUpdate(oCurrentVariable.FormattedName, oCurrentVariable.Path)
                LoadData()
                eCurrentMode = eModes.Disabled
                ModeChange()
            End If
        End If
    End Sub

    Private Sub SwitchVariable()
        If lstVariables.SelectedItems.Count > 0 Then
            eCurrentMode = eModes.View
            FillData()
            ModeChange()
        End If
    End Sub

    Private Function CoreValidatation(ByVal oCustomVariable As clsPathVariable) As Boolean
        If txtName.Text = String.Empty Then
            MsgBox("You must enter a valid path name.", MsgBoxStyle.Exclamation, "Game Backup Monitor")
            txtName.Focus()
            Return False
        End If

        If txtPath.Text = String.Empty Then
            MsgBox("You must enter a valid path.", MsgBoxStyle.Exclamation, "Game Backup Monitor")
            txtPath.Focus()
            Return False
        End If

        If mgrVariables.DoCheckDuplicate(oCustomVariable.Name, oCustomVariable.ID) Then
            MsgBox("An custom variable with this name already exists.", MsgBoxStyle.Exclamation, "Game Backup Monitor")
            txtName.Focus()
            Return False
        End If

        Return True
    End Function

    Private Sub frmVariableManager_Load(sender As Object, e As EventArgs) Handles MyBase.Load
        LoadData()
        ModeChange()
        AssignDirtyHandlers(grpVariable.Controls)
    End Sub

    Private Sub lstVariables_SelectedIndexChanged(sender As Object, e As EventArgs) Handles lstVariables.SelectedIndexChanged
        SwitchVariable()
    End Sub

    Private Sub btnAdd_Click(sender As Object, e As EventArgs) Handles btnAdd.Click
        AddVariable()
    End Sub

    Private Sub btnDelete_Click(sender As Object, e As EventArgs) Handles btnDelete.Click
        DeleteVariable()
    End Sub

    Private Sub btnSave_Click(sender As Object, e As EventArgs) Handles btnSave.Click
        SaveVariable()
    End Sub

    Private Sub btnCancel_Click(sender As Object, e As EventArgs) Handles btnCancel.Click
        CancelEdit()
    End Sub

    Private Sub btnClose_Click(sender As Object, e As EventArgs) Handles btnClose.Click
        Me.Close()
    End Sub

    Private Sub btnPathBrowse_Click(sender As Object, e As EventArgs) Handles btnPathBrowse.Click
        PathBrowse()
    End Sub

    Private Sub frmVariableManager_FormClosing(sender As Object, e As FormClosingEventArgs) Handles MyBase.FormClosing
        If bIsDirty Then
            Select Case HandleDirty()
                Case MsgBoxResult.Yes
                    SaveVariable()
                Case MsgBoxResult.No
                    'Do Nothing
                Case MsgBoxResult.Cancel
                    e.Cancel = True
            End Select
        End If
    End Sub
End Class