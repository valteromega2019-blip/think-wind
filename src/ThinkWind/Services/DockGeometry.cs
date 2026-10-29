using System;

namespace ThinkWind.Services;
internal static class DockGeometry
{
    internal static double StartWidth(double icon) => Math.Max(40, icon + 12);
    // Reserve from the measured button row, including indicators/template padding
    // and the spring's allowed overshoot, rather than a nominal icon-size guess.
    internal static double Headroom(double measuredHeight, double amount) => Math.Ceiling(measuredHeight * amount * 1.08) + 12;
}
