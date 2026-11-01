using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Interop;
using OrbyLauncher.Native;
using OrbyLauncher.Services;

namespace OrbyLauncher.Views;
internal sealed class VirtualKeyboardWindow : FloatingWindow
{
    protected override bool AttachedTop => false;
    protected override bool UsesIslandAppearance => true;
    protected override string? MaterialOverride => "Acrylic";
    private readonly Grid keys = new();
    private readonly TextBlock message = new() { Text = "Clique em um campo de texto e use as teclas abaixo.", FontSize = 11, TextWrapping = TextWrapping.Wrap };
    private readonly HashSet<string> modifiers = new();
    private readonly List<(Button Button, string Id)> modifierButtons = new();
    private readonly List<(Button Button, char Plain, char Upper)> letterButtons = new();
    private string accent = "";
    private bool caps, symbols;
    [DllImport("user32.dll", EntryPoint = "GetWindowLongW")] private static extern int GetWindowLong(nint window, int index);
    [DllImport("user32.dll", EntryPoint = "SetWindowLongW")] private static extern int SetWindowLong(nint window, int index, int value);
    internal VirtualKeyboardWindow(SettingsStore store) : base(store)
    {
        Title = "Orby Launcher · Teclado"; Focusable = false;
        Surface.Padding = new Thickness(14); Surface.CornerRadius = new CornerRadius(20);
        var panel = new DockPanel(); var heading = new DockPanel { Margin = new Thickness(2, 0, 2, 10) };
        var close = Tool("Fechar", () => Hide()); DockPanel.SetDock(close, Dock.Right); heading.Children.Add(close);
        var native = Tool("Teclado Windows", () => { Hide(); SystemActions.Run("osk.exe"); }); DockPanel.SetDock(native, Dock.Right); heading.Children.Add(native);
        heading.Children.Add(new TextBlock { Text = "Teclado", FontSize = 16, FontWeight = FontWeights.SemiBold, VerticalAlignment = VerticalAlignment.Center });
        heading.MouseLeftButtonDown += (_, _) => { if (!close.IsMouseOver && !native.IsMouseOver) DragMove(); };
        DockPanel.SetDock(heading, Dock.Top); panel.Children.Add(heading);
        var footer = new DockPanel { Margin = new Thickness(2, 8, 2, 0) };
        var layout = Tool("PT-BR / US", () => store.Update(p => p.KeyboardLayout = p.KeyboardLayout == "US" ? "PT-BR" : "US"));
        DockPanel.SetDock(layout, Dock.Right); footer.Children.Add(layout);
        footer.Children.Add(message); DockPanel.SetDock(footer, Dock.Bottom); panel.Children.Add(footer);
        panel.Children.Add(keys); Surface.Child = panel;
        SourceInitialized += (_, _) =>
        {
            nint handle = new WindowInteropHelper(this).Handle;
            SetWindowLong(handle, -20, GetWindowLong(handle, -20) | 0x08000000 | 0x80);
            HwndSource.FromHwnd(handle).AddHook((nint hwnd, int msg, nint wp, nint lp, ref bool handled) => { if (msg == 0x21) { handled = true; return 3; } return 0; });
        };
        IsVisibleChanged += (_, _) => { if (!IsVisible) { modifiers.Clear(); accent = ""; RefreshLetters(); RefreshModifierColors(); } };
        store.Changed += PreferencesChanged;
        Closed += (_, _) => store.Changed -= PreferencesChanged;
        Build();
    }
    private void PreferencesChanged(object? sender, EventArgs e) { Build(); RefreshSettings(); }
    private static Button Tool(string label, Action action)
    {
        var button = new Button { Content = label, Focusable = false, IsTabStop = false, Margin = new Thickness(3), Padding = new Thickness(9, 5, 9, 5) };
        button.Click += (_, _) => action(); return button;
    }
    private void Build()
    {
        keys.Children.Clear(); keys.RowDefinitions.Clear(); keys.ColumnDefinitions.Clear(); modifierButtons.Clear(); letterButtons.Clear();
        for (int i = 0; i < 6; i++) keys.RowDefinitions.Add(new RowDefinition());
        int columns = Store.Value.KeyboardNumberPad ? 20 : 16;
        for (int i = 0; i < columns; i++) keys.ColumnDefinitions.Add(new ColumnDefinition());
        Add(0, 0, "Esc", null, 0x1B);
        for (int i = 1; i <= 12; i++) Add(0, i, "F" + i, null, (ushort)(0x6F + i));
        Add(0, 13, "Ins", null, 0x2D); Add(0, 14, "Del", null, 0x2E); Add(0, 15, "PrtSc", null, 0x2C);
        string[] baseRows = Store.Value.KeyboardLayout == "US"
            ? new[] { "`1234567890-=", "qwertyuiop[]\\", "asdfghjkl;'", "zxcvbnm,./" }
            : new[] { "'1234567890-=", "qwertyuiop´[", "asdfghjklç~]", "zxcvbnm,.;/" };
        string[] shifted = Store.Value.KeyboardLayout == "US"
            ? new[] { "~!@#$%^&*()_+", "QWERTYUIOP{}|", "ASDFGHJKL:\"", "ZXCVBNM<>?" }
            : new[] { "\"!@#$%¨&*()_+", "QWERTYUIOP`{", "ASDFGHJKLÇ^}", "ZXCVBNM<>:?" };
        for (int row = 0; row < baseRows.Length; row++)
        {
            int offset = row == 0 ? 0 : 2;
            for (int c = 0; c < baseRows[row].Length; c++)
            {
                char plain = baseRows[row][c], upper = c < shifted[row].Length ? shifted[row][c] : plain;
                var button = Add(row + 1, offset + c, Letter(plain, upper), null, dynamicText: () => Letter(plain, upper));
                letterButtons.Add((button, plain, upper));
            }
        }
        Add(1, 13, "⌫", null, 8, 3); Modifier(2, "Tab", null, 9, 2, false);
        Modifier(3, "Caps", "Caps", 0, 2); Add(3, 14, "Enter", null, 13, 2);
        Modifier(4, "Shift", "Shift", 0, 2); Add(4, 12, "↑", null, 0x26); Add(4, 13, "Home", null, 0x24); Add(4, 14, "End", null, 0x23); Add(4, 15, "PgUp", null, 0x21);
        Add(5, 0, "Ctrl", "@Ctrl", 0, 2); Add(5, 2, "Win", "@Win"); Add(5, 3, "Alt", "@Alt");
        Add(5, 4, symbols ? "ABC" : "#+=", "@Symbols"); Add(5, 5, "Espaço", " ", 0, 6);
        Add(5, 11, "AltGr", "@AltGr"); Add(5, 12, "←", null, 0x25); Add(5, 13, "↓", null, 0x28); Add(5, 14, "→", null, 0x27); Add(5, 15, "PgDn", null, 0x22);
        if (symbols)
        {
            string values = "áéíóúàâêôãõüÁÉÍÓÚÀÂÊÔÃÕÜ€£¥©®™°ºª¿¡±×÷≠≤≥∞µ§|\\{}[]";
            for (int i = keys.Children.Count - 1; i >= 0; i--) if (Grid.GetRow(keys.Children[i]) is >= 1 and <= 3) keys.Children.RemoveAt(i);
            letterButtons.RemoveAll(entry => Grid.GetRow(entry.Button) <= 3);
            modifierButtons.RemoveAll(entry => Grid.GetRow(entry.Button) is >= 1 and <= 3);
            for (int i = 0; i < values.Length && i < 48; i++) Add(1 + i / 16, i % 16, values[i].ToString(), values[i].ToString());
            for (int i = keys.Children.Count - 1; i >= 0; i--)
                if ((Grid.GetRow(keys.Children[i]) == 1 && Grid.GetColumn(keys.Children[i]) >= 13) || (Grid.GetRow(keys.Children[i]) == 3 && Grid.GetColumn(keys.Children[i]) >= 14)) keys.Children.RemoveAt(i);
            Add(1, 13, "⌫", null, 8, 3); Add(3, 14, "Enter", null, 13, 2);
        }
        if (Store.Value.KeyboardNumberPad)
        {
            Add(0, 16, "Numérico", "", 0, 4);
            string[] numeric = { "789/", "456*", "123-", "0.,+" };
            for (int row = 0; row < 4; row++) for (int c = 0; c < 4; c++) Add(row + 1, 16 + c, numeric[row][c].ToString(), numeric[row][c].ToString());
            Add(5, 16, "Enter", null, 13, 4);
        }
        RefreshModifierColors();
    }
    private void Modifier(int row, string name, string? id, ushort key, int span, bool modifier = true) => Add(row, 0, name, modifier ? "@" + id : null, key, span);
    private Button Add(int row, int column, string label, string? text, ushort key = 0, int span = 1, Func<string>? dynamicText = null)
    {
        var button = Tool(label, () => Press(dynamicText?.Invoke() ?? text, key)); button.MinWidth = 0; button.MinHeight = 0; button.Padding = new Thickness(2); button.FontSize = 13;
        AutomationProperties.SetName(button, label == " " ? "Espaço" : label);
        Grid.SetRow(button, row); Grid.SetColumn(button, column); Grid.SetColumnSpan(button, span); keys.Children.Add(button);
        if (text is { Length: > 1 } && text.StartsWith('@')) modifierButtons.Add((button, text[1..]));
        return button;
    }
    private string Letter(char plain, char upper) => (char.IsLetter(plain) ? (caps ^ modifiers.Contains("Shift") ? char.ToUpperInvariant(plain) : plain) : modifiers.Contains("Shift") ? upper : plain).ToString();
    private void RefreshLetters()
    {
        foreach (var entry in letterButtons)
        { string label = Letter(entry.Plain, entry.Upper); entry.Button.Content = label; AutomationProperties.SetName(entry.Button, label); }
    }
    private void RefreshModifierColors()
    {
        foreach (var entry in modifierButtons)
        {
            bool active = entry.Id == "Caps" ? caps : entry.Id == "Symbols" ? symbols : modifiers.Contains(entry.Id);
            entry.Button.SetResourceReference(Control.BackgroundProperty, active ? "Think.AccentSurface" : "Think.Control");
            entry.Button.SetResourceReference(Control.ForegroundProperty, active ? "Think.AccentInk" : "Think.Text");
        }
    }
    private void Press(string? text, ushort key)
    {
        if (text is { Length: > 1 } && text.StartsWith('@'))
        {
            string id = text[1..]; if (id == "Caps") caps = !caps; else if (id == "Symbols") symbols = !symbols; else if (!modifiers.Add(id)) modifiers.Remove(id);
            if (id == "Symbols") Build(); else { RefreshLetters(); RefreshModifierColors(); } return;
        }
        if (text == "") return;
        if ((text is "´" or "`" or "~" or "^" or "¨") && accent != text && !modifiers.Contains("Ctrl") && !modifiers.Contains("Alt") && !modifiers.Contains("Win") && !modifiers.Contains("AltGr"))
        { accent = text; message.Text = "Acento " + text + " · escolha uma letra ou Espaço."; return; }
        if (accent.Length > 0 && text is not null && !modifiers.Contains("Ctrl") && !modifiers.Contains("Alt") && !modifiers.Contains("Win") && !modifiers.Contains("AltGr"))
        {
            string mark = accent switch { "´" => "\u0301", "`" => "\u0300", "~" => "\u0303", "^" => "\u0302", _ => "\u0308" };
            text = text == " " || text == accent ? accent : (text + mark).Normalize(NormalizationForm.FormC); accent = "";
        }
        nint current = VirtualKeyboardInput.GetForegroundWindow();
        if (current == 0 || current == new WindowInteropHelper(this).Handle) { message.Text = "Selecione um campo no aplicativo de destino."; return; }
        bool sent = VirtualKeyboardInput.Send(text, key, modifiers.Contains("Ctrl") || modifiers.Contains("AltGr"), modifiers.Contains("Alt") || modifiers.Contains("AltGr"), modifiers.Contains("Win"), modifiers.Contains("Shift"));
        message.Text = sent ? "Teclado · " + Store.Value.KeyboardLayout : "Entrada bloqueada. Aplicativos elevados ou protegidos podem exigir o teclado do Windows.";
        modifiers.Clear(); if (text is null) accent = ""; RefreshLetters(); RefreshModifierColors();
    }
    public override void Position()
    {
        double scale = double.IsFinite(Store.Value.KeyboardScale) ? Math.Clamp(Store.Value.KeyboardScale, .7, 1.3) : 1;
        double width = Math.Min(Display.Width - 24, (Store.Value.KeyboardNumberPad ? 1120 : 920) * scale);
        double height = Math.Min(Display.Height - 32, 350 * scale);
        Width = Math.Max(280, width); Height = Math.Max(200, height);
        nint handle = new WindowInteropHelper(this).Handle; if (handle == 0) return;
        int left = Display.Work.Left + (int)Math.Round((Display.Width - Width) * Display.Scale / 2);
        int top = Display.Work.Bottom - (int)Math.Round((Height + 18) * Display.Scale);
        OverlayChrome.Move(handle, new OverlayChrome.PixelRect { Left = left, Top = top, Right = left + (int)Math.Round(Width * Display.Scale), Bottom = top + (int)Math.Round(Height * Display.Scale) });
    }
}
