using System;
using System.IO;
using System.Runtime.InteropServices;

namespace ThinkWind.Native;

internal sealed record LaunchIdentity(string Path, string? AppId, bool? Installed = null);
internal static class ShellAppIdentity
{
    [DllImport("shell32.dll")]
    private static extern int SHGetPropertyStoreForWindow(nint window, ref Guid iid, [MarshalAs(UnmanagedType.Interface)] out CoreAudio.IPropertyStore store);
    [DllImport("ole32.dll")] private static extern int PropVariantClear(nint value);
    internal static string? WindowId(nint window)
    {
        CoreAudio.IPropertyStore? store = null;
        int size = IntPtr.Size == 8 ? 24 : 16;
        nint value = Marshal.AllocCoTaskMem(size);
        Marshal.Copy(new byte[size], 0, value, size);
        try
        {
            var iid = typeof(CoreAudio.IPropertyStore).GUID;
            if (SHGetPropertyStoreForWindow(window, ref iid, out store) < 0) return null;
            var key = new CoreAudio.PropertyKey { Format = new Guid("9F4C2855-9F79-4B39-A8D0-E1D42DE1D5F3"), Id = 5 };
            if (store.GetValue(ref key, value) < 0 || Marshal.ReadInt16(value) != 31) return null;
            string? id = Marshal.PtrToStringUni(Marshal.ReadIntPtr(value, 8));
            return string.IsNullOrWhiteSpace(id) ? null : id;
        }
        catch (COMException) { return null; }
        finally { PropVariantClear(value); Marshal.FreeCoTaskMem(value); CoreAudio.Release(store); }
    }
    // Called on the catalog's existing STA worker, never on the UI thread.
    internal static LaunchIdentity ReadLaunch(string target, ShellIcons icons)
    {
        string path = icons.Resolve(target);
        const string prefix = @"shell:AppsFolder\";
        string? id = target.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) ? target[prefix.Length..] : null;
        object? shell = null, folder = null, item = null;
        try
        {
            if (id is null && !System.IO.Path.GetExtension(target).Equals(".lnk", StringComparison.OrdinalIgnoreCase)) return new(path, null);
            shell = Activator.CreateInstance(Type.GetTypeFromProgID("Shell.Application")!);
            folder = ((dynamic)shell!).NameSpace(id is null ? System.IO.Path.GetDirectoryName(target) : "shell:AppsFolder");
            if (folder is null) return new(path, id);
            item = ((dynamic)folder).ParseName(id ?? System.IO.Path.GetFileName(target));
            if (item is null) return new(path, id, target.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) ? false : null);
            string explicitId = Convert.ToString(((dynamic)item).ExtendedProperty("System.AppUserModel.ID")) ?? "";
            if (!string.IsNullOrWhiteSpace(explicitId)) id = explicitId;
            string resolved = Convert.ToString(((dynamic)item).ExtendedProperty("System.Link.TargetParsingPath")) ?? "";
            if (!string.IsNullOrWhiteSpace(resolved) && File.Exists(resolved)) path = resolved;
            else
            {
                string parsingPath = Convert.ToString(((dynamic)item).Path) ?? "";
                if (File.Exists(parsingPath)) path = icons.Resolve(parsingPath);
            }
        }
        catch (Exception error) when (error is COMException or ArgumentException or IOException or UnauthorizedAccessException or Microsoft.CSharp.RuntimeBinder.RuntimeBinderException) { }
        finally { CoreAudio.Release(item); CoreAudio.Release(folder); CoreAudio.Release(shell); }
        return new(path, id, target.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) && item is not null ? true : null);
    }
}
