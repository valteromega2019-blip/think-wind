using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Shapes;
using Forms = System.Windows.Forms;

using ThinkWind.Services;
namespace ThinkWind.Views;

public sealed class OverlayAppearancePanel : UserControl
{
    private OverlayAppearanceOptions _options = new();
    private readonly CheckBox _sync = new();
    private readonly StackPanel _independent = new();
    private readonly Grid _columns = new();
    private readonly StackPanel _controls = new(), _previewSection = new();
    private readonly TextBox _accent = new(), _background = new(), _text = new();
    private readonly Dictionary<string, Slider> _sliders = new();
    private readonly Dictionary<string, TextBlock> _sliderValues = new();
    private readonly Border _previewRoot = new(), _previewBlock = new(), _previewGraph = new();
    private readonly TextBlock _previewTitle = new(), _previewNumbers = new(), _previewNote = new(), _status = new();
    private readonly Polyline _previewLine = new();
    private readonly List<TextBlock> _metricLabels = new();
    private bool _loading;
    private bool? _narrow;
    public Preferences? IslandAppearance { get; set; }
    private readonly TextBlock independentTitle = new();

    public event EventHandler? OptionsChanged;

    public OverlayAppearancePanel()
    {
        MonitoringDesign.Install(Resources);
        NameScope.SetNameScope(this, new NameScope());
        var card = new Border(); card.SetResourceReference(StyleProperty, "CardBorder"); Content = card;
        var root = new StackPanel(); card.Child = root;
        root.Children.Add(Text("Aparência", 17, true));
        root.Children.Add(Text("Alterações e prévia em tempo real.", 11, false, "Muted", new Thickness(0, 4, 0, 8)));
        _sync.Content = Text("Seguir cores, fundo e transparência da ilha", 11);
        Register(_sync, "OverlayThemeSync"); _sync.Margin = new Thickness(0, 0, 0, 12);
        _sync.Checked += (_, _) => Change(); _sync.Unchecked += (_, _) => Change(); root.Children.Add(_sync);
        root.Children.Add(_columns);
        _columns.Children.Add(_controls); _columns.Children.Add(_previewSection);
        independentTitle.Text = "Cores independentes"; independentTitle.FontSize = 13; independentTitle.FontWeight = FontWeights.SemiBold;
        independentTitle.SetResourceReference(TextBlock.ForegroundProperty, "Think.Text"); _controls.Children.Add(independentTitle);
        _controls.Children.Add(_independent);
        _independent.Children.Add(ColorField("Cor de destaque", _accent, "OverlayAccentHex"));
        _independent.Children.Add(ColorField("Cor do fundo", _background, "OverlayBackgroundHex"));
        _independent.Children.Add(ColorField("Cor do texto", _text, "OverlayTextHex"));
        _controls.Children.Add(Text("Transparências", 15, true, margin: new Thickness(0, 4, 0, 8)));
        AddSlider("Fundo do Overlay", "OverlayBackgroundTransparency", 100);
        AddSlider("Blocos de métricas", "OverlayBlockTransparency", 100);
        AddSlider("Gráfico e traçado", "OverlayGraphTransparency", 100);
        AddSlider("Cantos dos blocos", "OverlayCorners", 24);
        _controls.Children.Add(Text("0% mantém a superfície opaca; 100% a torna transparente. Textos e indicadores permanecem legíveis.", 11, false, "Muted"));
        BuildPreview();
        _status.TextWrapping = TextWrapping.Wrap; _status.Margin = new Thickness(0, 12, 0, 0); _status.SetResourceReference(TextBlock.ForegroundProperty, "Muted"); root.Children.Add(_status);
        SizeChanged += (_, _) => Reflow();
        SetOptions(new OverlayAppearanceOptions());
    }

    public OverlayAppearanceOptions GetOptions() => _options.Clone();
    public void SetOptions(OverlayAppearanceOptions? options)
    {
        _options = (options ?? new OverlayAppearanceOptions()).Clone();
        _loading = true;
        try {
            _sync.IsChecked = _options.SyncWithAppTheme;
            _accent.Text = _options.AccentColor; _background.Text = _options.BackgroundColor; _text.Text = _options.TextColor;
            _sliders["OverlayBackgroundTransparency"].Value = _options.BackgroundTransparency;
            _sliders["OverlayBlockTransparency"].Value = _options.BlockTransparency;
            _sliders["OverlayGraphTransparency"].Value = _options.GraphTransparency;
            _sliders["OverlayCorners"].Value = _options.CornerRadius;
        }
        finally { _loading = false; }
        RefreshPreview();
    }

