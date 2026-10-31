// Adapted from the user-owned Think Monitor source; runs independently in Think Wind.
using System.Collections.Generic;

namespace OrbyLauncher.Services;

public sealed record FrameStats(int ProcessId, string ProcessName, double Fps, double FrameTimeMs, double Low1Fps, IReadOnlyList<double> RecentFrameTimes);
