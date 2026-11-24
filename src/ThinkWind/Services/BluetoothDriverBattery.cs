using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;

namespace OrbyLauncher.Services;

// Read only the battery property already populated by the Windows Bluetooth driver.
// No GATT connection, pairing, HID reports, shell process or keyboard capture.
internal static class BluetoothDriverBattery
{
    [StructLayout(LayoutKind.Sequential)] private struct PropertyKey { internal Guid Format; internal uint Id; }
    [DllImport("cfgmgr32.dll", CharSet = CharSet.Unicode)] private static extern uint CM_Get_Device_ID_List_SizeW(out uint size, string filter, uint flags);
    [DllImport("cfgmgr32.dll", CharSet = CharSet.Unicode)] private static extern uint CM_Get_Device_ID_ListW(string filter, [Out] char[] buffer, uint size, uint flags);
    [DllImport("cfgmgr32.dll", CharSet = CharSet.Unicode)] private static extern uint CM_Locate_DevNodeW(out uint node, string id, uint flags);
    [DllImport("cfgmgr32.dll", CharSet = CharSet.Unicode)] private static extern uint CM_Get_DevNode_PropertyW(uint node, ref PropertyKey key, out uint type, [Out] byte[] buffer, ref uint size, uint flags);
    internal static PeripheralBattery[] Read()
    {
        var result = new List<PeripheralBattery>();
        const uint presentEnumerator = 0x101;
        if (CM_Get_Device_ID_List_SizeW(out uint size, "BTHLE", presentEnumerator) != 0 || size > 1048576) return result.ToArray();
        var ids = new char[size];
        if (CM_Get_Device_ID_ListW("BTHLE", ids, size, presentEnumerator) != 0) return result.ToArray();
        foreach (string id in new string(ids).Split('\0', StringSplitOptions.RemoveEmptyEntries))
        {
            if (!id.StartsWith(@"BTHLE\DEV_", StringComparison.OrdinalIgnoreCase) || CM_Locate_DevNodeW(out uint node, id, 0) != 0) continue;
            var key = new PropertyKey { Format = new Guid("104EA319-6EE2-4701-BD47-8DDBF425BBE5"), Id = 2 };
            var buffer = new byte[4]; uint length = 4;
            if (CM_Get_DevNode_PropertyW(node, ref key, out uint type, buffer, ref length, 0) != 0 || type != 3 || length != 1 || buffer[0] > 100) continue;
            int percent = buffer[0];
            key = new PropertyKey { Format = new Guid("A45C254E-DF1C-4EFD-8020-67D146A850E0"), Id = 14 };
            var text = new byte[1024]; length = (uint)text.Length;
            if (CM_Get_DevNode_PropertyW(node, ref key, out type, text, ref length, 0) != 0 || type != 18 || length < 2 || length > text.Length) continue;
            string name = Encoding.Unicode.GetString(text, 0, (int)length).TrimEnd('\0');
            if (string.IsNullOrWhiteSpace(name)) continue;
            string label = name.ToLowerInvariant();
            string glyph = label.Contains("keyboard") || label.Contains("teclado") || label.EndsWith(" kb") ? "\uE765"
                : label.Contains("mouse") ? "\uE962" : label.Contains("controller") || label.Contains("controle") ? "\uE7FC" : "\uE7F6";
            result.Add(new(name, percent, glyph));
        }
        return result.ToArray();
    }
}
