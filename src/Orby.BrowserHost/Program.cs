using System.Buffers.Binary;
using System.Text.Json;
using System.Text.RegularExpressions;

// One message per process. Standard output is exclusively the framed protocol.
string folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "OrbyLauncher", "BrowserBridge");
try
{
    using var manifest = JsonDocument.Parse(File.ReadAllText(Path.Combine(folder, "host.json")));
    if (args.Length == 0 || !manifest.RootElement.GetProperty("allowed_origins").EnumerateArray().Any(origin => origin.GetString() == args[0])) return 2;
    using var input = Console.OpenStandardInput(); using var output = Console.OpenStandardOutput();
    byte[] header = new byte[4]; input.ReadExactly(header);
    int size = BinaryPrimitives.ReadInt32LittleEndian(header);
    if (size <= 0 || size > 800000) return 3;
    byte[] data = new byte[size]; input.ReadExactly(data);
    using var message = JsonDocument.Parse(data, new JsonDocumentOptions { MaxDepth = 12 });
    var root = message.RootElement;
    if (root.GetProperty("version").GetInt32() != 1 || !Guid.TryParse(root.GetProperty("session").GetString(), out var session)) return 4;
    string cache = Path.Combine(folder, "Snapshots"); Directory.CreateDirectory(cache);
    if ((File.GetAttributes(cache) & FileAttributes.ReparsePoint) != 0) return 5;
    foreach (var file in new DirectoryInfo(cache).GetFiles("*.json").OrderByDescending(file => file.LastWriteTimeUtc).Skip(31)) file.Delete();
    foreach (var file in new DirectoryInfo(cache).GetFiles("*.json").Where(file => DateTime.UtcNow - file.LastWriteTimeUtc > TimeSpan.FromMinutes(15))) file.Delete();
    string type = root.GetProperty("type").GetString() ?? "";
    if (type == "clear")
    {
        foreach (string file in Directory.EnumerateFiles(cache, session.ToString("N") + "-*.json")) File.Delete(file);
    }
    else
    {
        int window = root.GetProperty("windowId").GetInt32(); if (window < 0) return 4;
        string destination = Path.Combine(cache, session.ToString("N") + "-" + window + ".json");
        if (type == "remove") File.Delete(destination);
        else if (type == "snapshot")
        {
            var tabs = root.GetProperty("tabs"); if (tabs.GetArrayLength() > 4) return 4;
            if ((root.GetProperty("activeTitle").GetString()?.Length ?? 0) > 1000) return 4;
            foreach (var tab in tabs.EnumerateArray())
            {
                if ((tab.GetProperty("title").GetString()?.Length ?? 0) > 240 || tab.GetProperty("id").GetInt32() < 0) return 4;
                if (tab.GetProperty("image").ValueKind == JsonValueKind.String)
                {
                    string image = tab.GetProperty("image").GetString()!;
                    const string prefix = "data:image/jpeg;base64,";
                    if (!image.StartsWith(prefix, StringComparison.Ordinal) || image.Length > 140000) return 4;
                    byte[] bytes = Convert.FromBase64String(image[prefix.Length..]);
                    if (bytes.Length < 3 || bytes[0] != 255 || bytes[1] != 216 || bytes[2] != 255) return 4;
                }
            }
            string temporary = destination + "." + Guid.NewGuid().ToString("N") + ".tmp";
            File.WriteAllBytes(temporary, data); File.Move(temporary, destination, true);
        }
        else return 4;
    }
    byte[] reply = "{\"ok\":true}"u8.ToArray(); BinaryPrimitives.WriteInt32LittleEndian(header, reply.Length);
    output.Write(header); output.Write(reply); output.Flush(); return 0;
}
catch (Exception error) when (error is IOException or UnauthorizedAccessException or JsonException or InvalidOperationException or FormatException or KeyNotFoundException) { return 1; }
