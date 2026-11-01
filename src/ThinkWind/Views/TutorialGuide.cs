using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Media;

namespace OrbyLauncher.Views;

// Targets live in the real visual tree, including rebuilt dock/header controls.
// No global input hooks, cursor movement or persistent polling.
internal static class TutorialGuide
{
    private static readonly DependencyProperty TargetProperty = DependencyProperty.RegisterAttached(
        "Target", typeof(string), typeof(TutorialGuide), new PropertyMetadata(null));
    internal static event Action<string>? Performed;
    internal static bool Active { get; set; }
    internal static bool KeepDockOpen { get; set; }
    internal static bool KeepIslandOpen { get; set; }
    internal static void Mark(FrameworkElement element, string key) => element.SetValue(TargetProperty, key);
    internal static void Notify(string action) { if (Active) Performed?.Invoke(action); }
    internal static FrameworkElement? Find(string key)
    {
        foreach (Window window in Application.Current.Windows.Cast<Window>().ToArray())
        {
            if (!window.IsVisible || window is TutorialWindow) continue;
            var found = FindChild(window, key);
            if (found is not null) return found;
        }
        return null;
    }
    private static FrameworkElement? FindChild(DependencyObject node, string key)
    {
        if (node is FrameworkElement element && (!element.IsVisible || element.ActualWidth <= 0 || element.ActualHeight <= 0)) return null;
        if (node is FrameworkElement target && (string?)target.GetValue(TargetProperty) == key) return target;
        int count = VisualTreeHelper.GetChildrenCount(node);
        for (int i = 0; i < count; i++)
        {
            var result = FindChild(VisualTreeHelper.GetChild(node, i), key);
            if (result is not null) return result;
        }
        return null;
    }
}