    public void RefreshPreview()
    {
        MonitoringDesign.Install(Resources, IslandAppearance);
        var palette = OverlayPalette.Resolve(_options, Application.Current?.Resources, IslandAppearance);
        _independent.IsEnabled = !_options.SyncWithAppTheme;
        _independent.Visibility = independentTitle.Visibility = _options.SyncWithAppTheme ? Visibility.Collapsed : Visibility.Visible;
        foreach (string id in new[] { "OverlayBackgroundTransparency", "OverlayBlockTransparency", "OverlayCorners" })
            _sliders[id].IsEnabled = !_options.SyncWithAppTheme;
        _previewRoot.Background = OverlayPalette.Brush(palette.Background); _previewRoot.BorderBrush = OverlayPalette.Brush(palette.Border);
        _previewBlock.Background = OverlayPalette.Brush(palette.Block); _previewGraph.Background = OverlayPalette.Brush(palette.Graph);
        _previewRoot.CornerRadius = _previewBlock.CornerRadius = _previewGraph.CornerRadius = new CornerRadius(_options.SyncWithAppTheme && IslandAppearance is not null ? Math.Clamp(IslandAppearance.IslandRadius, 0, 24) : _options.CornerRadius);
        _previewNumbers.Foreground = OverlayPalette.Brush(palette.Accent); _previewTitle.Foreground = OverlayPalette.Brush(palette.Text);
        _previewNote.Foreground = OverlayPalette.Brush(palette.Muted);
        foreach (var label in _metricLabels) label.Foreground = OverlayPalette.Brush(palette.Text);
        byte alpha = (byte)Math.Round((100 - _options.GraphTransparency) * 2.55);
        _previewLine.Stroke = OverlayPalette.Brush(Color.FromArgb(alpha, palette.Accent.R, palette.Accent.G, palette.Accent.B));
        foreach (var pair in _sliders) _sliderValues[pair.Key].Text = pair.Key == "OverlayCorners" ? $"{pair.Value.Value:0} px" : $"{pair.Value.Value:0}%";
        string saved = "Preferências salvas automaticamente.";
        _status.Text = (palette.TextAdjustedForContrast ? "O texto foi ajustado para manter o contraste. " : "O gráfico e os indicadores usam a cor de destaque. ") + saved;
        Reflow();
    }

    private void Change()
    {
        if (_loading) return;
        if (!OverlayAppearanceOptions.IsValidColor(_accent.Text) || !OverlayAppearanceOptions.IsValidColor(_background.Text) || !OverlayAppearanceOptions.IsValidColor(_text.Text)) {
            _status.Text = "Use cores completas no formato #RRGGBB."; return;
        }
        _options = new OverlayAppearanceOptions {
            SyncWithAppTheme = _sync.IsChecked == true, AccentColor = _accent.Text, BackgroundColor = _background.Text, TextColor = _text.Text,
            BackgroundTransparency = _sliders["OverlayBackgroundTransparency"].Value,
            BlockTransparency = _sliders["OverlayBlockTransparency"].Value, GraphTransparency = _sliders["OverlayGraphTransparency"].Value,
            CornerRadius = (int)Math.Round(_sliders["OverlayCorners"].Value)
        }.Normalize();
        RefreshPreview(); OptionsChanged?.Invoke(this, EventArgs.Empty);
    }

    private FrameworkElement ColorField(string label, TextBox input, string name)
    {
        var body = new StackPanel { Margin = new Thickness(0, 0, 0, 12) };
        body.Children.Add(Text(label, margin: new Thickness(0, 0, 0, 4)));
        var row = new Grid(); row.ColumnDefinitions.Add(new ColumnDefinition()); row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        Register(input, name); input.MinWidth = 100; input.MaxLength = 7; input.TextChanged += (_, _) => Change(); row.Children.Add(input);
        var choose = new Button { Content = "Escolher…", Margin = new Thickness(8, 0, 0, 0) }; choose.SetResourceReference(StyleProperty, "OutlineButton");
        choose.Click += (_, _) => PickColor(input); Grid.SetColumn(choose, 1); row.Children.Add(choose); body.Children.Add(row);
        return body;
    }

    private void PickColor(TextBox target)
    {
        if (!IsLoaded) return;
        using var dialog = new Forms.ColorDialog { FullOpen = true, AnyColor = true };
        dialog.Color = System.Drawing.ColorTranslator.FromHtml(OverlayAppearanceOptions.IsValidColor(target.Text) ? target.Text : "#00BFFF");
        IntPtr handle = Window.GetWindow(this) is Window owner ? new WindowInteropHelper(owner).Handle : IntPtr.Zero;
        Forms.DialogResult result = handle == IntPtr.Zero ? dialog.ShowDialog() : dialog.ShowDialog(new ColorOwner(handle));
        if (result == Forms.DialogResult.OK) target.Text = $"#{dialog.Color.R:X2}{dialog.Color.G:X2}{dialog.Color.B:X2}";
    }
    private sealed class ColorOwner(IntPtr handle) : Forms.IWin32Window { public IntPtr Handle => handle; }

    private void AddSlider(string label, string name, double maximum)
    {
        var header = new DockPanel { Margin = new Thickness(0, 0, 0, 4) };
        var value = Text("", 11, false, "Muted"); value.HorizontalAlignment = HorizontalAlignment.Right; DockPanel.SetDock(value, Dock.Right); header.Children.Add(value); header.Children.Add(Text(label));
        _controls.Children.Add(header);
        var slider = new Slider { Minimum = 0, Maximum = maximum, TickFrequency = 1, IsSnapToTickEnabled = true, Margin = new Thickness(0, 0, 0, 12) };
        slider.SetResourceReference(StyleProperty, typeof(Slider));
        Register(slider, name); _sliders[name] = slider; _sliderValues[name] = value; slider.ValueChanged += (_, _) => Change(); _controls.Children.Add(slider);
    }

