Imports System.IO

' OP2MapViewerJson
' https://github.com/leviathan400/OP2MapViewerJson
'
' Outpost 2 map viewer (JSON). Application to view Outpost 2 maps that are in JSON format.
' This only works with default tileMappings.
'

' Outpost 2: Divided Destiny is a real-time strategy video game released in 1997.

Public Class fMain

    Public ApplicationName As String = "OP2MapViewerJson"
    Public Version As String = "0.3.0"
    Public Build As String = "0020"

    Private currentMapFile As String
    Private currentMapPath As String

    'Normal startup or was a .json parsed to the application?
    Public LoadedWithCommandLineArgs As Boolean = False

    Private Sub fMain_Load(sender As Object, e As EventArgs) Handles MyBase.Load
        Debug.WriteLine("--- " & ApplicationName & " Started ---")
        Me.Icon = My.Resources.recommendations
        txtConsole.ReadOnly = True
        txtConsole.BackColor = Color.White

        Dim args = Environment.GetCommandLineArgs()
        If args.Length > 1 Then
            Dim filePath = args(1)
            'Debug.Write("GetCommandLineArgs: " & filePath)

            ' Check if the file exists
            If File.Exists(filePath) Then
                LoadedWithCommandLineArgs = True

                ' Try to load the json map file
                If LoadMapFile_JSON(filePath) Then
                    Debug.WriteLine(" - Map loaded successfully from command line")
                    Me.WindowState = FormWindowState.Minimized

                Else
                    Debug.WriteLine(" - Failed to load map")
                End If
            Else
                Debug.Write(" - File not found")
            End If
        End If

        btnSettings.Enabled = False ' Not currently used
    End Sub

    Public Sub AppendToConsole(TextToLog As String)
        ' Append text to the console with a newline
        txtConsole.AppendText(TextToLog & vbCrLf)

        ' Scroll to the end of the text
        txtConsole.SelectionStart = txtConsole.Text.Length
        txtConsole.ScrollToCaret()
    End Sub

    Private Sub btnOpen_Click(sender As Object, e As EventArgs) Handles btnOpen.Click

        OpenOutpost2Map_JSON()

    End Sub

    Private Sub OpenOutpost2Map_JSON()
        'Open a Outpost 2 .json file

        ' Create and configure OpenFileDialog
        Dim openFileDialog As New OpenFileDialog()
        openFileDialog.Filter = "Outpost 2 Map JSON Files (*.json)|*.json|All Files (*.*)|*.*"
        openFileDialog.Title = "Open Outpost 2 Map JSON File"

        ' Show dialog and check result
        If openFileDialog.ShowDialog() = DialogResult.OK Then

            LoadMapFile_JSON(openFileDialog.FileName)

        End If
    End Sub

    Private Function LoadMapFile_JSON(mapFilePath As String) As Boolean

        Try
            ' Save the selected file info
            currentMapFile = Path.GetFileName(mapFilePath)
            currentMapPath = mapFilePath

            ' Load the map
            Dim jsonMap As String = File.ReadAllText(currentMapPath)

            ' Open the map view form and pass the JSON data
            Dim mapViewForm As New fMapView()

            mapViewForm.Show()
            mapViewForm.LoadMapFromJson(currentMapFile, jsonMap)

            'Debug.WriteLine("Map Loaded: " & currentMapFile)
            Debug.WriteLine("Map Loaded: " & currentMapPath)
            AppendToConsole("Map loaded successfully - " & currentMapFile)

            Return True

        Catch ex As Exception
            Debug.WriteLine("Error loading map: " & ex.Message)
            AppendToConsole("Error loading map: " & ex.Message)

            Return False

        End Try

    End Function

    Private Sub btnSettings_Click(sender As Object, e As EventArgs) Handles btnSettings.Click

    End Sub

End Class
