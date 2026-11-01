using System;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Input;
using OrbyLauncher.Services;

namespace OrbyLauncher.Views;
internal sealed class CalculatorWidget : Grid
{
    private readonly CalculatorState state = new();
    private readonly TextBlock expression = new() { FontSize = 11, HorizontalAlignment = HorizontalAlignment.Right, TextTrimming = TextTrimming.CharacterEllipsis };
    private readonly TextBlock display = new() { Text = "0", FontSize = 25, FontWeight = FontWeights.SemiBold, HorizontalAlignment = HorizontalAlignment.Right };
    internal CalculatorWidget()
    {
        AutomationProperties.SetName(this, "Calculadora");
        RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        var heading = new TextBlock { Text = "Calculadora", FontSize = 12, FontWeight = FontWeights.SemiBold, Margin = new Thickness(3, 0, 3, 6) };
        expression.SetResourceReference(TextBlock.ForegroundProperty, "Think.Muted");
        display.SetResourceReference(TextBlock.ForegroundProperty, "Think.Text");
        var screen = new StackPanel { Margin = new Thickness(3, 0, 3, 8) };
        screen.Children.Add(heading); screen.Children.Add(expression);
        screen.Children.Add(new Viewbox { Child = display, Stretch = System.Windows.Media.Stretch.Uniform, StretchDirection = StretchDirection.DownOnly, Height = 34, HorizontalAlignment = HorizontalAlignment.Stretch });
        Children.Add(screen);
        var keys = new Grid(); Grid.SetRow(keys, 1); Children.Add(keys);
        for (int c = 0; c < 4; c++) keys.ColumnDefinitions.Add(new ColumnDefinition());
        string[][] rows = { new[] { "C", "±", "%", "÷" }, new[] { "7", "8", "9", "×" }, new[] { "4", "5", "6", "−" }, new[] { "1", "2", "3", "+" }, new[] { "⌫", "0", ",", "=" } };
        for (int r = 0; r < rows.Length; r++)
        {
            keys.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            for (int c = 0; c < 4; c++)
            {
                string key = rows[r][c];
                var button = new Button { Content = key, MinWidth = 0, MinHeight = 30, Padding = new Thickness(3), Margin = new Thickness(2), FontSize = 14 };
                string name = key switch { "⌫" => "Apagar último dígito", "C" => "Limpar cálculo", "±" => "Inverter sinal", "=" => "Calcular resultado", "÷" => "Dividir", "×" => "Multiplicar", _ => key };
                AutomationProperties.SetName(button, name); button.ToolTip = name;
                if (c == 3)
                {
                    button.SetResourceReference(Control.BackgroundProperty, key == "=" ? "Think.Accent" : "Think.AccentSurface");
                    button.SetResourceReference(Control.ForegroundProperty, "Think.AccentInk");
                }
                button.Click += (_, _) => Press(key);
                Grid.SetColumn(button, c); Grid.SetRow(button, r); keys.Children.Add(button);
            }
        }
        PreviewTextInput += (_, e) =>
        {
            string key = e.Text switch { "." => ",", "*" => "×", "/" => "÷", "-" => "−", _ => e.Text };
            if ((key.Length == 1 && char.IsAsciiDigit(key[0])) || key is "," or "+" or "−" or "×" or "÷" or "%" or "=") { Press(key); e.Handled = true; }
        };
        PreviewKeyDown += (_, e) =>
        {
            string? key = e.Key switch { Key.Enter => "=", Key.Back => "⌫", _ => null };
            if (key is not null) { Press(key); e.Handled = true; }
        };
    }
    private void Press(string key)
    {
        state.Press(key); expression.Text = state.Expression; display.Text = state.Display;
        AutomationProperties.SetName(display, "Resultado: " + state.Display);
    }
}
