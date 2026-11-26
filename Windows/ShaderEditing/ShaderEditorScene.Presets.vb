Imports Microsoft.Xna.Framework
Imports ProjectZ.ShaderEditor
Imports ProjectZ.Shared.Drawing.UI.Input
Imports ProjectZ.Shared.Drawing.UI.Layout
Imports ProjectZ.Shared.Drawing.UI.Primitives
Imports ProjectZ.Windows.Composition

Namespace Windows.ShaderEditing
    Partial Friend NotInheritable Class ShaderEditorScene
        Private presetBrowser As CanvasPanel
        Private presetSearch As Textbox
        Private presetCategory As ComboBox
        Private presetResults As ListBox
        Private presetCatalog As IReadOnlyList(Of ShaderPreset)
        Private presetMatches As ShaderPreset() = Array.Empty(Of ShaderPreset)()

        Private Sub SetUpPresetBrowser()
            presetBrowser = Find(Of CanvasPanel)("PresetBrowser")
            presetSearch = Find(Of Textbox)("PresetSearch")
            presetCategory = Find(Of ComboBox)("PresetCategory")
            presetResults = Find(Of ListBox)("PresetResults")
            If realtime Then
                presetCatalog = ShaderPresets.Realtime.Concat(ShaderPresets.All).ToArray()
            ElseIf hosted Then
                presetCatalog = If(surface, SurfaceMaterials.Presets, ShaderPresets.All)
            Else
                presetCatalog = ShaderPresets.All.Concat(SurfaceMaterials.Presets).ToArray()
            End If
            presetCategory.Items = New List(Of String) From {"All categories"}
            presetCategory.Items.AddRange(presetCatalog.Select(AddressOf PresetCategoryName).Distinct(StringComparer.Ordinal))
            presetCategory.SelectedIndex = 0
            presetCategory.ItemAppearance = Sub(index, row, label) label.ForegroundColor = theme.TextColor
            presetResults.ItemAppearance = Sub(index, row, label)
                                               label.ForegroundColor = theme.TextColor
                                               label.FontScale = 0.84F
                                               label.TextWrapping = TextWrapping.NoWrap
                                               label.UseXamlTextLayout = True
                                           End Sub
            AddHandler presetSearch.OnTextChanged, Sub(args) FilterPresets()
            AddHandler presetSearch.Submitted, AddressOf LoadSelectedPreset
            AddHandler presetCategory.SelectionChanged, Sub(sender, index) FilterPresets()
            AddHandler presetResults.SelectionChanged, Sub(sender, index) DescribePreset()
            Bind("Presets", Sub() ShowPresetBrowser(True))
            Bind("ClosePresets", Sub() ShowPresetBrowser(False))
            Bind("UsePreset", AddressOf LoadSelectedPreset)
        End Sub

        Private Sub ArrangePresetBrowser(width As Integer, height As Integer)
            root.SetChildPosition(presetBrowser, New Vector2((width - presetBrowser.Size.X) / 2, (height - presetBrowser.Size.Y) / 2))
        End Sub

        Private Sub StylePresetBrowser(settings As WindowAppearanceSettings)
            presetCategory.DropDownBackgroundColor = settings.SurfaceColor
            presetCategory.DropDownHighlightColor = Color.Lerp(settings.SurfaceColor, settings.AccentColor, 0.3F)
            presetCategory.DropDownIndicatorColor = settings.AccentColor
            presetResults.SelectionColor = presetCategory.DropDownHighlightColor
            Find(Of TextElement)("PresetTitle").ForegroundColor = settings.AccentColor
        End Sub

        Private Sub ShowPresetBrowser(visible As Boolean)
            If visible Then ClearEditorFocus() Else FocusElement(Nothing)
            If Not visible Then presetCategory.IsDropDownOpen = False
            presetBrowser.isVisible = visible
            RefreshModalState()
            If visible Then
                FilterPresets()
                FocusElement(presetSearch)
            End If
            RequestRender()
        End Sub

        Private Sub FilterPresets()
            Dim category = If(presetCategory.SelectedIndex > 0, presetCategory.SelectedItem, Nothing)
            Dim query = presetSearch.Text.Trim()
            Find(Of TextElement)("PresetSearchHint").isVisible = query.Length = 0
            presetMatches = presetCatalog.Where(Function(preset) (category Is Nothing OrElse PresetCategoryName(preset) = category) AndAlso
                ShaderPresets.MatchesSearch(query, preset.Name, preset.Id, PresetCategoryName(preset), preset.Description)).ToArray()
            presetResults.Items = presetMatches.Select(Function(preset) preset.Name & "   ·   " & PresetCategoryName(preset)).ToList()
            presetResults.SelectedIndex = If(presetMatches.Length > 0, 0, -1)
            If presetMatches.Length > 0 Then presetResults.ScrollIntoView(0)
            Find(Of TextElement)("PresetCount").Text = presetMatches.Length & " of " & presetCatalog.Count & " presets"
            DescribePreset()
        End Sub

        Private Function PresetCategoryName(preset As ShaderPreset) As String
            Return If(Not hosted AndAlso SurfaceMaterials.Presets.Contains(preset), "Material / " & preset.Category, preset.Category)
        End Function

        Private Sub DescribePreset()
            Dim selected = presetMatches.ElementAtOrDefault(presetResults.SelectedIndex)
            Find(Of TextElement)("PresetDescription").Text = If(selected?.Description, "No matching presets.")
            Find(Of Button)("UsePreset").isEnabled = selected IsNot Nothing
            RequestRender()
        End Sub

        Private Sub LoadSelectedPreset()
            Dim selected = presetMatches.ElementAtOrDefault(presetResults.SelectedIndex)
            If selected Is Nothing Then Return
            ShowPresetBrowser(False)
            Confirm(Sub()
                        SetDocument(selected.CreateDocument.Invoke(), Nothing, keepSaved:=hosted)
                        SetStatus("Loaded " & selected.Name)
                        ' Preview a new preset without sending it to the host.
                        pendingRunPublishes = False
                        pendingRun = 1
                    End Sub)
        End Sub
    End Class
End Namespace