    private void BuildPreview()
    {
        _previewSection.Children.Add(Text("Pré-visualização", 15, true, margin: new Thickness(0, 0, 0, 8)));
        var checker = new DrawingGroup(); checker.Children.Add(new GeometryDrawing(new SolidColorBrush(Color.FromRgb(115, 115, 115)), null, new RectangleGeometry(new Rect(0, 0, 16, 16))));
        var light = new SolidColorBrush(Color.FromRgb(165, 165, 165)); checker.Children.Add(new GeometryDrawing(light, null, new RectangleGeometry(new Rect(0, 0, 8, 8)))); checker.Children.Add(new GeometryDrawing(light, null, new RectangleGeometry(new Rect(8, 8, 8, 8)))); checker.Freeze();
        var surface = new Border { Padding = new Thickness(16), CornerRadius = new CornerRadius(8), Background = new DrawingBrush(checker) { TileMode = TileMode.Tile, Viewport = new Rect(0, 0, 16, 16), ViewportUnits = BrushMappingMode.Absolute } };
        _previewSection.Children.Add(surface);
        _previewRoot.Padding = new Thickness(12); _previewRoot.BorderThickness = new Thickness(1); surface.Child = _previewRoot;
        var stack = new StackPanel(); _previewRoot.Child = stack;
        _previewTitle.Text = "Think Control · Overlay"; _previewTitle.FontWeight = FontWeights.SemiBold; _previewTitle.Margin = new Thickness(0, 0, 0, 8); stack.Children.Add(_previewTitle);
        _previewBlock.Padding = new Thickness(8); _previewNumbers.Text = "144 FPS · 6,9 ms"; _previewNumbers.FontSize = 20; _previewNumbers.FontWeight = FontWeights.Bold; _previewNumbers.TextWrapping = TextWrapping.Wrap; _previewBlock.Child = _previewNumbers; stack.Children.Add(_previewBlock);
        _previewGraph.Padding = new Thickness(8); _previewGraph.Margin = new Thickness(0, 8, 0, 8); stack.Children.Add(_previewGraph);
        var canvas = new Canvas { Height = 42, ClipToBounds = true, HorizontalAlignment = HorizontalAlignment.Stretch }; _previewGraph.Child = canvas;
        canvas.SizeChanged += (_, _) => _previewLine.RenderTransform = new ScaleTransform(Math.Max(0, canvas.ActualWidth) / 220, 1);
        _previewLine.StrokeThickness = 2; _previewLine.Points = new PointCollection { new(0, 26), new(25, 22), new(50, 30), new(75, 12), new(100, 18), new(125, 16), new(150, 25), new(175, 7), new(200, 16), new(220, 11) }; canvas.Children.Add(_previewLine);
        foreach (string label in new[] { "CPU   48 °C · 24%", "GPU   52 °C · 36%", "RAM   12,2 / 32 GB" }) { var row = new TextBlock { Text = label, FontFamily = new FontFamily("Cascadia Mono, Consolas"), TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 0, 0, 5) }; _metricLabels.Add(row); stack.Children.Add(row); }
        _previewNote.Text = "Amostras ilustrativas · sem leitura de sensores"; _previewNote.FontSize = 10; _previewNote.TextWrapping = TextWrapping.Wrap; _previewNote.Margin = new Thickness(0, 8, 0, 0); stack.Children.Add(_previewNote);
        _previewSection.Children.Add(Text("O padrão quadriculado ajuda a visualizar a transparência. O Overlay real continua separado dos controles do aplicativo.", 11, false, "Muted", new Thickness(0, 8, 0, 0)));
    }

    private void Reflow()
    {
        bool narrow = ActualWidth < 480;
        if (_narrow == narrow) return;
        _narrow = narrow;
        _columns.ColumnDefinitions.Clear(); _columns.RowDefinitions.Clear();
        _columns.ColumnDefinitions.Add(new ColumnDefinition());
        _columns.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        if (narrow) {
            _columns.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); Grid.SetColumn(_previewSection, 0); Grid.SetRow(_previewSection, 1); _previewSection.Margin = new Thickness(0, 12, 0, 0);
        }
        else {
            _columns.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(12) }); _columns.ColumnDefinitions.Add(new ColumnDefinition()); Grid.SetColumn(_previewSection, 2); Grid.SetRow(_previewSection, 0); _previewSection.Margin = new Thickness(0);
        }
    }
    private void Register(FrameworkElement element, string name) { element.Name = name; RegisterName(name, element); }
    private static TextBlock Text(string text, double size = 13, bool bold = false, string resource = "Fg", Thickness? margin = null) {
        var block = new TextBlock { Text = text, FontSize = size, FontWeight = bold ? FontWeights.SemiBold : FontWeights.Normal, TextWrapping = TextWrapping.Wrap, Margin = margin ?? new Thickness(0) };
        block.SetResourceReference(TextBlock.ForegroundProperty, resource); return block;
    }
}
