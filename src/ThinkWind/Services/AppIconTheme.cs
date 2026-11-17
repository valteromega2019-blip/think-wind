using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace OrbyLauncher.Services;

// Presentation only: never modifies shell resources, associations or identity.
internal static class AppIconTheme
{
    private sealed record Symbol(string? Path = null, string? Glyph = null, string? Letters = null);
    private static readonly Lazy<Dictionary<string, string>> paths = new(LoadPaths);
    private static readonly Dictionary<string, Symbol> resolved = new(StringComparer.Ordinal);
    private static readonly Dictionary<string, Geometry> geometry = new(StringComparer.Ordinal);
    private static readonly (string Match, string Icon)[] aliases = {
        ("creativecloud", "adobecreativecloud"), ("chatgpt", "openai"),
        ("hextothink", "hex2think"), ("hex2think", "hex2think"), ("hexthink", "hex2think"),
        ("tretalk", "tretalk"), ("tretaalk", "tretalk"), ("vscode", "visualstudiocode"),
        ("visualstudiocode", "visualstudiocode"), ("codeinsiders", "visualstudiocode"), ("codeexe", "visualstudiocode"),
        ("youtubemusic", "youtubemusic"), ("googlechrome", "googlechrome"), ("chromeexe", "googlechrome"),
        ("firefox", "firefox"), ("brave", "brave"), ("msedge", "microsoftedge"), ("microsoftedge", "microsoftedge"),
        ("whatsapp", "whatsapp"), ("spotify", "spotify"), ("discord", "discord"), ("telegram", "telegram"),
        ("obsstudio", "obsstudio"), ("obs64", "obsstudio"), ("epicgames", "epicgames"), ("steam", "steam"),
        ("autocad", "autocad"), ("revit", "revit"), ("archicad", "archicad"), ("sketchup", "sketchup"),
        ("rhinoceros", "rhinoceros"), ("rhino", "rhinoceros"), ("3dsmax", "3dsmax"), ("cinema4d", "cinema4d"),
        ("photoshop", "adobephotoshop"), ("illustrator", "adobeillustrator"), ("indesign", "adobeindesign"),
        ("aftereffects", "adobeaftereffects"), ("premiere", "adobepremierepro"), ("lightroom", "adobelightroom"),
        ("acrobat", "adobeacrobatreader"), ("audition", "adobeaudition"), ("coreldraw", "coreldraw"),
        ("affinitydesigner", "affinitydesigner"), ("affinityphoto", "affinityphoto"), ("affinitypublisher", "affinitypublisher"),
        ("blender", "blender"), ("davinciresolve", "davinciresolve"), ("figma", "figma"), ("inkscape", "inkscape"),
        ("gimp", "gimp"), ("krita", "krita"), ("canva", "canva"), ("notion", "notion"), ("slack", "slack"),
        ("androidstudio", "androidstudio"), ("intellij", "intellijidea"), ("pycharm", "pycharm"),
        ("webstorm", "webstorm"), ("rider", "rider"), ("sublimetext", "sublimetext"), ("postman", "postman"),
        ("docker", "docker"), ("gitkraken", "gitkraken"), ("githubdesktop", "github"), ("gitexe", "git"),
        ("unity", "unity"), ("unrealengine", "unrealengine"), ("godot", "godotengine"), ("vlc", "vlcmediaplayer"),
        ("7zip", "7zip"), ("winrar", "winrar"), ("dropbox", "dropbox"), ("zoom", "zoom"), ("trello", "trello")
    };
    private static readonly Dictionary<string, string> monograms = new() {
        ["adobephotoshop"]="Ps", ["adobeillustrator"]="Ai", ["adobeindesign"]="Id", ["adobeaftereffects"]="Ae",
        ["adobepremierepro"]="Pr", ["adobelightroom"]="Lr", ["adobeacrobatreader"]="Ac", ["adobeaudition"]="Au",
        ["coreldraw"]="Cd", ["affinitydesigner"]="Ad", ["affinityphoto"]="Ap", ["affinitypublisher"]="Ap",
        ["autocad"]="CAD", ["revit"]="Rv", ["archicad"]="Ar", ["sketchup"]="Sk", ["3dsmax"]="3ds",
        ["cinema4d"]="C4D", ["visualstudiocode"]="VS", ["microsoftedge"]="e", ["winrar"]="RAR"
    };
    internal static bool Enabled { get; private set; }
    internal static bool WithoutBackground { get; private set; }
    private static Color accent;
    private static SolidColorBrush tile = Brushes.Transparent;
    private static Dictionary<string, string> custom = new(StringComparer.Ordinal);
    private static HashSet<string> foregroundApp = new(StringComparer.Ordinal);
    internal static void UpdateForegroundApp(string target, string? appId)
    {
        var next = CustomAppIcons.Keys(target, appId).ToHashSet(StringComparer.Ordinal);
        if (foregroundApp.SetEquals(next)) return;
        foregroundApp = next;
        if (WithoutBackground) Changed?.Invoke();
    }
    private static readonly Dictionary<string, BitmapSource?> customImages = new(StringComparer.Ordinal);
    internal static event Action? Changed;
    internal static void Apply(Preferences preferences)
    {
        var next = (Color)ColorConverter.ConvertFromString(preferences.AccentColor);
        var nextCustom = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var entry in preferences.CustomAppIcons)
            foreach (string key in entry.Identities) nextCustom[key] = entry.Image;
        bool customChanged = nextCustom.Count != custom.Count || nextCustom.Any(pair => !custom.TryGetValue(pair.Key, out var image) || image != pair.Value);
        if (Enabled == preferences.UnifiedAppIcons && WithoutBackground == preferences.AppIconsWithoutBackground && next == accent && !customChanged) return;
        if (customChanged) { custom = nextCustom; customImages.Clear(); }
        Enabled = preferences.UnifiedAppIcons; WithoutBackground = preferences.AppIconsWithoutBackground; accent = next;
        tile = new SolidColorBrush(next); tile.Freeze(); Changed?.Invoke();
    }
    internal static bool HasCustom(string target, string? appId) => custom.Count > 0
        && CustomAppIcons.Keys(target, appId).Any(custom.ContainsKey);
    private static BitmapSource? CustomImage(string target, string? appId)
    {
        if (custom.Count == 0) return null;
        foreach (string key in CustomAppIcons.Keys(target, appId))
            if (custom.TryGetValue(key, out var encoded))
            {
                if (!customImages.TryGetValue(encoded, out var image))
                {
                    image = CustomAppIcons.Decode(encoded);
                    if (customImages.Count >= 64) customImages.Clear(); customImages[encoded] = image;
                }
                return image;
            }
        return null;
    }
    internal static void Draw(DrawingContext dc, Size size, string name, string target, string? appId)
    {
        double side = Math.Min(size.Width, size.Height);
        if (!double.IsFinite(side) || side <= 0) return;
        var bounds = new Rect((size.Width - side) / 2, (size.Height - side) / 2, side, side);
        if (!WithoutBackground) dc.DrawRoundedRectangle(tile, null, bounds, side * .23, side * .23);
        bool isForeground = WithoutBackground && CustomAppIcons.Keys(target, appId).Any(foregroundApp.Contains);
        Brush ink = isForeground ? tile : Brushes.White;
        double inset = side * (WithoutBackground ? .08 : .2);
        var content = new Rect(bounds.X + inset, bounds.Y + inset, side - 2 * inset, side - 2 * inset);
        if (CustomImage(target, appId) is BitmapSource uploaded)
        {
            double imageScale = Math.Min(content.Width / uploaded.PixelWidth, content.Height / uploaded.PixelHeight);
            double width = uploaded.PixelWidth * imageScale, height = uploaded.PixelHeight * imageScale;
            var area = new Rect(content.X + (content.Width - width) / 2, content.Y + (content.Height - height) / 2, width, height);
            dc.PushOpacityMask(new ImageBrush(uploaded) { Stretch = Stretch.Fill });
            dc.DrawRectangle(ink, null, area); dc.Pop(); return;
        }
        var symbol = Resolve(name, target, appId);
        if (symbol.Path is string path)
        {
            if (!geometry.TryGetValue(path, out var shape))
            {
                try
                {
                    bool evenOdd = path.StartsWith("F0 ", StringComparison.Ordinal);
                    shape = Geometry.Parse(evenOdd ? path : "F1 " + path);
                    // Discard cancelled outer contours before measuring Adobe letters.
                    // This is cached; no outline work is repeated on each frame.
                    if (evenOdd) shape = shape.GetOutlinedPathGeometry();
                    shape.Freeze();
                }
                catch (Exception error) when (error is FormatException or ArgumentException or InvalidOperationException or OverflowException)
                { DrawText(dc, content, Initials(name), false, ink); return; }
                if (geometry.Count >= 256) geometry.Clear(); geometry[path] = shape;
            }
            var original = shape.Bounds;
            if (original.Width <= 0 || original.Height <= 0) return;
            double scale = Math.Min(content.Width / original.Width, content.Height / original.Height);
            dc.PushTransform(new MatrixTransform(scale, 0, 0, scale,
                content.X + (content.Width - original.Width * scale) / 2 - original.X * scale,
                content.Y + (content.Height - original.Height * scale) / 2 - original.Y * scale));
            dc.DrawGeometry(ink, null, shape); dc.Pop();
        }
        else DrawText(dc, content, symbol.Glyph ?? symbol.Letters ?? Initials(name), symbol.Glyph is not null, ink);
    }
    private static void DrawText(DrawingContext dc, Rect box, string value, bool glyph, Brush ink)
    {
        var typeface = glyph ? new Typeface("Segoe Fluent Icons, Segoe MDL2 Assets")
            : new Typeface(new FontFamily("Segoe UI Variable, Segoe UI"), FontStyles.Normal, FontWeights.SemiBold, FontStretches.Normal);
        var text = new FormattedText(value, CultureInfo.InvariantCulture, FlowDirection.LeftToRight,
            typeface, box.Height * (glyph ? .95 : value.Length > 2 ? .55 : .78), ink, 1);
        dc.DrawText(text, new Point(box.X + (box.Width - text.Width) / 2, box.Y + (box.Height - text.Height) / 2));
    }
    private static Symbol Resolve(string name, string target, string? appId)
    {
        string key = name + "|" + target + "|" + appId;
        if (resolved.TryGetValue(key, out var cached)) return cached;
        string fileName = target;
        try { fileName = Path.GetFileName(target); } catch (ArgumentException) { }
        // Do not match parent folders (e.g. every game under Steam\common).
        string title = Normalize(name), identity = Normalize(fileName + " " + appId);
        Symbol result;
        if (identity.Contains("mssettings") || identity.Contains("immersivecontrolpanel") || identity.Contains("systemsettingsexe")
            || title is "configuracoes" or "settings" or "configuracoesdowindows" or "paineldecontrole" or "controlpanel") result = new(Glyph: "\uE713");
        else if (identity.Contains("explorerexe") || title is "explorador" or "exploradordearquivos" or "fileexplorer" or "pasta" or "pastas"
            or "documentos" or "documents" or "downloads" or "imagens" or "pictures" or "musicas" or "music" or "videos" or "areadetrabalho" or "desktop") result = new(Glyph: "\uE8B7");
        else if (identity is "calcexe" or "calculatorexe" || title is "calculadora" or "calculator") result = new(Glyph: "\uE8EF");
        else if (identity.Contains("msedge") || title is "microsoftedge" or "edge") result = new(Glyph: "\uE774");
        else if (identity.Contains("notepad") || title is "blocodenotas") result = new(Glyph: "\uE70B");
        else if (identity.Contains("powershell") || identity.Contains("windowsterminal") || identity is "cmdexe" or "wtexe") result = new(Glyph: "\uE756");
        else
        {
            string? icon = aliases.FirstOrDefault(entry => title.Contains(entry.Match, StringComparison.Ordinal)
                || identity.Contains(entry.Match, StringComparison.Ordinal)).Icon;
            string file = ""; try { file = Normalize(Path.GetFileNameWithoutExtension(target)); } catch (ArgumentException) { }
            if (icon is not null && paths.Value.TryGetValue(icon, out var brand)) result = new(Path: brand);
            else if (paths.Value.TryGetValue(title, out var exact) || paths.Value.TryGetValue(file, out exact)) result = new(Path: exact);
            else if (IsFolder(target)) result = new(Glyph: "\uE8B7");
            else if (icon is not null && monograms.TryGetValue(icon, out var letters)) result = new(Letters: letters);
            else result = new(Letters: Initials(name));
        }
        if (resolved.Count >= 512) resolved.Clear(); resolved[key] = result; return result;
    }
    private static string Initials(string name)
    {
        var words = name.Split(new[] { ' ', '-', '_', '.' }, StringSplitOptions.RemoveEmptyEntries);
        string result = string.Concat(words.Take(2).Select(word => StringInfo.GetNextTextElement(word))).ToUpperInvariant();
        return result.Length == 0 ? "?" : result;
    }
    private static bool IsFolder(string target)
    {
        // Shape hint only, no filesystem/UNC queries on the rendering thread.
        try { return System.IO.Path.IsPathFullyQualified(target) && System.IO.Path.GetExtension(target).Length == 0; }
        catch (ArgumentException) { return false; }
    }
    private static string Normalize(string value)
    {
        var text = new StringBuilder();
        string normalized;
        try { normalized = value.Normalize(NormalizationForm.FormD); } catch (ArgumentException) { normalized = value; }
        foreach (char character in normalized)
            if (char.IsLetterOrDigit(character) && CharUnicodeInfo.GetUnicodeCategory(character) != UnicodeCategory.NonSpacingMark)
                text.Append(char.ToLowerInvariant(character));
        return text.ToString();
    }
    private static Dictionary<string, string> LoadPaths()
    {
        var result = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (string resource in new[] { "OrbyLauncher.Iconography", "OrbyLauncher.Iconography.Supplemental" })
        {
            using var stream = typeof(AppIconTheme).Assembly.GetManifestResourceStream(resource);
            if (stream is null) continue;
            using var document = JsonDocument.Parse(stream);
            foreach (var entry in document.RootElement.EnumerateArray())
            {
                if (entry.ValueKind != JsonValueKind.Object
                    || !entry.TryGetProperty("path", out var pathValue) || pathValue.ValueKind != JsonValueKind.String) continue;
                string? path = pathValue.GetString();
                if (string.IsNullOrWhiteSpace(path)) continue;
                if (entry.TryGetProperty("fillRule", out var rule) && rule.ValueKind == JsonValueKind.String && rule.GetString() == "evenodd") path = "F0 " + path;
                string id = Normalize(entry.TryGetProperty("id", out var idValue) && idValue.ValueKind == JsonValueKind.String ? idValue.GetString() ?? "" : "");
                string name = Normalize(entry.TryGetProperty("name", out var nameValue) && nameValue.ValueKind == JsonValueKind.String ? nameValue.GetString() ?? "" : "");
                if (id.Length > 0) result[id] = path;
                if (name.Length > 0) result[name] = path;
            }
        }
        return result;
    }
}
