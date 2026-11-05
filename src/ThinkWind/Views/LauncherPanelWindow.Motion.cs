using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace OrbyLauncher.Views;

internal sealed partial class LauncherPanelWindow
{
    private readonly Dictionary<string, Button> tabButtons = new();
    private readonly TranslateTransform rowsMotion = new(), openingMotion = new();
    private string renderedStamp = "", renderedSection = "";
    private int renderedPage;
    private readonly Stopwatch rowsAnimationTime = new();
    private bool DrawerAnimations => IsVisible && !editingLibrary && !LayoutDrag.Active && menus == 0
        && SystemParameters.ClientAreaAnimation && Store.Value.AnimationMilliseconds > 0;
    private TimeSpan DrawerDuration => TimeSpan.FromMilliseconds(Math.Clamp(Store.Value.AnimationMilliseconds * 1.4, 220, 440));

    private int DrawerDirection()
    {
        if (renderedSection == section) return Math.Sign(page - renderedPage);
        string[] sections = { "Fixados", "Biblioteca", "Recentes", "Todos" };
        return Math.Sign(Array.IndexOf(sections, section) - Array.IndexOf(sections, renderedSection));
    }

    private void UpdateDrawerTabs()
    {
        foreach (var pair in tabButtons)
        {
            bool selected = pair.Key == section;
            pair.Value.SetResourceReference(BackgroundProperty, selected ? "Think.AccentSurface" : "Think.Control");
            pair.Value.SetResourceReference(ForegroundProperty, selected ? "Think.AccentInk" : "Think.Text");
            pair.Value.FontWeight = selected ? FontWeights.SemiBold : FontWeights.Normal;
        }
    }

    private DoubleAnimation Enter(double from, double to) => new(from, to, DrawerDuration)
    { EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }, FillBehavior = FillBehavior.Stop };

    private void AnimateDrawerRows(int direction, bool hadRows)
    {
        // Animate the whole grid through composition, never its cell dimensions.
        // SnapshotAndReplace keeps rapid wheel/tab changes from accumulating clocks.
        resultsGrid.RenderTransform = rowsMotion;
        // WPF can retain a stopped animation clock after its visible effect
        // ends. Track the actual transition time, otherwise all later pages
        // start at offset zero and appear to have no animation at all.
        bool continuing = rowsAnimationTime.IsRunning && rowsAnimationTime.Elapsed < DrawerDuration;
        double previousX = rowsMotion.X, previousY = rowsMotion.Y, previousOpacity = resultsGrid.Opacity;
        rowsMotion.BeginAnimation(TranslateTransform.XProperty, null);
        rowsMotion.BeginAnimation(TranslateTransform.YProperty, null);
        resultsGrid.BeginAnimation(OpacityProperty, null);
        rowsMotion.X = rowsMotion.Y = 0; resultsGrid.Opacity = 1;
        if (!DrawerAnimations) { rowsAnimationTime.Reset(); return; }
        rowsAnimationTime.Restart();
        if (direction == 0) rowsMotion.BeginAnimation(TranslateTransform.YProperty, Enter(continuing ? previousY : 8, 0), HandoffBehavior.SnapshotAndReplace);
        else rowsMotion.BeginAnimation(TranslateTransform.XProperty, Enter(continuing ? previousX : direction * 32, 0), HandoffBehavior.SnapshotAndReplace);
        resultsGrid.BeginAnimation(OpacityProperty, Enter(continuing ? previousOpacity : hadRows ? .86 : .8, 1), HandoffBehavior.SnapshotAndReplace);
    }

    private void AnimateDrawerOpening(bool openingSurface)
    {
        content.RenderTransform = openingMotion;
        content.BeginAnimation(OpacityProperty, null); openingMotion.BeginAnimation(TranslateTransform.YProperty, null);
        content.Opacity = 1; openingMotion.Y = 0;
        if (!DrawerAnimations) return;
        if (openingSurface) AnimateOpeningFromOffset(18, DrawerDuration.TotalMilliseconds);
        AnimateDrawerRows(0, false);
        content.BeginAnimation(OpacityProperty, Enter(.65, 1), HandoffBehavior.SnapshotAndReplace);
        openingMotion.BeginAnimation(TranslateTransform.YProperty, Enter(10, 0), HandoffBehavior.SnapshotAndReplace);
    }

    private void CancelDrawerMotion()
    {
        rowsAnimationTime.Reset();
        resultsGrid.BeginAnimation(OpacityProperty, null); content.BeginAnimation(OpacityProperty, null);
        rowsMotion.BeginAnimation(TranslateTransform.XProperty, null); rowsMotion.BeginAnimation(TranslateTransform.YProperty, null);
        openingMotion.BeginAnimation(TranslateTransform.YProperty, null);
        rowsMotion.X = rowsMotion.Y = openingMotion.Y = 0;
        resultsGrid.Opacity = content.Opacity = 1;
    }

    public override void RefreshAppearance()
    {
        base.RefreshAppearance();
        if (!DrawerAnimations) CancelDrawerMotion();
    }
}
