using System;
using System.Collections.Generic;
using System.Linq;

namespace ThinkWind.Services;

internal sealed record QuickControl(string Id, string Name, string Glyph, string? Target);
internal static class LayoutCatalog
{
    internal static readonly QuickControl[] QuickControls =
    {
        new("clock", "Relógio", "\uE917", null),
        new("wifi", "Wi-Fi", "\uE701", "ms-settings:network-wifi"),
        new("bluetooth", "Bluetooth", "\uE702", "ms-settings:bluetooth"),
        new("audio", "Áudio", "\uE767", null),
        new("tray", "Bandeja de aplicativos", "\uE712", null),
        new("notifications", "Notificações", "\uE7F4", "ms-settings:notifications"),
        new("calendar", "Calendário", "\uE787", null),
        new("airplane", "Modo avião", "\uE709", "ms-settings:network-airplanemode"),
        new("settings", "Personalizar", "\uE713", null)
    };
    internal static readonly string[] Modules = { "connections", "media", "audio", "metrics", "calendar", "background", "notifications" };
    internal static string ModuleName(string id) => id switch
    { "connections" => "Conexões", "media" => "Reprodução de mídia", "audio" => "Saída e microfone", "metrics" => "Monitoramento", "calendar" => "Calendário", "background" => "Aplicativos em segundo plano", "notifications" => "Notificações", _ => id };
    internal static List<string> Complete(IEnumerable<string>? order, IEnumerable<string> known) =>
        (order ?? Array.Empty<string>()).Where(known.Contains).Concat(known).Distinct(StringComparer.Ordinal).ToList();
    internal static void Move(List<string> order, string source, string target, bool after)
    {
        if (source == target || !order.Contains(source) || !order.Contains(target)) return;
        order.Remove(source);
        order.Insert(order.IndexOf(target) + (after ? 1 : 0), source);
    }
}
