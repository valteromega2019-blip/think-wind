param([Parameter(Mandatory=$true)][string]$StateFile, [int]$OwnerPid)
$ErrorActionPreference = 'Stop'
$script:ownPath = $PSCommandPath
Add-Type -AssemblyName PresentationFramework, PresentationCore, WindowsBase
Add-Type @'
using System;
using System.Runtime.InteropServices;
public static class OrbyUpdateMaterial {
 [StructLayout(LayoutKind.Sequential)] public struct Margins { public int Left, Right, Top, Bottom; }
 [DllImport("dwmapi.dll")] public static extern int DwmSetWindowAttribute(IntPtr window, int attribute, ref int value, int size);
 [DllImport("dwmapi.dll")] public static extern int DwmExtendFrameIntoClientArea(IntPtr window, ref Margins margins);
}
'@
$accent = '#60CDFF'; $background = '#111318'; $opacity = .72; $light = $false
try {
    $prefs = Get-Content -LiteralPath (Join-Path $env:LOCALAPPDATA 'OrbyLauncher\settings.json') -Raw | ConvertFrom-Json
    if ($prefs.AccentColor -match '^#[A-Fa-f0-9]{6}$') { $accent = $prefs.AccentColor }
    if ($prefs.BarBackgroundColor -match '^#[A-Fa-f0-9]{6}$') { $background = $prefs.BarBackgroundColor }
    if ($null -ne $prefs.IslandOpacity) { $opacity = [Math]::Max(.05, [Math]::Min(1, [double]$prefs.IslandOpacity)) }
    $light = $prefs.Theme -eq 'Light'
    if ($prefs.Theme -eq 'Auto') { $light = (Get-ItemProperty 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Themes\Personalize').AppsUseLightTheme -eq 1 }
} catch { }
function Brush([string]$value) { return [Windows.Media.BrushConverter]::new().ConvertFromString($value) }
$window = [Windows.Window]::new()
$window.Title = 'Orby Launcher - Atualização'; $window.Width = 480
$window.SizeToContent = 'Height'; $window.MinHeight = 260; $window.MaxHeight = [Windows.SystemParameters]::WorkArea.Height
$window.WindowStartupLocation = 'CenterScreen'; $window.WindowStyle = 'None'; $window.ResizeMode = 'NoResize'
$window.Background = [Windows.Media.Brushes]::Transparent
$chrome = [Windows.Shell.WindowChrome]::new(); $chrome.GlassFrameThickness = [Windows.Thickness]::new(-1)
$chrome.CaptionHeight = 0; $chrome.ResizeBorderThickness = [Windows.Thickness]::new(0)
[Windows.Shell.WindowChrome]::SetWindowChrome($window, $chrome)
$surface = [Windows.Controls.Border]::new(); $surface.CornerRadius = [Windows.CornerRadius]::new(20)
$surface.Padding = [Windows.Thickness]::new(28); $surface.Background = Brush $background
$root = [Windows.Controls.StackPanel]::new(); $surface.Child = $root; $window.Content = $surface
$title = [Windows.Controls.TextBlock]::new(); $title.Text = 'Orby Launcher'; $title.FontSize = 23; $title.FontWeight = 'SemiBold'
$title.Foreground = Brush $accent; $root.Children.Add($title) | Out-Null
$symbol = [Windows.Controls.TextBlock]::new(); $symbol.Text = [string][char]0xE895
$symbol.FontFamily = [Windows.Media.FontFamily]::new('Segoe Fluent Icons, Segoe MDL2 Assets'); $symbol.FontSize = 26
$symbol.Foreground = Brush $accent; $symbol.Margin = [Windows.Thickness]::new(0,16,0,0)
$root.Children.Add($symbol) | Out-Null
$stage = [Windows.Controls.TextBlock]::new(); $stage.FontSize = 17; $stage.FontWeight = 'SemiBold'
$stage.Text = 'Preparando atualização'; $stage.Margin = [Windows.Thickness]::new(0,20,0,8)
$stage.Foreground = [Windows.Media.Brushes]::White; $root.Children.Add($stage) | Out-Null
$details = [Windows.Controls.TextBlock]::new(); $details.FontSize = 13; $details.TextWrapping = 'Wrap'
$details.Foreground = [Windows.Media.Brushes]::White; $details.Opacity = .85
$details.Text = 'Aguarde. Suas configurações serão preservadas.'; $root.Children.Add($details) | Out-Null
$progress = [Windows.Controls.ProgressBar]::new(); $progress.Height = 8; $progress.Margin = [Windows.Thickness]::new(0,22,0,18)
$progress.IsIndeterminate = $true; $progress.Foreground = Brush $accent; $progress.Background = Brush '#30343B'
$progress.BorderThickness = [Windows.Thickness]::new(0)
$progressClip = [Windows.Controls.Border]::new(); $progressClip.CornerRadius = [Windows.CornerRadius]::new(4)
$progressClip.Child = $progress; $root.Children.Add($progressClip) | Out-Null
$hint = [Windows.Controls.TextBlock]::new(); $hint.Text = 'Não desligue o computador durante a instalação.'
$hint.TextWrapping = 'Wrap'; $hint.FontSize = 11; $hint.Foreground = [Windows.Media.Brushes]::White; $hint.Opacity = .65
$root.Children.Add($hint) | Out-Null
$close = [Windows.Controls.Button]::new(); $close.Content = 'Fechar'; $close.Visibility = 'Collapsed'
$close.Padding = [Windows.Thickness]::new(14,7,14,7); $close.Margin = [Windows.Thickness]::new(0,14,0,0)
$close.HorizontalAlignment = 'Right'; $close.Add_Click({ $window.Close() }); $root.Children.Add($close) | Out-Null
$script:finished = $false; $script:completedAt = $null
$window.Add_Closing({ param($sender,$event) if (!$script:finished) { $event.Cancel = $true } })
$window.Add_MouseLeftButtonDown({ try { $window.DragMove() } catch { } })
$window.Add_SourceInitialized({
    $handle = [Windows.Interop.WindowInteropHelper]::new($window).Handle
    $dark = [int](!$light); $round = 2; $material = 3
    [OrbyUpdateMaterial]::DwmSetWindowAttribute($handle,20,[ref]$dark,4) | Out-Null
    [OrbyUpdateMaterial]::DwmSetWindowAttribute($handle,33,[ref]$round,4) | Out-Null
    $result = [OrbyUpdateMaterial]::DwmSetWindowAttribute($handle,38,[ref]$material,4)
    if ($result -ge 0 -and ![Windows.SystemParameters]::HighContrast) {
        $margins = [OrbyUpdateMaterial+Margins]::new(); $margins.Left = -1; $margins.Right = -1; $margins.Top = -1; $margins.Bottom = -1
        [OrbyUpdateMaterial]::DwmExtendFrameIntoClientArea($handle,[ref]$margins) | Out-Null
        $tint = [Windows.Media.ColorConverter]::ConvertFromString($background); $tint.A = [byte](255 * $opacity)
        $surface.Background = [Windows.Media.SolidColorBrush]::new($tint)
    }
    # Keep text legible even for custom very light backgrounds.
    $color = [Windows.Media.ColorConverter]::ConvertFromString($background)
    if ((.2126*$color.R + .7152*$color.G + .0722*$color.B) -gt 170) {
        $stage.Foreground = $details.Foreground = $hint.Foreground = [Windows.Media.Brushes]::Black
    }
    if ([Windows.SystemParameters]::HighContrast) {
        $surface.Background = [Windows.SystemColors]::WindowBrush
        $title.Foreground = $stage.Foreground = $details.Foreground = $hint.Foreground = [Windows.SystemColors]::WindowTextBrush
    }
})
$timer = [Windows.Threading.DispatcherTimer]::new(); $timer.Interval = [TimeSpan]::FromMilliseconds(500)
$timer.Add_Tick({
    try {
        $state = Get-Content -LiteralPath $StateFile -Raw | ConvertFrom-Json
        $stage.Text = [string]$state.Title; $details.Text = [string]$state.Detail
        if ($state.Done) {
            $script:finished = $true; $progress.IsIndeterminate = $false
            $symbol.Text = if ($state.Error) { [string][char]0xE7BA } else { [string][char]0xE73E }
            if ($state.Error) { $progress.Value = 0; $close.Visibility = 'Visible'; $hint.Text = 'A instalação não foi confirmada. Consulte o aviso acima.' }
            else { $progress.Value = 100; $hint.Text = 'O Orby foi iniciado.'; if ($null -eq $script:completedAt) { $script:completedAt = [DateTime]::UtcNow } }
        }
    } catch { }
    if (!$script:finished -and $OwnerPid -gt 0 -and $null -eq (Get-Process -Id $OwnerPid -ErrorAction SilentlyContinue)) {
        $script:finished = $true; $progress.IsIndeterminate = $false; $progress.Value = 0
        $stage.Text = 'Atualização interrompida'; $details.Text = 'O auxiliar encerrou sem confirmar a conclusão. Abra o Orby pelo menu Iniciar para conferir o estado.'
        $close.Visibility = 'Visible'
    }
    if ($null -ne $script:completedAt -and ([DateTime]::UtcNow - $script:completedAt).TotalSeconds -ge 3) { $window.Close() }
})
$window.Add_Closed({
    $timer.Stop()
    foreach ($file in @($StateFile, ($StateFile + '.ready'), $script:ownPath)) {
        try { Remove-Item -LiteralPath $file -Force -ErrorAction SilentlyContinue } catch { }
    }
}); $timer.Start()
$window.Add_Loaded({ [IO.File]::WriteAllText($StateFile + '.ready', 'ready') })
$window.ShowDialog() | Out-Null
