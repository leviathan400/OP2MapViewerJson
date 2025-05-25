Imports System.Drawing
Imports System.IO
Imports Newtonsoft.Json
Imports Newtonsoft.Json.Linq

' OP2MapViewerJson
' fMapView

Public Class fMapView

    ' Form where we render the map in game view
    ' We are expecting 2012 separate bmp files (each 32x32) in this folder
    ' Viewer currently only works with well00 (default) tileset maps
    Private TilesetLocation As String = "D:\tilesets\well00\all"

    ' Map properties
    Private mapJsonFileName As String
    Private mapWidth As Integer
    Private mapHeight As Integer
    Private mapFileName As String
    Private mapName As String
    Private mapNotes As String

    Private mapTileSetPrefix As String
    Private mapTiles As Integer(,)

    ' Rendering properties
    Private tileSize As Integer = 32    ' Tiles are 32x32 pixels
    Private mapScale As Single = 1.0F
    Private viewOffsetX As Integer = 0
    Private viewOffsetY As Integer = 0
    Private tileCache As New Dictionary(Of Integer, Bitmap)

    Private isRightMouseDown As Boolean = False
    Private lastMouseX As Integer = 0
    Private lastMouseY As Integer = 0

    ' Status bar
    Private mapNameLabel As ToolStripStatusLabel
    Private zoomLevelLabel As New ToolStripStatusLabel

    Private Sub fMapView_Load(sender As Object, e As EventArgs) Handles MyBase.Load
        Me.Icon = My.Resources.recommendations
        Me.Text = "Map Viewer"

        ' Set up the panel for rendering
        pnlMap.AutoScroll = True

        pnlMap.Dock = System.Windows.Forms.DockStyle.Fill
        pnlMap.Location = New System.Drawing.Point(0, 0)
        pnlMap.Size = New System.Drawing.Size(762, 541) ' This size will be overridden by the Dock property

        InitializeUI()
    End Sub

    Private Sub InitializeUI()
        ' Enable double buffering for smooth rendering
        pnlMap.GetType().GetProperty("DoubleBuffered", Reflection.BindingFlags.Instance Or Reflection.BindingFlags.NonPublic).SetValue(pnlMap, True)

        ' Set up status bar
        mapNameLabel = New ToolStripStatusLabel()
        mapNameLabel.Name = "lblNameLabel"
        mapNameLabel.Text = "No map loaded"
        mapNameLabel.TextAlign = ContentAlignment.MiddleLeft
        mapNameLabel.Spring = True

        ' Create zoom level label
        zoomLevelLabel.Name = "lblZoomLevel"
        zoomLevelLabel.Text = "Zoom: 100%"
        zoomLevelLabel.TextAlign = ContentAlignment.MiddleRight
        zoomLevelLabel.Alignment = ToolStripItemAlignment.Right

        ' Add the labels to the status strip
        StatusStrip.Items.Add(mapNameLabel)
        StatusStrip.Items.Add(zoomLevelLabel)

        ' Add a handler to draw the map
        AddHandler pnlMap.Paint, AddressOf PnlMap_Paint

    End Sub


    Public Sub LoadMapFromJson(JsonMapFile As String, jsonData As String)
        ' Load a Outpost 2 .json map file

        Try
            mapJsonFileName = JsonMapFile

            ' Parse the JSON data
            Dim jsonObject As JObject = JObject.Parse(jsonData)

            ' Get map dimensions from the header
            Dim header As JObject = jsonObject("header")
            mapWidth = CInt(header("width"))
            mapHeight = CInt(header("height"))
            ' Ge map file name
            mapFileName = header("map").ToString()
            ' Get map name
            mapName = header("name").ToString()
            ' Get map notes
            mapNotes = header("notes").ToString()

            ' Set the window title 
            Me.Text = "Map: " & mapName

            '' Set the window title - Remove ".map"
            'Me.Text = "Map: " & mapName.Substring(0, mapName.Length - 4) & ".json"

            ' Update status bar with map name
            mapNameLabel.Text = "Map: " & JsonMapFile & " / " & mapFileName & " / " & mapName & " (" & mapWidth & "x" & mapHeight & ")"

            ' Get the tiles array
            Dim tilesArray As JArray = jsonObject("tiles")

            ' Determine if tiles are stored in a flat array or as 2D array
            If tilesArray(0).Type = JTokenType.Array Then
                ' 2D array format (PerRow or PerRowPadded)
                mapTiles = New Integer(mapHeight - 1, mapWidth - 1) {}
                For y As Integer = 0 To mapHeight - 1
                    Dim rowArray As JArray = CType(tilesArray(y), JArray)
                    For x As Integer = 0 To mapWidth - 1
                        mapTiles(y, x) = CInt(rowArray(x))
                    Next
                Next
            Else
                ' Flat array format (Original)
                mapTiles = New Integer(mapHeight - 1, mapWidth - 1) {}
                For y As Integer = 0 To mapHeight - 1
                    For x As Integer = 0 To mapWidth - 1
                        Dim index As Integer = y * mapWidth + x
                        mapTiles(y, x) = CInt(tilesArray(index))
                    Next
                Next
            End If

            ' Get tileset information
            Dim tilesetData As JObject = jsonObject("tileset")
            If tilesetData IsNot Nothing Then
                Dim sourcesArray As JArray = tilesetData("sources")
                If sourcesArray IsNot Nothing AndAlso sourcesArray.Count > 0 Then
                    ' Get the first tileset filename
                    Dim firstSource As JObject = sourcesArray(0)
                    Dim tilesetFilename As String = firstSource("filename").ToString()

                    ' Extract the first 6 characters as the prefix
                    mapTileSetPrefix = tilesetFilename.Substring(0, 6)

                    'Debug.WriteLine("Tileset prefix: " & mapTileSetPrefix)
                End If
            End If

            ' Check if the map uses default tileMappings
            Dim usesDefaultTileMappings As Boolean = False

            If tilesetData IsNot Nothing Then
                Dim tileMappingsArray As JArray = tilesetData("tileMappings")       'Get the tileMappings
                If tileMappingsArray IsNot Nothing Then
                    ' Just parse the tileMappingsArray to HasDefaultTileMappings
                    usesDefaultTileMappings = HasDefaultTileMappings(tileMappingsArray)

                    If usesDefaultTileMappings = False Then
                        Debug.WriteLine("WARNING: Map does not use default tileMappings: " & mapFileName)
                        fMain.AppendToConsole("WARNING: Map does not use default tileMappings: " & mapFileName)

                    ElseIf usesDefaultTileMappings = True Then
                        Debug.WriteLine("Map tileMappings are default: " & mapFileName)

                    End If

                Else
                    Debug.WriteLine("tileMappings is Empty")

                End If
            End If


            '' Check if the map uses default tileMappings
            'Dim usesDefaultTileMappings As Boolean = HasDefaultTileMappings(jsonObject)
            'If Not usesDefaultTileMappings Then
            '    Debug.WriteLine("WARNING: Map does not use default tileMappings: " & mapFileName)
            '    fMain.AppendToConsole("WARNING: Map does not use default tileMappings: " & mapFileName)
            'Else
            '    Debug.WriteLine("Map tileMappings are default: " & mapFileName)
            'End If

            ' Set up panel scrolling and size
            pnlMap.AutoScrollMinSize = New Size(mapWidth * tileSize * mapScale, mapHeight * tileSize * mapScale)

            ' Set default zoom level to 50% - We want to get a better look at the map
            SetZoomLevel(0.5F)

            ' Force a redraw of the panel
            pnlMap.Invalidate()

        Catch ex As Exception
            MessageBox.Show("Error loading map: " & ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error)
        End Try
    End Sub

    Private Sub PnlMap_Paint(sender As Object, e As PaintEventArgs)
        If mapTiles Is Nothing Then Return

        ' Get the graphics object
        Dim g As Graphics = e.Graphics
        g.InterpolationMode = Drawing2D.InterpolationMode.NearestNeighbor
        g.PixelOffsetMode = Drawing2D.PixelOffsetMode.Half

        ' Calculate the visible area
        Dim visibleRect As Rectangle = pnlMap.ClientRectangle
        Dim startX As Integer = Math.Max(0, (pnlMap.AutoScrollPosition.X * -1) \ (tileSize * mapScale))
        Dim startY As Integer = Math.Max(0, (pnlMap.AutoScrollPosition.Y * -1) \ (tileSize * mapScale))
        Dim endX As Integer = Math.Min(mapWidth - 1, startX + (visibleRect.Width \ (tileSize * mapScale)) + 1)
        Dim endY As Integer = Math.Min(mapHeight - 1, startY + (visibleRect.Height \ (tileSize * mapScale)) + 1)

        ' Draw only the visible tiles
        For y As Integer = startY To endY
            For x As Integer = startX To endX
                If y >= 0 AndAlso y < mapHeight AndAlso x >= 0 AndAlso x < mapWidth Then
                    Dim tileIndex As Integer = mapTiles(y, x)
                    Dim tileBitmap As Bitmap = GetTileBitmap(tileIndex)

                    If tileBitmap IsNot Nothing Then
                        Dim destRect As New RectangleF(
                            x * tileSize * mapScale + viewOffsetX + pnlMap.AutoScrollPosition.X,
                            y * tileSize * mapScale + viewOffsetY + pnlMap.AutoScrollPosition.Y,
                            tileSize * mapScale,
                            tileSize * mapScale)

                        g.DrawImage(tileBitmap, destRect)
                    End If
                End If
            Next
        Next
    End Sub

    Private Function GetTileBitmap(tileIndex As Integer) As Bitmap
        ' Check if we've already loaded this tile
        If tileCache.ContainsKey(tileIndex) Then
            Return tileCache(tileIndex)
        End If

        ' Format the tile index as a 4-digit number (e.g., 0 -> "0000")
        Dim formattedTileIndex As String = tileIndex.ToString("D4")

        ' Try to load the tile from file
        Try
            Dim tileFilePath As String = Path.Combine(TilesetLocation, formattedTileIndex & ".bmp")

            If File.Exists(tileFilePath) Then
                Dim bitmap As New Bitmap(tileFilePath)
                tileCache.Add(tileIndex, bitmap)
                Return bitmap
            Else
                ' If tile doesn't exist, create a red placeholder
                Dim errorBitmap As New Bitmap(tileSize, tileSize)
                Using g As Graphics = Graphics.FromImage(errorBitmap)
                    g.Clear(System.Drawing.Color.Red)
                End Using

                tileCache.Add(tileIndex, errorBitmap)
                Debug.WriteLine("Tile doesn't exist: " & tileIndex)

                Return errorBitmap
            End If
        Catch ex As Exception
            ' If there's an error loading the file, return a red placeholder
            Dim errorBitmap As New Bitmap(tileSize, tileSize)
            Using g As Graphics = Graphics.FromImage(errorBitmap)
                g.Clear(System.Drawing.Color.Red)
            End Using

            Return errorBitmap
        End Try
    End Function

    ' Add a method to change the zoom level
    Public Sub SetZoom(zoomFactor As Single)
        mapScale = zoomFactor
        pnlMap.AutoScrollMinSize = New Size(mapWidth * tileSize * mapScale, mapHeight * tileSize * mapScale)
        pnlMap.Invalidate()
    End Sub

    Private Sub fMapView_Closed(sender As Object, e As EventArgs) Handles Me.Closed

        'Debug.WriteLine("1 - fMapView_Closed")

    End Sub

    ' Clean up when the form is closed
    Protected Overrides Sub OnFormClosed(e As FormClosedEventArgs)
        ' Clear and dispose of all cached bitmaps
        For Each bitmap In tileCache.Values
            bitmap.Dispose()
        Next
        tileCache.Clear()

        'Debug.WriteLine("2 - OnFormClosed")

        Debug.WriteLine("Map Closed: " & mapJsonFileName)
        fMain.AppendToConsole("Map Closed: " & mapJsonFileName)

        If fMain.LoadedWithCommandLineArgs = True Then
            'If the application was loaded via command line (eg after OP2MapGenerator)
            'then we want to exit the entire application when we close this form

            'Debug.WriteLine("LoadedWithCommandLineArgs = True")
            'MsgBox("LoadedWithCommandLineArgs = True")

            Application.Exit()

        End If

        MyBase.OnFormClosed(e)
    End Sub




    Private Sub pnlMap_MouseDown(sender As Object, e As MouseEventArgs) Handles pnlMap.MouseDown
        If e.Button = MouseButtons.Right Then
            isRightMouseDown = True
            lastMouseX = e.X
            lastMouseY = e.Y
            pnlMap.Cursor = Cursors.Hand
        End If
    End Sub

    Private Sub pnlMap_MouseMove(sender As Object, e As MouseEventArgs) Handles pnlMap.MouseMove
        If isRightMouseDown Then
            ' Calculate how far the mouse has moved since the last position
            Dim deltaX As Integer = e.X - lastMouseX
            Dim deltaY As Integer = e.Y - lastMouseY

            ' Update the AutoScrollPosition based on the mouse movement
            ' Note: AutoScrollPosition uses negative values, so we need to reverse the delta
            Dim newX As Integer = pnlMap.AutoScrollPosition.X + deltaX
            Dim newY As Integer = pnlMap.AutoScrollPosition.Y + deltaY

            ' AutoScrollPosition is read-only, so we need to use ScrollControlIntoView
            ' or set the AutoScrollPosition directly with negative values
            pnlMap.AutoScrollPosition = New Point(-newX, -newY)

            ' Update the last position
            lastMouseX = e.X
            lastMouseY = e.Y
        End If
    End Sub

    Private Sub pnlMap_MouseUp(sender As Object, e As MouseEventArgs) Handles pnlMap.MouseUp
        If e.Button = MouseButtons.Right Then
            isRightMouseDown = False
            pnlMap.Cursor = Cursors.Default
        End If
    End Sub


    Private Sub pnlMap_MouseWheel(sender As Object, e As MouseEventArgs) Handles pnlMap.MouseWheel
        ' Handle mouse wheel for zooming
        ' Check if Ctrl key is pressed while using the mouse wheel
        If ModifierKeys = Keys.Control Then
            ' Store old zoom level for positioning calculations
            Dim oldZoom As Single = mapScale

            ' Get mouse position relative to the map
            Dim mousePoint As Point = pnlMap.PointToClient(MousePosition)

            ' Store the current position under the mouse in world coordinates
            Dim worldX As Single = (mousePoint.X - pnlMap.AutoScrollPosition.X) / oldZoom
            Dim worldY As Single = (mousePoint.Y - pnlMap.AutoScrollPosition.Y) / oldZoom

            ' Fixed zoom levels (in decimal form) 10% to 150%
            Dim zoomLevels As Single() = {0.1F, 0.25F, 0.5F, 0.75F, 1.0F, 1.5F}

            ' Find current index in the zoom levels array
            Dim currentIndex As Integer = -1
            For i As Integer = 0 To zoomLevels.Length - 1
                ' Use a small epsilon for floating point comparison
                If Math.Abs(mapScale - zoomLevels(i)) < 0.01F Then
                    currentIndex = i
                    Exit For
                End If
            Next

            ' If current zoom is not in our defined levels, find closest level
            If currentIndex = -1 Then
                Dim minDifference As Single = Single.MaxValue
                For i As Integer = 0 To zoomLevels.Length - 1
                    Dim difference As Single = Math.Abs(mapScale - zoomLevels(i))
                    If difference < minDifference Then
                        minDifference = difference
                        currentIndex = i
                    End If
                Next
            End If

            ' Adjust index based on scroll direction
            Dim newIndex As Integer = currentIndex
            If e.Delta > 0 Then
                ' Zoom in - move to next higher zoom level
                newIndex = Math.Min(zoomLevels.Length - 1, currentIndex + 1)
            Else
                ' Zoom out - move to next lower zoom level
                newIndex = Math.Max(0, currentIndex - 1)
            End If

            ' Set the new zoom level using our centralized function
            SetZoomLevel(zoomLevels(newIndex))

            ' Calculate new scroll position to keep the point under the mouse
            Dim newX As Integer = CInt(worldX * mapScale - mousePoint.X)
            Dim newY As Integer = CInt(worldY * mapScale - mousePoint.Y)
            pnlMap.AutoScrollPosition = New Point(Math.Max(0, -newX), Math.Max(0, -newY))
        End If
    End Sub


    'Private Sub pnlMap_MouseWheel(sender As Object, e As MouseEventArgs) Handles pnlMap.MouseWheel
    '    ' Handle mouse wheel for zooming

    '    ' Check if Ctrl key is pressed while using the mouse wheel
    '    If ModifierKeys = Keys.Control Then
    '        ' Store old zoom level for positioning calculations
    '        Dim oldZoom As Single = mapScale

    '        ' Get mouse position relative to the map
    '        Dim mousePoint As Point = pnlMap.PointToClient(MousePosition)

    '        ' Store the current position under the mouse in world coordinates
    '        Dim worldX As Single = (mousePoint.X - pnlMap.AutoScrollPosition.X) / oldZoom
    '        Dim worldY As Single = (mousePoint.Y - pnlMap.AutoScrollPosition.Y) / oldZoom

    '        ' Fixed zoom levels (in decimal form)   25% to 200%
    '        Dim zoomLevels As Single() = {0.25F, 0.5F, 0.75F, 1.0F, 1.5F, 2.0F}

    '        ' Find current index in the zoom levels array
    '        Dim currentIndex As Integer = -1
    '        For i As Integer = 0 To zoomLevels.Length - 1
    '            ' Use a small epsilon for floating point comparison
    '            If Math.Abs(mapScale - zoomLevels(i)) < 0.01F Then
    '                currentIndex = i
    '                Exit For
    '            End If
    '        Next

    '        ' If current zoom is not in our defined levels, find closest level
    '        If currentIndex = -1 Then
    '            Dim minDifference As Single = Single.MaxValue
    '            For i As Integer = 0 To zoomLevels.Length - 1
    '                Dim difference As Single = Math.Abs(mapScale - zoomLevels(i))
    '                If difference < minDifference Then
    '                    minDifference = difference
    '                    currentIndex = i
    '                End If
    '            Next
    '        End If

    '        ' Adjust index based on scroll direction
    '        Dim newIndex As Integer = currentIndex
    '        If e.Delta > 0 Then
    '            ' Zoom in - move to next higher zoom level
    '            newIndex = Math.Min(zoomLevels.Length - 1, currentIndex + 1)
    '        Else
    '            ' Zoom out - move to next lower zoom level
    '            newIndex = Math.Max(0, currentIndex - 1)
    '        End If

    '        ' Set the new zoom level
    '        mapScale = zoomLevels(newIndex)

    '        ' Apply the zoom
    '        pnlMap.AutoScrollMinSize = New Size(mapWidth * tileSize * mapScale, mapHeight * tileSize * mapScale)

    '        ' Calculate new scroll position to keep the point under the mouse
    '        Dim newX As Integer = CInt(worldX * mapScale - mousePoint.X)
    '        Dim newY As Integer = CInt(worldY * mapScale - mousePoint.Y)
    '        pnlMap.AutoScrollPosition = New Point(Math.Max(0, -newX), Math.Max(0, -newY))

    '        ' Update zoom level label - show as percentage
    '        Dim lblZoomLevel As ToolStripStatusLabel = DirectCast(StatusStrip.Items("lblZoomLevel"), ToolStripStatusLabel)
    '        lblZoomLevel.Text = $"Zoom: {Math.Round(mapScale * 100)}%"

    '        ' Force redraw
    '        pnlMap.Invalidate()
    '    End If
    'End Sub

    Public Sub SetZoomLevel(zoomLevel As Single)
        ' Set the zoom scale
        mapScale = zoomLevel

        ' Update the zoom level label in status bar if it exists
        If StatusStrip IsNot Nothing AndAlso StatusStrip.Items.ContainsKey("lblZoomLevel") Then
            Dim lblZoomLevel As ToolStripStatusLabel = DirectCast(StatusStrip.Items("lblZoomLevel"), ToolStripStatusLabel)
            lblZoomLevel.Text = $"Zoom: {Math.Round(mapScale * 100)}%"
        End If

        ' Update the panel's scroll size if map is loaded
        If mapTiles IsNot Nothing AndAlso mapWidth > 0 AndAlso mapHeight > 0 Then
            pnlMap.AutoScrollMinSize = New Size(mapWidth * tileSize * mapScale, mapHeight * tileSize * mapScale)
        End If

        ' Force a redraw of the panel
        pnlMap.Invalidate()
    End Sub


    Private Function HasDefaultTileMappings(tileMappingsArray As JArray) As Boolean
        ' Check if the map has the default tileMappings by looking at the first 4 entries
        Try
            ' Get the tileset information and tileMappings
            'Dim tilesetData As JObject = jsonObject("tileset")
            'If tilesetData Is Nothing Then Return False

            'Dim tileMappingsArray As JArray = tilesetData("tileMappings")
            'If tileMappingsArray Is Nothing OrElse tileMappingsArray.Count < 4 Then Return False

            ' Verify we have enough entries to check
            If tileMappingsArray Is Nothing OrElse tileMappingsArray.Count < 4 Then Return False



            ' Check first entry (index 0)
            If CInt(tileMappingsArray(0)("tilesetIndex")) <> 0 OrElse
               CInt(tileMappingsArray(0)("tileGraphicIndex")) <> 0 OrElse
               CInt(tileMappingsArray(0)("animationCount")) <> 0 OrElse
               CInt(tileMappingsArray(0)("animationDelay")) <> 0 Then
                Return False
            End If

            ' Check second entry (index 1)
            If CInt(tileMappingsArray(1)("tilesetIndex")) <> 1 OrElse
               CInt(tileMappingsArray(1)("tileGraphicIndex")) <> 0 OrElse
               CInt(tileMappingsArray(1)("animationCount")) <> 0 OrElse
               CInt(tileMappingsArray(1)("animationDelay")) <> 0 Then
                Return False
            End If

            ' Check third entry (index 2)
            If CInt(tileMappingsArray(2)("tilesetIndex")) <> 1 OrElse
               CInt(tileMappingsArray(2)("tileGraphicIndex")) <> 1 OrElse
               CInt(tileMappingsArray(2)("animationCount")) <> 0 OrElse
               CInt(tileMappingsArray(2)("animationDelay")) <> 0 Then
                Return False
            End If

            ' Check fourth entry (index 3)
            If CInt(tileMappingsArray(3)("tilesetIndex")) <> 1 OrElse
               CInt(tileMappingsArray(3)("tileGraphicIndex")) <> 2 OrElse
               CInt(tileMappingsArray(3)("animationCount")) <> 0 OrElse
               CInt(tileMappingsArray(3)("animationDelay")) <> 0 Then
                Return False
            End If

            ' If all checks passed, this map has default tileMappings
            Return True

        Catch ex As Exception
            Debug.WriteLine("Error checking default tileMappings: " & ex.Message)
            Return False
        End Try
    End Function

End Class
