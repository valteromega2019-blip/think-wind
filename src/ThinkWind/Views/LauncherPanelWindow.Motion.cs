using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace ThinkWind.Views;

internal sealed partial class LauncherPanelWindow
{
    private readonly Dictionary<string, Button> tabButtons = new();
    private readonly TranslateTransform rowsMotion = new(), openingMotion = new();
    private string renderedStamp = "", renderedSection = "";
    private int renderedPage;
    private bool DrawerAnimations => IsVisible && !LayoutDrag.Active && menus == 0
        && SystemParameters.ClientAreaAnimation && Store.Value.AnimationMilliseconds > 0;
    private TimeSpan DrawerDuration => TimeSpan.FromMilliseconds(Math.Clamp(Store.Value.AnimationMilliseconds, 100, 320));

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
        rowsMotion.BeginAnimation(TranslateTransform.XProperty, null);
        rowsMotion.BeginAnimation(TranslateTransform.YProperty, null);
        resultsGrid.BeginAnimation(OpacityProperty, null);
        rowsMotion.X = rowsMotion.Y = 0; resultsGrid.Opacity = 1;
        if (!hadRows || !DrawerAnimations) return;
        if (direction == 0) rowsMotion.BeginAnimation(TranslateTransform.YProperty, Enter(6, 0), HandoffBehavior.SnapshotAndReplace);
        else rowsMotion.BeginAnimation(TranslateTransform.XProperty, Enter(direction * 16, 0), HandoffBehavior.SnapshotAndReplace);
        resultsGrid.BeginAnimation(OpacityProperty, Enter(.55, 1), HandoffBehavior.SnapshotAndReplace);
    }

    private void AnimateDrawerOpening()
    {
        content.RenderTransform = openingMotion;
        content.BeginAnimation(OpacityProperty, null); openingMotion.BeginAnimation(TranslateTransform.YProperty, null);
        content.Opacity = 1; openingMotion.Y = 0;
        if (!DrawerAnimations) return;
        content.BeginAnimation(OpacityProperty, Enter(.65, 1), HandoffBehavior.SnapshotAndReplace);
        openingMotion.BeginAnimation(TranslateTransform.YProperty, Enter(10, 0), HandoffBehavior.SnapshotAndReplace);
    }

    private void CancelDrawerMotion()
    {
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
