using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using ThinkWind.Native;

namespace ThinkWind.Views;
internal sealed partial class DockWindow
{
    private bool fileDragging;
    private string? fileTarget;
    private ShellDropSession? shellDrop;
    private readonly DispatcherTimer activateDrop = new() { Interval = TimeSpan.FromMilliseconds(450) };
    private readonly DispatcherTimer pageDrop = new() { Interval = TimeSpan.FromMilliseconds(500) };
    private int dropPageDirection;
    private nint dropWindow;
    private Button? dropButton;
    private object? dropTooltip;
    private Border? dropMark;
    private void InitializeFileDrop()
    {
        AllowDrop = true;
        PreviewDragEnter += (_, e) => { if (HasFiles(e)) BeginFileDrag(); };
        PreviewDragOver += (_, e) => { if (HasFiles(e)) { BeginFileDrag(); e.Effects = DragDropEffects.None; } };
        Drop += (_, e) => { if (HasFiles(e) && !e.Handled) { e.Effects = DragDropEffects.None; e.Handled = true; FinishFileDrag(); } };
        activateDrop.Tick += (_, _) =>
        {
            activateDrop.Stop(); if (!fileDragging || !Desktop.LeftButtonDown || dropWindow == 0) return;
            bool activated = Desktop.RestoreAndActivate(dropWindow);
            if (dropButton is not null) dropButton.ToolTip = activated ? "Janela ativada. Continue arrastando até a área de destino do aplicativo." : "O Windows não permitiu ativar esta janela. Continue até o aplicativo manualmente.";
        };
        pageDrop.Tick += (_, _) =>
        {
            pageDrop.Stop(); int destination = page + dropPageDirection;
            if (!fileDragging || !Desktop.LeftButtonDown || destination < 0 || destination >= fit.Pages(entries.Count)) return;
            ClearFileTarget(); page = destination; RenderPage(); Position();
        };
    }
    private static bool HasFiles(DragEventArgs e)
    {
        try { return e.Data.GetDataPresent(DataFormats.FileDrop, false); }
        catch (Exception error) when (error is COMException or InvalidOperationException) { return false; }
    }
    private void BeginFileDrag()
    {
        StopPreviews();
        magnifier.Reset();
        fileDragging = true; lastHover = DateTime.UtcNow;
        launcher?.Dismiss();
        // The entire visible collapsed strip is a real OLE drop target at the screen edge.
        SetHidden(false, false);
    }
    private void AttachFileTarget(Button button, Item item, Border marker)
    {
        if (item.Target is null && item.Windows.Count == 0) return;
        button.AllowDrop = true;
        void Over(DragEventArgs e)
        {
            if (!HasFiles(e)) return;
            BeginFileDrag(); e.Handled = true;
            if (fileTarget != item.Key)
            {
                ClearFileTarget(); fileTarget = item.Key; dropButton = button; dropTooltip = button.ToolTip; dropMark = marker;
                var window = item.Windows.Find(candidate => !candidate.Minimized) ?? (item.Windows.Count > 0 ? item.Windows[0] : null);
                if (window is not null)
                {
                    dropWindow = window.Handle; marker.Visibility = Visibility.Visible;
                    button.ToolTip = "Mantenha o arquivo aqui para ativar a janela; depois continue o arrasto para dentro dela."; activateDrop.Start();
                }
                else if (item.Target is not null)
                    shellDrop = ShellDropSession.Open(item.Target, e.Data, e.AllowedEffects, e.KeyStates, out _);
            }
            if (dropWindow != 0) { e.Effects = DragDropEffects.None; return; }
            e.Effects = shellDrop?.Over(e.AllowedEffects, e.KeyStates) ?? DragDropEffects.None;
            marker.Visibility = e.Effects == DragDropEffects.None ? Visibility.Hidden : Visibility.Visible;
            button.ToolTip = e.Effects == DragDropEffects.None ? "Este atalho não expôs um receptor compatível. Abra o aplicativo e continue o arrasto até sua janela." : "Solte para abrir os arquivos pelo receptor nativo deste aplicativo.";
        }
        button.PreviewDragEnter += (_, e) => Over(e); button.PreviewDragOver += (_, e) => Over(e);
        button.PreviewDragLeave += (_, e) =>
        {
            if (!HasFiles(e) || fileTarget != item.Key) return;
            var point = e.GetPosition(button);
            if (point.X < 0 || point.Y < 0 || point.X >= button.ActualWidth || point.Y >= button.ActualHeight) ClearFileTarget();
        };
        button.PreviewDrop += (_, e) =>
        {
            if (!HasFiles(e)) return;
            e.Handled = true;
            // Existing windows receive files only when the user drops inside their own UI.
            e.Effects = fileTarget == item.Key && dropWindow == 0 ? shellDrop?.Deliver(e.AllowedEffects, e.KeyStates) ?? DragDropEffects.None : DragDropEffects.None;
            FinishFileDrag();
        };
    }
    private void ClearFileTarget()
    {
        activateDrop.Stop(); shellDrop?.Dispose(); shellDrop = null; fileTarget = null; dropWindow = 0;
        if (dropMark is not null) dropMark.Visibility = Visibility.Hidden;
        if (dropButton is not null) dropButton.ToolTip = dropTooltip;
        dropMark = null; dropButton = null; dropTooltip = null;
    }
    private void AttachFilePageTarget(Button button, int direction)
    {
        button.AllowDrop = true;
        button.PreviewDragEnter += (_, e) => { if (!HasFiles(e)) return; BeginFileDrag(); ClearFileTarget(); dropPageDirection = direction; pageDrop.Stop(); pageDrop.Start(); e.Effects = DragDropEffects.None; e.Handled = true; };
        button.PreviewDragLeave += (_, _) => pageDrop.Stop();
    }
    private void FinishFileDrag() { fileDragging = false; pageDrop.Stop(); ClearFileTarget(); lastHover = DateTime.UtcNow; signature = ""; }
    private void PollFileDrag()
    {
        if (!fileDragging) return;
        if (!Desktop.LeftButtonDown || Desktop.EscapeDown) FinishFileDrag();
    }
    private void DisposeFileDrop() { activateDrop.Stop(); pageDrop.Stop(); ClearFileTarget(); }
}
