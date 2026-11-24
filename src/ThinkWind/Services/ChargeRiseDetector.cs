using System;

namespace OrbyLauncher.Services;

// A heuristic, not a device charging flag. Requires a baseline and two high samples.
// State is small, in-memory only, and expires so a missed unplug cannot latch forever.
internal sealed class ChargeRiseDetector(int rise, int fall)
{
    private int previous, baseline, peak;
    private long previousAt, candidateAt, inferredAt;
    private bool initialized, candidate, inferred;
    internal int Baseline => baseline;
    internal bool HoldBaseline => candidate || inferred;
    internal bool Update(int value, long now)
    {
        if (!initialized || now - previousAt > 120000 || now < previousAt)
        {
            initialized = true; candidate = inferred = false;
            previous = baseline = peak = value; previousAt = now; return false;
        }
        if (inferred)
        {
            peak = Math.Max(peak, value);
            if (peak - value >= fall || now - inferredAt >= 600000)
            { inferred = false; candidate = false; baseline = value; }
        }
        else if (candidate)
        {
            if (now - candidateAt > 120000 || value - baseline < rise)
                candidate = false;
            else
            { inferred = true; inferredAt = now; peak = value; candidate = false; }
        }
        else if (value - previous >= rise)
        { baseline = previous; candidateAt = now; candidate = true; }
        previous = value; previousAt = now;
        return inferred;
    }
}
