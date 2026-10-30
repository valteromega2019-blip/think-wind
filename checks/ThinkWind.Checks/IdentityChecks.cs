using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using ThinkWind.Native;

internal static class IdentityChecks
{
    [ComImport, Guid("886D8EEB-8CF2-4446-8D02-CDBA1DBDCF99"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface WritableStore
    {
        [PreserveSig] int GetCount(out uint count);
        [PreserveSig] int GetAt(uint index, out CoreAudio.PropertyKey key);
        [PreserveSig] int GetValue(ref CoreAudio.PropertyKey key, nint value);
        [PreserveSig] int SetValue(ref CoreAudio.PropertyKey key, nint value);
        [PreserveSig] int Commit();
    }
    [DllImport("shell32.dll")] private static extern int SHGetPropertyStoreForWindow(nint window, ref Guid iid, [MarshalAs(UnmanagedType.Interface)] out WritableStore store);
    [DllImport("ole32.dll")] private static extern int PropVariantClear(nint value);
    internal static void Check(string directory)
    {
        var window = new Window { Width = 160, Height = 80, ShowActivated = false, Title = "Think Wind identity check" };
        WritableStore? store = null;
        int size = IntPtr.Size == 8 ? 24 : 16;
        nint variant = Marshal.AllocCoTaskMem(size);
        Marshal.Copy(new byte[size], 0, variant, size);
        var key = new CoreAudio.PropertyKey { Format = new Guid("9F4C2855-9F79-4B39-A8D0-E1D42DE1D5F3"), Id = 5 };
        try
        {
            window.Show(); nint handle = new WindowInteropHelper(window).Handle;
            var iid = typeof(WritableStore).GUID;
            Marshal.ThrowExceptionForHR(SHGetPropertyStoreForWindow(handle, ref iid, out store));
            string id = "ThinkWind.Checks." + Guid.NewGuid().ToString("N");
            Marshal.WriteInt16(variant, 31); Marshal.WriteIntPtr(variant, 8, Marshal.StringToCoTaskMemUni(id));
            Marshal.ThrowExceptionForHR(store.SetValue(ref key, variant));
            if (ShellAppIdentity.WindowId(handle) != id) throw new InvalidOperationException("Identidade da janela não foi recuperada.");
            PropVariantClear(variant); Marshal.Copy(new byte[size], 0, variant, size);
            Marshal.ThrowExceptionForHR(store.SetValue(ref key, variant));
            if (ShellAppIdentity.WindowId(handle) is not null) throw new InvalidOperationException("Identidade removida permaneceu em cache.");
        }
        finally
        {
            PropVariantClear(variant); Marshal.Copy(new byte[size], 0, variant, size);
            if (store is not null) store.SetValue(ref key, variant);
            CoreAudio.Release(store); Marshal.FreeCoTaskMem(variant); window.Close();
        }
        string target = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "explorer.exe");
        string link = Path.Combine(directory, "identity.lnk"); object? shell = null, shortcut = null;
        try
        {
            shell = Activator.CreateInstance(Type.GetTypeFromProgID("WScript.Shell")!);
            shortcut = ((dynamic)shell!).CreateShortcut(link); ((dynamic)shortcut!).TargetPath = target; ((dynamic)shortcut!).Save();
            var identity = ShellAppIdentity.ReadLaunch(link, new ShellIcons());
            if (!identity.Path.Equals(target, StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("Atalho não associado ao executável de destino.");
        }
        finally { CoreAudio.Release(shortcut); CoreAudio.Release(shell); }
    }
}
