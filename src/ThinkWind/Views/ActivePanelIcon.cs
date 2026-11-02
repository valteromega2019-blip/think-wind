using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace OrbyLauncher.Views;

internal static class ActivePanelIcon
{
    internal static readonly DependencyProperty IdProperty = DependencyProperty.RegisterAttached("Id", typeof(string), typeof(ActivePanelIcon));
    private static readonly DependencyProperty ActiveProperty = DependencyProperty.RegisterAttached("Active", typeof(bool), typeof(ActivePanelIcon), new PropertyMetadata(false));
    private static readonly DependencyProperty BrushProperty = DependencyProperty.RegisterAttached("AccentBrush", typeof(Brush), typeof(ActivePanelIcon));
    internal static bool IsActive(Button button) => (bool)button.GetValue(ActiveProperty);
    internal static void Update(Button button, bool active, bool force = false)
    {
        var brush = active ? button.FindResource("Think.Accent") as Brush : null;
        if (!force && IsActive(button) == active && ReferenceEquals(button.GetValue(BrushProperty), brush)) return;
        button.SetValue(ActiveProperty, active);
        button.SetValue(BrushProperty, brush);
        var animation = active ? new ObjectAnimationUsingKeyFrames { Duration = TimeSpan.Zero, FillBehavior = FillBehavior.HoldEnd } : null;
        if (animation is not null) animation.KeyFrames.Add(new DiscreteObjectKeyFrame(brush, KeyTime.FromTimeSpan(TimeSpan.Zero)));
        // Animation overlays preserve the original resources/bindings below.
        button.BeginAnimation(Control.ForegroundProperty, animation);
        if (button.Content is DependencyObject content) Paint(content, animation);
    }
    private static void Paint(DependencyObject element, ObjectAnimationUsingKeyFrames? animation)
    {
        if (element is TextBlock text) text.BeginAnimation(TextBlock.ForegroundProperty, animation);
        foreach (object child in LogicalTreeHelper.GetChildren(element)) if (child is DependencyObject node) Paint(node, animation);
    }
}
