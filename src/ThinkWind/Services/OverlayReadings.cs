using System;
using System.Collections.Generic;
using System.Linq;

namespace ThinkWind.Services;

internal static class OverlayReadings
{
    internal static string Value(HardwareSample sample, string key)
    {
        double? extra = sample.Extra?.GetValueOrDefault(key);
        string N(double? value, string suffix, string format = "0") => value is { } number && double.IsFinite(number) ? number.ToString(format) + suffix : "—";
        return key switch
        {
            "fps" => N(sample.Frames?.Fps, " FPS"), "frametime" => N(sample.Frames?.FrameTimeMs, " ms", "0.0"),
            "low1" => N(sample.Frames?.Low1Fps, " FPS"),
            "cpu_load" => N(sample.Cpu.Load, "%"), "cpu_temp" => N(sample.Cpu.Temperature, " °C"),
            "cpu_power" => N(sample.Cpu.Power, " W"), "cpu_clock" => N(sample.Cpu.Clock / 1000, " GHz", "0.00"),
            "cpu_maxcore" => "núcleo " + N(extra, "%"), "cpu_clockmax" => "pico " + N(extra / 1000, " GHz", "0.00"),
            "gpu_load" => N(sample.Gpu.Load, "%"), "gpu_temp" => N(sample.Gpu.Temperature, " °C"),
            "gpu_power" => N(sample.Gpu.Power, " W"), "gpu_clock" => N(sample.Gpu.Clock, " MHz"),
            "gpu_hotspot" => "HS " + N(extra, " °C"), "gpu_memclock" => "mem " + N(extra, " MHz"),
            "gpu_volt" => N(extra, " V", "0.000"), "gpu_fan" => N(extra, " RPM"), "gpu_perfcap" => "PerfCap —",
            "vram_used" => N(sample.Gpu.MemoryUsed, " GB", "0.0"),
            "vram_pct" => N(sample.Gpu.MemoryTotal > 0 ? sample.Gpu.MemoryUsed / sample.Gpu.MemoryTotal * 100 : null, "%"),
            "vram_temp" => N(extra, " °C"), "ram_used" => N(sample.RamUsed, " GB", "0.0"),
            "ram_pct" => N(sample.RamTotal > 0 ? sample.RamUsed / sample.RamTotal * 100 : null, "%"),
            "game" => sample.Frames?.ProcessName ?? "Aguardando aplicativo", "clock" => sample.Time.ToString("HH:mm"), _ => "—"
        };
    }
    internal static IEnumerable<(string Group, string Value)> Rows(HardwareSample sample, IReadOnlyCollection<string> selected)
        => OverlayMetrics.All.Where(m => selected.Contains(m.Key) && m.Group is "CPU" or "GPU" or "VRAM" or "RAM")
            .GroupBy(m => m.Group).Select(group => (group.Key, string.Join(" · ", group.Select(m => Value(sample, m.Key)))));
}
