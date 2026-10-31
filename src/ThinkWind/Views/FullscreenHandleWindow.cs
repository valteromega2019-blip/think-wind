using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using ThinkWind.Services;

namespace ThinkWind.Views;

internal sealed class FullscreenHandleWindow : FloatingWindow
{
    protected override bool AttachedTop => true;
    internal FullscreenHandleWindow(SettingsStore store, Action reveal) : base(store)
    {
        Title = "Think Wind — Acesso em tela cheia";
        ShowActivated = false;
        Surface.Padding = new Thickness(0);
        Surface.Child = new Border { Width = 96, Height = 6, CornerRadius = new CornerRadius(3),
            Background = new SolidColorBrush(Color.FromRgb(160, 176, 193)) };
        MouseEnter += (_, _) => reveal();
    }
    public override void Position() => Place(96, 6, false);
}