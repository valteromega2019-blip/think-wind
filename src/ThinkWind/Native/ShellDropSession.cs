using System;
using System.Runtime.InteropServices;
using System.Windows;
using ComData = System.Runtime.InteropServices.ComTypes.IDataObject;

namespace ThinkWind.Native;
// Ask the Shell's real drop handler; accepting a hover never means a transfer occurred.
internal sealed class ShellDropSession : IDisposable
{
    [StructLayout(LayoutKind.Sequential)] private struct Point { internal int X, Y; }
    [ComImport, Guid("43826D1E-E718-42EE-BC55-A1E261C37BFE"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface ShellItem
    {
        [PreserveSig] int BindToHandler(nint context, ref Guid handler, ref Guid iid, [MarshalAs(UnmanagedType.Interface)] out DropTarget target);
        [PreserveSig] int GetParent(out ShellItem parent);
        [PreserveSig] int GetDisplayName(uint kind, out nint name);
        [PreserveSig] int GetAttributes(uint mask, out uint attributes);
        [PreserveSig] int Compare(ShellItem other, uint hint, out int order);
    }
    [ComImport, Guid("00000122-0000-0000-C000-000000000046"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface DropTarget
    {
        [PreserveSig] int DragEnter([MarshalAs(UnmanagedType.Interface)] ComData data, uint keys, Point point, ref uint effect);
        [PreserveSig] int DragOver(uint keys, Point point, ref uint effect);
        [PreserveSig] int DragLeave();
        [PreserveSig] int Drop([MarshalAs(UnmanagedType.Interface)] ComData data, uint keys, Point point, ref uint effect);
    }
    [DllImport("shell32.dll", CharSet = CharSet.Unicode, PreserveSig = true)] private static extern int SHCreateItemFromParsingName(string name, nint context, ref Guid iid, out ShellItem item);
    private DropTarget? target;
    private ComData? data;
    private bool entered;
    private static Point Cursor() { var p = System.Windows.Forms.Cursor.Position; return new Point { X = p.X, Y = p.Y }; }
    internal static ShellDropSession? Open(string name, System.Windows.IDataObject source, DragDropEffects allowed, DragDropKeyStates keys, out DragDropEffects effect)
    {
        effect = DragDropEffects.None; ShellItem? item = null; var session = new ShellDropSession();
        try
        {
            Guid shellId = typeof(ShellItem).GUID, dropId = typeof(DropTarget).GUID, handler = new("3981E225-F559-11D3-8E3A-00C04F6837D5");
            if (SHCreateItemFromParsingName(name, 0, ref shellId, out item) < 0 || item.BindToHandler(0, ref handler, ref dropId, out var target) < 0) return null;
            session.target = target; session.data = source as ComData ?? (ComData)new System.Windows.DataObject(source);
            uint accepted = (uint)allowed & 5; // Opening files is never a move/delete operation.
            int hr = target.DragEnter(session.data, (uint)keys, Cursor(), ref accepted); session.entered = hr >= 0;
            effect = hr >= 0 ? (DragDropEffects)(accepted & (uint)allowed & 5) : DragDropEffects.None;
            if (!session.entered) { session.Dispose(); return null; }
            return session;
        }
        catch (Exception error) when (error is COMException or InvalidCastException or ArgumentException) { session.Dispose(); return null; }
        finally { CoreAudio.Release(item); }
    }
    internal DragDropEffects Over(DragDropEffects allowed, DragDropKeyStates keys)
    {
        if (!entered || target is null) return DragDropEffects.None;
        try { uint effect = (uint)allowed & 5; return target.DragOver((uint)keys, Cursor(), ref effect) >= 0 ? (DragDropEffects)(effect & (uint)allowed & 5) : DragDropEffects.None; }
        catch (COMException) { return DragDropEffects.None; }
    }
    internal DragDropEffects Deliver(DragDropEffects allowed, DragDropKeyStates keys)
    {
        if (!entered || target is null || data is null) return DragDropEffects.None;
        entered = false;
        try { uint effect = (uint)allowed & 5; return target.Drop(data, (uint)keys, Cursor(), ref effect) >= 0 ? (DragDropEffects)(effect & (uint)allowed & 5) : DragDropEffects.None; }
        catch (COMException) { return DragDropEffects.None; }
    }
    public void Dispose()
    {
        try { if (entered) target?.DragLeave(); } catch (COMException) { }
        entered = false; CoreAudio.Release(target); target = null; data = null;
    }
}
