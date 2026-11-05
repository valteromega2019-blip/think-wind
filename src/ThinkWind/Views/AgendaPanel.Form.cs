using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace OrbyLauncher.Views;
internal sealed partial class AgendaPanel
{
    private static UIElement FormLabel(string glyph, string text)
    {
        var row = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 4, 0, 4) };
        var icon = ClockDesign.Text(glyph, 14); icon.FontFamily = new FontFamily("Segoe Fluent Icons, Segoe MDL2 Assets");
        icon.Margin = new Thickness(0, 0, 7, 0); icon.VerticalAlignment = VerticalAlignment.Center;
        row.Children.Add(icon); row.Children.Add(ClockDesign.Text(text, 12)); return row;
    }
    private static ToggleButton TypeSwitch(bool note)
    {
        var toggle = new ToggleButton { IsChecked = note, Height = 38, Margin = new Thickness(0, 4, 0, 10), ToolTip = "Alternar entre evento e nota" };
        toggle.SetResourceReference(Control.ForegroundProperty, "Think.Text");
        toggle.Template = (ControlTemplate)System.Windows.Markup.XamlReader.Parse("""
<ControlTemplate xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation" xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml" TargetType="ToggleButton">
 <Border CornerRadius="19" Background="{DynamicResource Think.Control}" Padding="3">
  <Grid><Grid.ColumnDefinitions><ColumnDefinition/><ColumnDefinition/></Grid.ColumnDefinitions>
   <Border x:Name="Selection" Grid.ColumnSpan="2" HorizontalAlignment="Left" CornerRadius="16" Background="{DynamicResource Think.AccentSurface}"><Border.RenderTransform><TranslateTransform/></Border.RenderTransform></Border>
   <StackPanel Orientation="Horizontal" HorizontalAlignment="Center" VerticalAlignment="Center"><TextBlock Text="&#xE787;" FontFamily="Segoe Fluent Icons" Foreground="{DynamicResource Think.Text}" Margin="0,0,6,0"/><TextBlock Text="Evento" Foreground="{DynamicResource Think.Text}"/></StackPanel>
   <StackPanel Grid.Column="1" Orientation="Horizontal" HorizontalAlignment="Center" VerticalAlignment="Center"><TextBlock Text="&#xE70F;" FontFamily="Segoe Fluent Icons" Foreground="{DynamicResource Think.Text}" Margin="0,0,6,0"/><TextBlock Text="Nota" Foreground="{DynamicResource Think.Text}"/></StackPanel>
  </Grid>
 </Border>
 <ControlTemplate.Triggers><Trigger Property="IsKeyboardFocused" Value="True"><Setter Property="Opacity" Value="0.85"/></Trigger></ControlTemplate.Triggers>
</ControlTemplate>
""");
        void Move(bool animate)
        {
            toggle.ApplyTemplate();
            if (toggle.Template.FindName("Selection", toggle) is not Border selection) return;
            double half = Math.Max(0, (toggle.ActualWidth - 6) / 2);
            selection.Width = half;
            var motion = (TranslateTransform)selection.RenderTransform;
            if (motion.IsFrozen) { motion = motion.Clone(); selection.RenderTransform = motion; }
            double target = toggle.IsChecked == true ? half : 0;
            if (animate && SystemParameters.ClientAreaAnimation)
                motion.BeginAnimation(TranslateTransform.XProperty, new DoubleAnimation(motion.X, target, TimeSpan.FromMilliseconds(160)) { EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut } });
            else { motion.BeginAnimation(TranslateTransform.XProperty, null); motion.X = target; }
        }
        toggle.Loaded += (_, _) => Move(false); toggle.SizeChanged += (_, _) => Move(false);
        toggle.Checked += (_, _) => Move(true); toggle.Unchecked += (_, _) => Move(true);
        return toggle;
    }
}
