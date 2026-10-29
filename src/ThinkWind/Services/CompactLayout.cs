using System;

namespace ThinkWind.Services;
internal sealed record DockFit(int PerPage, double Icon, double Padding, double Gap, double NavigationWidth)
{
    internal int Pages(int count) => Math.Max(1, (int)Math.Ceiling(count / (double)Math.Max(1, PerPage)));
}
internal static class CompactLayout
{
    internal static DockFit FitDock(int count, double available, double icon, double gap)
    {
        count = Math.Max(1, count); available = Math.Max(60, available);
        double requested = icon + 20 + gap;
        double factor = Math.Min(1, available / (count * requested));
        if (icon * factor >= 18) return new(count, icon * factor, 10 * factor, gap * factor, 0);
        double navigation = Math.Min(88, available * .35);
        double smallGap = Math.Min(4, gap);
        int perPage = Math.Max(1, (int)Math.Floor((available - navigation) / (18 + 10 + smallGap)));
        double fittedIcon = Math.Clamp((available - navigation) / perPage - 10 - smallGap, 18, Math.Max(18, icon));
        return new(perPage, fittedIcon, 5, smallGap, navigation);
    }
    internal static double PanelHeight(double workHeight) => Math.Max(120, Math.Min(520, workHeight * .64));
}
