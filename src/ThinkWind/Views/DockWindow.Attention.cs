using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using OrbyLauncher.Services;
using OrbyLauncher.Native;

namespace OrbyLauncher.Views;

internal sealed partial class DockWindow
{
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern uint RegisterWindowMessage(string name);
    [DllImport("user32.dll")] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool RegisterShellHookWindow(nint window);
    [DllImport("user32.dll")] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool DeregisterShellHookWindow(nint window);
    private readonly HashSet<nint> attentionWindows = new();
    private readonly List<(Border Dot, Button Button, nint[] Windows, Item Item)> attentionDots = new();
    private TaskbarBadge[] nativeBadges = Array.Empty<TaskbarBadge>();
    private HwndSource? attentionSource;
    private uint attentionMessage;

    private void InitializeAttention()
    {
        SourceInitialized += (_, _) =>
        {
            var handle = new WindowInteropHelper(this).Handle;
            attentionMessage = RegisterWindowMessage("SHELLHOOK");
            if (attentionMessage == 0 || !RegisterShellHookWindow(handle)) return;
            attentionSource = HwndSource.FromHwnd(handle);
            attentionSource.AddHook(AttentionMessage);
        };
        Closed += (_, _) =>
        {
            if (attentionSource is not null)
            {
                DeregisterShellHookWindow(attentionSource.Handle);
                attentionSource.RemoveHook(AttentionMessage); attentionSource = null;
            }
            attentionDots.Clear(); attentionWindows.Clear();
        };
    }

    private nint AttentionMessage(nint hwnd, int message, nint wParam, nint lParam, ref bool handled)
    {
        if (attentionMessage == 0 || (uint)message != attentionMessage || lParam == 0) return 0;
        // Shell attention messages are delivered to this HWND. No keyboard/mouse
        // interception, injected DLL, title polling or continuous animation.
        switch ((int)wParam)
        {
            case 0x8006: // HSHELL_FLASH: the application explicitly requests attention.
                if (WindowThumbnail.IsWindow(lParam) && !Desktop.IsActiveWindow(lParam, Desktop.GetForegroundWindow()))
                    attentionWindows.Add(lParam);
                break;
            case 4: // HSHELL_WINDOWACTIVATED
            case 0x8004: // HSHELL_RUDEAPPACTIVATED
                attentionWindows.RemoveWhere(window => Desktop.IsActiveWindow(window, lParam));
                break;
            case 2: // HSHELL_WINDOWDESTROYED
                attentionWindows.Remove(lParam);
                break;
            default: return 0; // A title redraw/new browser tab alone isn't a notification.
        }
        RefreshAttentionDots();
        return 0;
    }

    private void AddAttentionDot(Grid panel, Button button, Item item)
    {
        if (item.Key.StartsWith("system:", StringComparison.Ordinal)) return;
        var handles = item.Windows.Select(window => window.Handle).ToArray();
        var active = Desktop.GetForegroundWindow();
        foreach (var handle in handles) if (Desktop.IsActiveWindow(handle, active)) attentionWindows.Remove(handle);
        var dot = new Border
        {
            Width = 7, Height = 7, CornerRadius = new CornerRadius(3.5),
            HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Top,
            RenderTransform = new TranslateTransform(0, -3), IsHitTestVisible = false,
            Visibility = handles.Any(attentionWindows.Contains) || HasUnreadMetadata(item) ? Visibility.Visible : Visibility.Hidden
        };
        dot.SetResourceReference(Border.BackgroundProperty, "Think.Accent");
        panel.Children.Add(dot);
        attentionDots.Add((dot, button, handles, item));
        SetAttentionAccessibility(button, dot.Visibility == Visibility.Visible);
    }
    private void RefreshAttentionDots()
    {
        foreach (var (dot, button, windows, item) in attentionDots)
        {
            bool pending = windows.Any(attentionWindows.Contains) || HasUnreadMetadata(item);
            dot.Visibility = pending ? Visibility.Visible : Visibility.Hidden;
            SetAttentionAccessibility(button, pending);
        }
    }
    private void RefreshUnreadMetadata()
    {
        nativeBadges = TaskbarBadges.Snapshot();
        attentionWindows.RemoveWhere(window => !WindowThumbnail.IsWindow(window));
        RefreshAttentionDots();
    }
    private bool HasUnreadMetadata(Item item)
    {
        // Counter-only titles are meaningful for messaging apps, never for an
        // arbitrary browser tab count or an ordinary redraw.
        string identity = item.Target + " " + string.Join(" ", item.Windows.Select(window => window.Executable + " " + window.AppId));
        string[] messengers = { "whatsapp", "telegram", "discord", "signal", "tretalk", "slack", "messenger", "ms-teams" };
        string? messenger = messengers.FirstOrDefault(name => identity.Contains(name, StringComparison.OrdinalIgnoreCase));
        if (messenger is not null && item.Windows.Any(window => TaskbarBadges.HasCounter(window.Title))) return true;
        var aliases = new[] { item.Name, messenger ?? "" }.Concat(item.Windows.Select(window => window.Title)).Where(name => name.Length >= 3).Distinct(StringComparer.OrdinalIgnoreCase);
        foreach (var badge in nativeBadges)
            if (aliases.Any(alias => badge.Name.Contains(alias, StringComparison.OrdinalIgnoreCase))
                && (TaskbarBadges.HasUnread(badge.Name + " " + badge.Status) || (messenger is not null && TaskbarBadges.HasCounter(badge.Name)))) return true;
        return false;
    }
    private static void SetAttentionAccessibility(Button button, bool pending) =>
        System.Windows.Automation.AutomationProperties.SetItemStatus(button, pending ? "Nova atividade no aplicativo" : "");
}
