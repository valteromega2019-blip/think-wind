using System;

namespace ThinkWind.Services;
internal sealed record AppDrawerLayout(double Width, double Height, int Columns, int Rows)
{
    internal const double CellHeight = 128;
    internal int Capacity => Columns * Rows;
    internal static AppDrawerLayout Fit(double displayWidth, double availableHeight)
    {
        double width = Math.Max(48, Math.Min(720, displayWidth - 16));
        double height = Math.Max(48, Math.Min(660, availableHeight));
        int columns = Math.Clamp((int)((width - 28) / 112), 1, 6);
        int rows = Math.Clamp((int)((height - 340) / CellHeight), 1, 3);
        return new(width, height, columns, rows);
    }
}
