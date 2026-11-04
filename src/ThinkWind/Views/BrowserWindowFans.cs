using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using OrbyLauncher.Services;

namespace OrbyLauncher.Views;

// Each real browser window owns one independent stack of its tabs.
internal sealed class BrowserWindowFans : StackPanel, IDisposable
{
    private readonly List<BrowserTabFan> groups = new();
    internal event Action? DismissRequested;
    internal event Action? LayoutChanged;
    internal event Action<nint, FrameworkElement>? WindowHovered;
    internal event Action? WindowHoverEnded;
    internal bool IsInteracting => groups.Any(group => group.IsInteracting);
    internal BrowserWindowFans() { Orientation = Orientation.Horizontal; Width = 320; }
    internal void Present(IReadOnlyList<BrowserTab> tabs, nint owner, Preferences preferences)
    {
        Dispose(); Children.Clear(); groups.Clear();
        foreach (var window in tabs.GroupBy(tab => tab.Window).Take(6))
        {
            var fan = new BrowserTabFan { VerticalAlignment = VerticalAlignment.Bottom };
            fan.DismissRequested += () => DismissRequested?.Invoke();
            fan.WindowHovered += (window, card) => WindowHovered?.Invoke(window, card);
            fan.WindowHoverEnded += () => WindowHoverEnded?.Invoke();
            fan.LayoutChanged += () => { UpdateSize(); LayoutChanged?.Invoke(); };
            fan.Present(window.ToArray(), owner, preferences);
            groups.Add(fan); Children.Add(fan);
        }
        UpdateSize();
    }
    private void UpdateSize()
    {
        Width = Math.Max(320, groups.Sum(group => group.Width));
        Height = groups.Count == 0 ? 184 : groups.Max(group => group.Height);
    }
    internal void AttachPanelMaterial(FrameworkElement panel) => groups.FirstOrDefault()?.AttachPanelMaterial(panel);
    internal void ConfigureAppearance(Preferences preferences)
    { foreach (var group in groups) group.ConfigureAppearance(preferences); }
    internal void PlaceImages() { foreach (var group in groups) group.PlaceImages(); }
    public void Dispose() { foreach (var group in groups) group.Dispose(); Children.Clear(); groups.Clear(); }
}
