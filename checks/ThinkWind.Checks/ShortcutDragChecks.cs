using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;
using System.Windows;
using OrbyLauncher.Views;
internal static class ShortcutDragChecks
{
    [DllImport("shell32.dll", CharSet = CharSet.Unicode)] private static extern uint DragQueryFile(nint drop, uint index, System.Text.StringBuilder? path, uint count);
    [DllImport("ole32.dll")] private static extern void ReleaseStgMedium(ref STGMEDIUM medium);
    internal static int Run()
    {
        int passed = 0, failed = 0;
        void Check(string name, Action action) { try { action(); passed++; Console.WriteLine("PASS " + name); } catch(Exception error) { failed++; Console.WriteLine("FAIL " + name + ": " + error); } }
        string folder = Path.Combine(Path.GetTempPath(), "OrbyShortcutChecks-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
        string path = Path.Combine(folder, "Atalho de teste.lnk");
        object? shell = null, shortcut = null;
        try
        {
            shell = Activator.CreateInstance(Type.GetTypeFromProgID("WScript.Shell")!);
            shortcut = ((dynamic)shell!).CreateShortcut(path);
            ((dynamic)shortcut).TargetPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "explorer.exe");
            ((dynamic)shortcut).Save();
            Check("Atalho real foi criado sem executar aplicativo", () => { if (!File.Exists(path) || new FileInfo(path).Length == 0) throw new Exception("Atalho ausente."); });
            var data = LibraryShortcutDrag.CreateFileData(path);
            Check("Formato gerenciado FileDrop contém o caminho", () => { if (data.GetData(DataFormats.FileDrop) is not string[] files || files.Length != 1 || files[0] != path) throw new Exception("FileDrop inválido."); });
            Check("OLE CF_HDROP pode ser lido pela API do Shell", () =>
            {
                var format = new FORMATETC { cfFormat = 15, dwAspect = DVASPECT.DVASPECT_CONTENT, lindex = -1, tymed = TYMED.TYMED_HGLOBAL };
                var ole = (System.Runtime.InteropServices.ComTypes.IDataObject)data;
                if (ole.QueryGetData(ref format) != 0) throw new Exception("Shell recusou formato.");
                ole.GetData(ref format, out var medium);
                try
                {
                    if (DragQueryFile(medium.unionmember, uint.MaxValue, null, 0) != 1) throw new Exception("CF_HDROP não contém um arquivo.");
                    var name = new System.Text.StringBuilder(2048); DragQueryFile(medium.unionmember, 0, name, 2048);
                    if (name.ToString() != path) throw new Exception("Caminho OLE diferente.");
                }
                finally { ReleaseStgMedium(ref medium); }
            });
            Check("Arquivo continua disponível após leitura OLE", () => { if (!File.Exists(path)) throw new Exception("Arquivo removido cedo."); });
            Check("Origem inexistente é recusada antes do arraste", () => { try { LibraryShortcutDrag.CreateFileData(Path.Combine(folder, "ausente.lnk")); } catch(FileNotFoundException) { return; } throw new Exception("Arquivo inexistente aceito."); });
        }
        finally
        {
            if (shortcut is not null) Marshal.FinalReleaseComObject(shortcut);
            if (shell is not null) Marshal.FinalReleaseComObject(shell);
            File.Delete(path); Directory.Delete(folder);
        }
        Console.WriteLine($"Atalhos: {passed} aprovados; {failed} falhas. Sem drop real no desktop ou na barra.");
        return failed == 0 ? 0 : 1;
    }
}