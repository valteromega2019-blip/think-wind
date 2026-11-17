using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace OrbyLauncher.Services;

public sealed record AppIconOverride(string[] Identities, string Image);

internal static class CustomAppIcons
{
    internal const int MaximumEncodedSize = 128 * 1024;
    private const int MaximumTotalSize = 1024 * 1024;
    internal static string[] Keys(string target, string? appId)
    {
        var keys = new List<string>();
        if (target.StartsWith("ms-settings:", StringComparison.OrdinalIgnoreCase)
            || target.EndsWith("SystemSettings.exe", StringComparison.OrdinalIgnoreCase)
            || appId?.Contains("windows.immersivecontrolpanel", StringComparison.OrdinalIgnoreCase) == true)
            keys.Add("app:orby-windows-settings");
        if (!string.IsNullOrWhiteSpace(appId)) keys.Add("app:" + appId.Trim().ToLowerInvariant());
        if (!string.IsNullOrWhiteSpace(target)) keys.Add("target:" + target.Trim().Replace('/', '\\').TrimEnd('\\').ToLowerInvariant());
        return keys.Where(key => key.Length <= 2048).Distinct(StringComparer.Ordinal).ToArray();
    }
    internal static List<AppIconOverride> Normalize(List<AppIconOverride>? entries)
    {
        var result = new List<AppIconOverride>(); int total = 0;
        foreach (var entry in entries ?? new())
        {
            if (entry?.Identities is null || entry.Image is not { Length: >= 64 and <= MaximumEncodedSize }
                || !entry.Image.StartsWith("iVBORw0KGgo", StringComparison.Ordinal)) continue;
            var keys = entry.Identities.Where(key => !string.IsNullOrWhiteSpace(key) && key.Length <= 2048
                && (key.StartsWith("app:", StringComparison.Ordinal) || key.StartsWith("target:", StringComparison.Ordinal)))
                .Select(key => key.ToLowerInvariant()).Distinct(StringComparer.Ordinal).Take(4).ToArray();
            if (keys.Length == 0 || total + entry.Image.Length > MaximumTotalSize) continue;
            result.Add(new(keys, entry.Image)); total += entry.Image.Length;
            if (result.Count == 64) break;
        }
        return result;
    }
    internal static bool Has(Preferences preferences, string target, string? appId) =>
        preferences.CustomAppIcons.Any(entry => entry.Identities.Intersect(Keys(target, appId), StringComparer.Ordinal).Any());
    internal static void Set(SettingsStore store, string target, string? appId, string image)
    {
        var keys = Keys(target, appId);
        if (keys.Length == 0) throw new InvalidOperationException("Não foi possível identificar este aplicativo.");
        var existing = store.Value.CustomAppIcons.Where(entry => entry.Identities.Intersect(keys, StringComparer.Ordinal).Any()).ToArray();
        keys = keys.Concat(existing.SelectMany(entry => entry.Identities)).Distinct(StringComparer.Ordinal).Take(4).ToArray();
        var remaining = store.Value.CustomAppIcons.Except(existing).ToList();
        if (remaining.Count >= 64 || remaining.Sum(entry => entry.Image.Length) + image.Length > MaximumTotalSize)
            throw new InvalidOperationException("O limite de ícones personalizados foi atingido. Restaure um ícone antes de adicionar outro.");
        remaining.Add(new(keys, image));
        store.Update(preferences => preferences.CustomAppIcons = remaining);
    }
    internal static void Restore(SettingsStore store, string target, string? appId)
    {
        var keys = Keys(target, appId);
        store.Update(preferences => preferences.CustomAppIcons.RemoveAll(entry => entry.Identities.Intersect(keys, StringComparer.Ordinal).Any()));
    }
    internal static string Import(string path)
    {
        using var file = File.Open(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        if (file.Length is < 8 or > 2 * 1024 * 1024) throw new InvalidDataException("Escolha um PNG ou ICO de até 2 MB, com fundo transparente.");
        byte[] bytes = new byte[checked((int)file.Length)]; file.ReadExactly(bytes);
        bool png = bytes.Length >= 24 && bytes[0] == 137 && bytes[1] == 80 && bytes[2] == 78 && bytes[3] == 71;
        bool ico = bytes[0] == 0 && bytes[1] == 0 && bytes[2] == 1 && bytes[3] == 0;
        if (!png && !ico) throw new InvalidDataException("O arquivo precisa ser um PNG ou ICO válido.");
        if (ico && System.Buffers.Binary.BinaryPrimitives.ReadUInt16LittleEndian(bytes.AsSpan(4, 2)) is < 1 or > 64)
            throw new InvalidDataException("Arquivo ICO inválido ou com imagens demais.");
        if (png)
        {
            uint pngWidth = System.Buffers.Binary.BinaryPrimitives.ReadUInt32BigEndian(bytes.AsSpan(16, 4));
            uint pngHeight = System.Buffers.Binary.BinaryPrimitives.ReadUInt32BigEndian(bytes.AsSpan(20, 4));
            if (pngWidth is 0 or > 1024 || pngHeight is 0 or > 1024) throw new InvalidDataException("Escolha um símbolo de até 1024 × 1024 pixels.");
        }
        using var input = new MemoryStream(bytes, writable: false);
        var decoder = BitmapDecoder.Create(input, BitmapCreateOptions.PreservePixelFormat | BitmapCreateOptions.DelayCreation, BitmapCacheOption.OnDemand);
        if (decoder.Frames.Count is < 1 or > 64) throw new InvalidDataException("Arquivo de ícone inválido.");
        var frame = decoder.Frames.Where(frame => frame.PixelWidth is > 0 and <= 1024 && frame.PixelHeight is > 0 and <= 1024)
            .OrderByDescending(frame => (long)frame.PixelWidth * frame.PixelHeight).FirstOrDefault()
            ?? throw new InvalidDataException("Escolha um símbolo com dimensões de até 1024 × 1024 pixels.");
        var source = new FormatConvertedBitmap(frame, PixelFormats.Bgra32, null, 0);
        int width = source.PixelWidth, height = source.PixelHeight;
        byte[] pixels = new byte[width * height * 4]; source.CopyPixels(pixels, width * 4, 0);
        int left = width, top = height, right = -1, bottom = -1; bool transparent = false;
        for (int y = 0; y < height; y++) for (int x = 0; x < width; x++)
        {
            byte alpha = pixels[(y * width + x) * 4 + 3];
            transparent |= alpha < 255;
            if (alpha > 4) { left = Math.Min(left, x); top = Math.Min(top, y); right = Math.Max(right, x); bottom = Math.Max(bottom, y); }
        }
        if (right < left) throw new InvalidDataException("O arquivo está vazio ou totalmente transparente.");
        if (!transparent) throw new InvalidDataException("Use um PNG ou ICO com fundo transparente, para que só o símbolo apareça sobre a cor de destaque.");
        BitmapSource cropped = new CroppedBitmap(source, new System.Windows.Int32Rect(left, top, right - left + 1, bottom - top + 1));
        double scale = Math.Min(1, 256d / Math.Max(cropped.PixelWidth, cropped.PixelHeight));
        if (scale < 1) cropped = new TransformedBitmap(cropped, new ScaleTransform(scale, scale));
        width = cropped.PixelWidth; height = cropped.PixelHeight; pixels = new byte[width * height * 4]; cropped.CopyPixels(pixels, width * 4, 0);
        for (int index = 0; index < pixels.Length; index += 4) pixels[index] = pixels[index + 1] = pixels[index + 2] = 255;
        var normalized = BitmapSource.Create(width, height, 96, 96, PixelFormats.Bgra32, null, pixels, width * 4);
        var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(normalized));
        using var output = new MemoryStream(); encoder.Save(output);
        string encoded = Convert.ToBase64String(output.ToArray());
        if (encoded.Length > MaximumEncodedSize) throw new InvalidDataException("O símbolo é complexo demais. Use uma imagem menor.");
        return encoded;
    }
    internal static BitmapSource? Decode(string encoded)
    {
        try
        {
            if (encoded.Length > MaximumEncodedSize) return null;
            byte[] bytes = Convert.FromBase64String(encoded);
            if (bytes.Length < 24 || bytes[0] != 137 || bytes[1] != 80 || bytes[2] != 78 || bytes[3] != 71) return null;
            uint width = System.Buffers.Binary.BinaryPrimitives.ReadUInt32BigEndian(bytes.AsSpan(16, 4));
            uint height = System.Buffers.Binary.BinaryPrimitives.ReadUInt32BigEndian(bytes.AsSpan(20, 4));
            if (width is 0 or > 256 || height is 0 or > 256) return null;
            using var stream = new MemoryStream(bytes, writable: false);
            var image = new PngBitmapDecoder(stream, BitmapCreateOptions.None, BitmapCacheOption.OnLoad).Frames[0];
            image.Freeze(); return image;
        }
        catch (Exception error) when (error is FormatException or ArgumentException or IOException or InvalidOperationException or NotSupportedException) { return null; }
    }
}
