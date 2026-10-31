using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Shapes;
using ThinkWind.Services;

namespace ThinkWind.Views;

internal sealed class HardwareMonitorPanel : UserControl
{
    private readonly HardwareTelemetry telemetry;
    private readonly SettingsStore store;
    private readonly StackPanel body = new();
    private readonly OverlayToggleButton overlay;
    private sealed record GpuChoice(string Id, string Label)
    {
        public override string ToString() => Label;
    }
    private bool wideLayout;
    internal bool WideLayout
    {
        get => wideLayout;
        set { if (wideLayout == value) return; wideLayout = value; Render(); }
    }
    private readonly ComboBox gpuPicker = new() { DisplayMemberPath = nameof(GpuChoice.Label), MinHeight = 32, Margin = new Thickness(4, 0, 4, 10) };
    private readonly TextBlock sensorNotice = new() { FontSize = 11, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(4, 0, 4, 8), Visibility = Visibility.Collapsed };
    private bool syncingGpu;
    private string gpuChoiceSignature = "";
    private bool components;
    private int minutes = 5;
    private string chartMetric = "Temperatura";
    private HardwareSample? latest;
    internal event Action? LayoutChanged;
    private Size lastLayoutSize;
    internal HardwareMonitorPanel(SettingsStore settings, HardwareTelemetry source)
    {
        store = settings; telemetry = source;
        overlay = new OverlayToggleButton(store);
        MonitoringDesign.Install(Resources, store.Value);
        var root = new StackPanel();
        var navigation = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(4, 0, 4, 6) };
        AddButton(navigation, "Resumo", () => { components = false; Render(); });
        AddButton(navigation, "Componentes", () => { components = true; Render(); });
        root.Children.Add(navigation);
        var gpuLabel = new TextBlock { Text = "GPU monitorada", FontSize = 12, Margin = new Thickness(4, 2, 4, 6) };
        gpuLabel.SetResourceReference(TextBlock.ForegroundProperty, "Think.Text"); root.Children.Add(gpuLabel);
        gpuPicker.SetResourceReference(Control.ForegroundProperty, "Think.Text");
        gpuPicker.ToolTip = "Escolhe os sensores exibidos no painel e no overlay; não altera a GPU usada pelos aplicativos.";
        System.Windows.Automation.AutomationProperties.SetName(gpuPicker, "GPU monitorada no painel e overlay");
        gpuPicker.SelectionChanged += (_, _) =>
        {
            if (!syncingGpu && gpuPicker.SelectedItem is GpuChoice choice && store.Value.MonitoringGpuId != choice.Id)
                store.Update(p => p.MonitoringGpuId = choice.Id);
        };
        root.Children.Add(gpuPicker);
        sensorNotice.SetResourceReference(TextBlock.ForegroundProperty, "Think.Muted"); root.Children.Add(sensorNotice);
        root.Children.Add(body);
        var footer = new WrapPanel { Margin = new Thickness(4, 8, 4, 0), VerticalAlignment = VerticalAlignment.Center };
        footer.Children.Add(overlay);
        AddButton(footer, "Personalizar overlay", () => ((App)Application.Current).OpenOverlaySettings());
        root.Children.Add(footer); Content = root;
        IsVisibleChanged += (_, _) => telemetry.SetActive(this, IsVisible);
        Loaded += (_, _) => { telemetry.Changed += Update; store.Changed += SettingsChanged; SettingsChanged(null, EventArgs.Empty); telemetry.SetActive(this, IsVisible); if (telemetry.Latest is { } sample) Update(sample); };
        Unloaded += (_, _) => { telemetry.Changed -= Update; store.Changed -= SettingsChanged; telemetry.SetActive(this, false); };
        Render();
    }
    private void SettingsChanged(object? sender, EventArgs e) { MonitoringDesign.Install(Resources, store.Value); UpdateGpuPicker(); Render(); }
    internal static string Number(double? value, string unit, string format = "0") => value is { } number && double.IsFinite(number) ? number.ToString(format) + unit : "—";
    private void Update(HardwareSample sample)
    {
        latest = sample;
        int errorAt = sample.Status.IndexOf("Coletor administrativo:", StringComparison.Ordinal);
        sensorNotice.Text = errorAt >= 0 ? sample.Status[errorAt..] : "";
        sensorNotice.Visibility = errorAt >= 0 ? Visibility.Visible : Visibility.Collapsed;
        if (IsVisible) { UpdateGpuPicker(); Render(); }
    }
    private void UpdateGpuPicker()
    {
        var gpus = latest?.Gpus ?? Array.Empty<GpuReading>();
        string selected = store.Value.MonitoringGpuId ?? "";
        string signature = selected + "|" + string.Join("|", gpus.Select(gpu => gpu.Id + ":" + gpu.Device.Name + ":" + gpu.Integrated));
        if (signature == gpuChoiceSignature) return;
        gpuChoiceSignature = signature; syncingGpu = true;
        try
        {
            var choices = new List<GpuChoice> { new("", "Automático — priorizar GPU dedicada") };
            choices.AddRange(gpus.OrderBy(gpu => gpu.Integrated == false ? 0 : gpu.Integrated is null ? 1 : 2)
                .ThenByDescending(gpu => gpu.DedicatedMemory).Select(gpu => new GpuChoice(gpu.Id,
                    gpu.Device.Name + (gpu.Integrated == false ? " · dedicada" : gpu.Integrated == true ? " · integrada" : ""))));
            if (selected.Length > 0 && choices.All(choice => choice.Id != selected))
                choices.Add(new(selected, "GPU escolhida indisponível — usando seleção automática"));
            gpuPicker.ItemsSource = choices;
            gpuPicker.SelectedItem = choices.First(choice => choice.Id == selected);
        }
        finally { syncingGpu = false; }
    }
    private TextBlock Text(string value, double size = 11, string resource = "Fg", bool mono = false)
    {
        var text = new TextBlock { Text = value, FontSize = size, TextWrapping = TextWrapping.Wrap, FontFamily = new FontFamily(mono ? "Cascadia Mono, Consolas" : "Segoe UI Variable Text") };
        text.SetResourceReference(TextBlock.ForegroundProperty, resource); return text;
    }
    private Border Card(UIElement child)
    {
        var card = new Border { Child = child, Margin = new Thickness(3), Padding = new Thickness(10) };
        card.SetResourceReference(StyleProperty, "CardBorder"); return card;
    }
    private void AddButton(Panel panel, string label, Action action, bool selected = false)
    {
        var button = new Button { Content = label, Margin = new Thickness(0, 0, 6, 0), Padding = new Thickness(8, 4, 8, 4), FontSize = 10 };
        button.SetResourceReference(StyleProperty, "OutlineButton");
        if (selected) { button.SetResourceReference(Control.BackgroundProperty, "Think.AccentSurface"); button.SetResourceReference(Control.BorderBrushProperty, "Think.Accent"); }
        button.Click += (_, _) => action(); panel.Children.Add(button);
    }
    private void Render()
    {
        body.Children.Clear();
        if (components) RenderComponents(); else RenderSummary();
        // Sensor values change frequently; only ask the owning Acrylic window
        // to resize when the board's natural geometry actually changes.
        double measureWidth = double.IsFinite(Width) && Width > 0 ? Width : 520;
        Measure(new Size(measureWidth, double.PositiveInfinity));
        var size = new Size(Math.Ceiling(DesiredSize.Width), Math.Ceiling(DesiredSize.Height));
        if (Math.Abs(size.Width - lastLayoutSize.Width) > 1 || Math.Abs(size.Height - lastLayoutSize.Height) > 1)
        {
            lastLayoutSize = size;
            LayoutChanged?.Invoke();
        }
    }
    private void RenderSummary()
    {
        var s = latest;
        var cards = new UniformGrid { Columns = WideLayout ? 4 : 2 };
        Summary(cards, "\uE950", "Processador", s?.Cpu.Temperature.HasValue == true ? Number(s.Cpu.Temperature," °C") : Number(s?.Cpu.Load,"%"), $"{Number(s?.Cpu.Load,"%")} de uso · {s?.Cpu.Name ?? "Processador"}", s?.Cpu.Load);
        Summary(cards, "\uE7F4", "Placa de vídeo", s?.Gpu.Temperature.HasValue == true ? Number(s.Gpu.Temperature," °C") : Number(s?.Gpu.Load,"%"), $"{Number(s?.Gpu.Load,"%")} de uso · {s?.Gpu.Name ?? "GPU"}", s?.Gpu.Load);
        double? ramPct = s?.RamTotal > 0 ? s.RamUsed / s.RamTotal * 100 : null;
        Summary(cards, "\uE964", "Memória RAM", Number(s?.RamUsed," GB","0.0"), $"{Number(ramPct,"%")} de {Number(s?.RamTotal," GB")}", ramPct);
        double? diskPct = s?.DiskTotal > 0 ? (1-s.DiskFree/s.DiskTotal)*100 : null;
        Summary(cards, "\uEDA2", "Armazenamento", Number(s?.DiskFree," GB","0.0"), "livres em " + (s?.Disk ?? "C:"), diskPct);
        body.Children.Add(cards);
        var bottom = new Grid(); bottom.ColumnDefinitions.Add(new ColumnDefinition()); bottom.ColumnDefinitions.Add(new ColumnDefinition());
        var chart = Card(Chart()); bottom.Children.Add(chart);
        var rings = Card(Rings()); Grid.SetColumn(rings,1); bottom.Children.Add(rings); body.Children.Add(bottom);
    }
    private void Summary(Panel target, string glyph, string label, string value, string detail, double? percent)
    {
        var panel = new StackPanel();
        var heading = new StackPanel { Orientation = Orientation.Horizontal };
        var icon = Text(glyph,13,"Accent"); icon.FontFamily = new FontFamily("Segoe Fluent Icons"); icon.Margin=new Thickness(0,0,8,0); heading.Children.Add(icon); heading.Children.Add(Text(label,11,"Muted")); panel.Children.Add(heading);
        var hero = Text(value,25); hero.FontWeight=FontWeights.SemiBold; hero.Margin=new Thickness(0,8,0,2); panel.Children.Add(hero);
        var description=Text(detail,10,"Muted"); description.TextWrapping=TextWrapping.NoWrap; description.TextTrimming=TextTrimming.CharacterEllipsis; description.ToolTip=detail; panel.Children.Add(description);
        var bar = new ProgressBar { Value=Math.Clamp(percent??0,0,100), Maximum=100, Height=3, Margin=new Thickness(0,8,0,0), BorderThickness=new Thickness(0), Opacity=percent.HasValue?1:.25 };
        bar.SetResourceReference(Control.ForegroundProperty,"Accent"); bar.SetResourceReference(Control.BackgroundProperty,"Line"); panel.Children.Add(bar); target.Children.Add(Card(panel));
    }
    private UIElement Chart()
    {
        var panel=new StackPanel(); panel.Children.Add(Text("CPU · "+chartMetric.ToUpperInvariant(),10,"Muted"));
        Func<HardwareSample,DeviceReading,double?> metric = (_,device)=>chartMetric=="Temperatura"?device.Temperature:chartMetric=="Carga"?device.Load:device.Power;
        var hero=Text(Number(latest is null?null:metric(latest,latest.Cpu),chartMetric=="Temperatura"?" °C":chartMetric=="Carga"?"%":" W","0.0"),24,"Fg",true); hero.Margin=new Thickness(0,5,0,7); panel.Children.Add(hero);
        string unit=chartMetric=="Temperatura"?" °C":chartMetric=="Carga"?"%":" W";
        var first=telemetry.History.FirstOrDefault(sample=>sample.Time>=DateTime.Now.AddMinutes(-minutes));
        double? delta=latest is not null && first is not null ? metric(latest,latest.Cpu)-metric(first,first.Cpu) : null;
        panel.Children.Add(Text("GPU "+Number(latest is null?null:metric(latest,latest.Gpu),unit)+" · Δ CPU "+Number(delta,unit,"+0.0;-0.0;0.0"),10,"Muted"));
        var ranges=new StackPanel { Orientation=Orientation.Horizontal };
        foreach(int range in new[]{1,5,15}) AddButton(ranges,range+" MIN",()=>{minutes=range;Render();},minutes==range); panel.Children.Add(ranges);
        var modes=new WrapPanel { Margin=new Thickness(0,6,0,6) }; foreach(string mode in new[]{"Temperatura","Carga","Consumo"}) AddButton(modes,mode,()=>{chartMetric=mode;Render();},chartMetric==mode); panel.Children.Add(modes);
        var canvas=new Canvas { Width=208,Height=85,ClipToBounds=true,HorizontalAlignment=HorizontalAlignment.Left };
        for(int row=0;row<3;row++) { var line=new System.Windows.Shapes.Line { X1=0,X2=208,Y1=row*41,Y2=row*41,StrokeThickness=1,StrokeDashArray=new DoubleCollection {3,3} }; line.SetResourceReference(Shape.StrokeProperty,"Line");canvas.Children.Add(line); }
        var samples=telemetry.History.Where(sample=>sample.Time >= DateTime.Now.AddMinutes(-minutes)).ToArray();
        double max=chartMetric=="Carga"?100:chartMetric=="Temperatura"?100:Math.Max(100,samples.Select(sample=>Math.Max(metric(sample,sample.Cpu)??0,metric(sample,sample.Gpu)??0)).DefaultIfEmpty(100).Max());
        foreach(bool gpu in new[]{false,true})
        {
            Polyline? current=null;
            foreach(var sample in samples)
            {
                double? value=metric(sample,gpu?sample.Gpu:sample.Cpu);
                if(!value.HasValue){current=null;continue;}
                if(current is null){current=new Polyline {StrokeThickness=1.5};current.SetResourceReference(Shape.StrokeProperty,gpu?"Muted":"Fg");if(gpu)current.StrokeDashArray=new DoubleCollection{3,2};canvas.Children.Add(current);}
                double x=208*(1-(DateTime.Now-sample.Time).TotalMinutes/minutes);
                current.Points.Add(new Point(Math.Clamp(x,0,208),83-Math.Clamp(value.Value/max,0,1)*80));
            }
        }
        panel.Children.Add(canvas); panel.Children.Add(Text($"-{minutes} min                          agora",9,"Faint")); panel.Children.Add(Text("— CPU     ┄ GPU",10,"Muted")); return panel;
    }
    private UIElement Rings()
    {
        var panel=new StackPanel();panel.Children.Add(Text("MEMÓRIA E DISCO",10,"Muted"));panel.Children.Add(Text(Number(latest?.RamUsed," GB","0.0"),24,"Fg",true));panel.Children.Add(Text("de RAM em uso · "+Number(latest?.RamTotal," GB")+" instalados",10,"Faint"));
        var canvas=new Canvas {Width=160,Height=160,HorizontalAlignment=HorizontalAlignment.Center,Margin=new Thickness(0,6,0,6)};
        double?[] ratios={latest?.RamTotal>0?latest.RamUsed/latest.RamTotal:null,latest?.Gpu.MemoryTotal>0?latest.Gpu.MemoryUsed/latest.Gpu.MemoryTotal:null,latest?.DiskTotal>0?1-latest.DiskFree/latest.DiskTotal:null};
        string[] colors={"Fg","Muted","Faint"};
        for(int index=0;index<3;index++)
        {
            double radius=68-index*17;var track=new Ellipse {Width=radius*2,Height=radius*2,StrokeThickness=9};track.SetResourceReference(Shape.StrokeProperty,"Line");Canvas.SetLeft(track,80-radius);Canvas.SetTop(track,80-radius);canvas.Children.Add(track);
            if(ratios[index] is {} fraction && fraction>0)
            {
                double angle=Math.Clamp(fraction,0,.9999)*Math.PI*2;
                var geometry=new PathGeometry();var figure=new PathFigure {StartPoint=new Point(80,80-radius)};
                figure.Segments.Add(new ArcSegment(new Point(80+radius*Math.Sin(angle),80-radius*Math.Cos(angle)),new Size(radius,radius),0,angle>Math.PI,SweepDirection.Clockwise,true));geometry.Figures.Add(figure);
                var arc=new System.Windows.Shapes.Path {Data=geometry,StrokeThickness=9,StrokeStartLineCap=PenLineCap.Round,StrokeEndLineCap=PenLineCap.Round};arc.SetResourceReference(Shape.StrokeProperty,colors[index]);canvas.Children.Add(arc);
            }
        }
        var center=Text(Number(ratios[0]*100,"%"),13,"Fg",true);center.Measure(new Size(double.PositiveInfinity,double.PositiveInfinity));Canvas.SetLeft(center,80-center.DesiredSize.Width/2);Canvas.SetTop(center,80-center.DesiredSize.Height/2);canvas.Children.Add(center);panel.Children.Add(canvas);
        string[] names={"RAM","VRAM","Disco "+(latest?.Disk??"C:")};for(int i=0;i<3;i++)panel.Children.Add(Text("● "+names[i]+"     "+Number(ratios[i]*100,"%"),10,colors[i]));return panel;
    }
    private void RenderComponents()
    {
        var panel=new StackPanel(); var title=Text("Componentes",20);title.FontWeight=FontWeights.SemiBold;panel.Children.Add(title);
        void Device(string name,string description,double? temperature,double? load,double? clock,double? power,string? usage=null)
        {
            var section=new StackPanel {Margin=new Thickness(0,12,0,0)};section.Children.Add(Text(name,13));section.Children.Add(Text(description,10,"Muted"));
            var grid=new UniformGrid {Columns=2,Margin=new Thickness(0,8,0,10)};
            grid.Children.Add(Text("Temperatura\n"+Number(temperature," °C"),11,"Muted",true));grid.Children.Add(Text("Carga\n"+Number(load,"%"),11,"Muted",true));grid.Children.Add(Text("Clock\n"+Number(clock," MHz"),11,"Muted",true));grid.Children.Add(Text("Consumo / uso\n"+(usage??Number(power," W")),11,"Muted",true));section.Children.Add(grid);
            var separator=new Border {Height=1};separator.SetResourceReference(Border.BackgroundProperty,"Line");section.Children.Add(separator);panel.Children.Add(section);
        }
        var s=latest;
        Device("Processador",s?.Cpu.Name??"—",s?.Cpu.Temperature,s?.Cpu.Load,s?.Cpu.Clock,s?.Cpu.Power);
        Device("Placa de vídeo",s?.Gpu.Name??"—",s?.Gpu.Temperature,s?.Gpu.Load,s?.Gpu.Clock,s?.Gpu.Power);
        Device("Memória RAM",Number(s?.RamTotal," GB")+" instalados",null,s?.RamTotal>0?s.RamUsed/s.RamTotal*100:null,null,null,Number(s?.RamUsed," GB","0.0"));
        Device("Disco "+(s?.Disk??"C:"),"Armazenamento do Windows",null,s?.DiskTotal>0?(1-s.DiskFree/s.DiskTotal)*100:null,null,null,Number(s?.DiskFree," GB livres","0.0"));
        body.Children.Add(Card(panel));
    }
}
