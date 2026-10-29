using System;
using System.Windows.Media;

namespace ThinkWind.Views;
// Follow WPF's presentation cadence instead of competing with a dispatcher timer.
internal sealed class FrameClock
{
    internal bool IsRunning { get; private set; }
    internal event EventHandler? Tick;
    internal void Start() { if (IsRunning) return; IsRunning = true; CompositionTarget.Rendering += Rendering; }
    internal void Stop() { if (!IsRunning) return; IsRunning = false; CompositionTarget.Rendering -= Rendering; }
    private void Rendering(object? sender, EventArgs args) => Tick?.Invoke(this, args);
}
