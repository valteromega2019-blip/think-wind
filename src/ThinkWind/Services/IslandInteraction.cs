namespace ThinkWind.Services;

internal enum IslandStage { Compact, Hover, Full }
// Pure transition rules: popup lifetime never promotes the main island to Full.
internal sealed class IslandInteraction
{
    internal IslandStage Stage { get; private set; }
    internal IslandStage BeforeFull { get; private set; }
    internal bool MiniOpen { get; private set; }
    internal void Enter() { if (!MiniOpen && Stage == IslandStage.Compact) Stage = IslandStage.Hover; }
    internal void Leave() { if (!MiniOpen && Stage == IslandStage.Hover) Stage = IslandStage.Compact; }
    internal void OpenFull()
    {
        if (Stage == IslandStage.Full) return;
        BeforeFull = Stage; Stage = IslandStage.Full;
    }
    internal void CloseFull() { if (Stage == IslandStage.Full) Stage = BeforeFull; }
    internal void Compact() { Stage = IslandStage.Compact; }
    internal void OpenMini() { MiniOpen = true; }
    internal void CloseMini() { MiniOpen = false; }
}
