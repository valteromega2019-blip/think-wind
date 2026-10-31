using System;
using System.Windows.Media;

namespace ThinkWind.Views;
// Follow WPF's presentation cadence instead of competing with a dispatcher timer.
internal sealed class FrameClock
{
    internal bool IsRunning { get; private set; }
    internal event EventHandler? Tick;
    private TimeSpan? previousFrame;
    internal void Start() { if (IsRunning) return; previousFrame = null; IsRunning = true; CompositionTarget.Rendering += Rendering; }
    internal void Stop() { if (!IsRunning) return; IsRunning = false; CompositionTarget.Rendering -= Rendering; }
    private void Rendering(object? sender, EventArgs args)
    {
        if (args is RenderingEventArgs rendering)
        {
            if (previousFrame == rendering.RenderingTime) return;
            previousFrame = rendering.RenderingTime;
        }
        Tick?.Invoke(this, args);
    }
}
