using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using OrbyLauncher.Views;

internal static class IslandViewportChecks
{
    internal static int Run()
    {
        _ = Application.Current ?? new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
        int failed = 0, passed = 0;
        foreach (double width in new[] { 400d, 540, 780, 900 })
        foreach (double height in new[] { 260d, 440, 720 })
        {
            try { Check(width, height); passed++; Console.WriteLine($"PASS {width}x{height}"); }
            catch (Exception error) { failed++; Console.WriteLine($"FAIL {width}x{height}: {error.Message}"); }
        }
        Console.WriteLine($"Layout: {passed} passed, {failed} failed");
        return failed == 0 ? 0 : 1;
    }
    private static void Check(double width, double height)
    {
        var board = new IslandModuleBoard { Width = 760, Gap = 8 };
        var cards = new Border[5];
        for (int i = 0; i < cards.Length; i++)
        {
            cards[i] = new Border { Child = new Border { MinHeight = i == 4 ? 310 : 240 } };
            cards[i].SetValue(IslandModuleBoard.ColumnsProperty, i == 0 ? 1 : 2);
        }
        board.SetCards(cards);
        board.Measure(new Size(760, double.PositiveInfinity));
        double natural = board.DesiredSize.Height;
        double fit = Math.Min(1, Math.Min((width - 24) / 760, (height - 104) / natural));
        var viewport = new IslandModuleViewport { Child = board, Width = 760 * fit, Height = natural * fit };
        viewport.Configure(760, natural, fit);
        var root = new DockPanel { Width = viewport.Width, Height = viewport.Height + 80 };
        var header = new Border { Height = 80 }; DockPanel.SetDock(header, Dock.Top); root.Children.Add(header);
        var full = new StackPanel(); full.Children.Add(new AdornerDecoratorHost(viewport)); root.Children.Add(full);
        var host = new IslandLayoutHost { Child = root };
        void Arrange()
        {
            host.Measure(new Size(root.Width, root.Height));
            host.Arrange(new Rect(0, 0, root.Width, root.Height)); host.UpdateLayout();
        }
        Arrange();
        Validate("first", root, board, cards[4]);
        // Simulate metadata/native calendar becoming taller after first arrange.
        ((Border)cards[4].Child).MinHeight = 380;
        Arrange();
        if (viewport.RenderSize.Height > viewport.Height + .1)
            throw new Exception($"Viewport draws {viewport.RenderSize.Height:F2} above explicit height {viewport.Height:F2}");
        var right = cards[4].TransformToAncestor(root).Transform(new Point(cards[4].ActualWidth, 0)).X;
        if (root.Width - right > 1) throw new Exception($"Empty right strip {root.Width - right:F2} DIP after content grows");
        Validate("late content", root, board, cards[4]);
        host.Measure(new Size(root.Width, 6)); host.Arrange(new Rect(0, 0, root.Width, 6));
        Arrange();
        Validate("reopen", root, board, cards[4]);
    }
    private static void Validate(string phase, FrameworkElement root, FrameworkElement board, FrameworkElement last)
    {
        var bottom = last.TransformToAncestor(root).Transform(new Point(0, last.ActualHeight)).Y;
        if (Math.Abs(bottom - root.Height) > 1) throw new Exception($"{phase}: last bottom {bottom:F2} differs from {root.Height:F2}");
        var right = last.TransformToAncestor(root).Transform(new Point(last.ActualWidth, 0)).X;
        if (Math.Abs(root.Width - right) > 1) throw new Exception($"{phase}: right margin {root.Width - right:F2} DIP");
        if (last.ActualHeight < 300) throw new Exception($"{phase}: row truncated to {last.ActualHeight}");
    }
}