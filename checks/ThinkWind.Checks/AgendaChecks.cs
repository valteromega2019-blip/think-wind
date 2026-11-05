using System;
using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using OrbyLauncher.Services;
using OrbyLauncher.Views;
internal static class AgendaChecks
{
 internal static int Run()
 {
  _ = Application.Current ?? new Application(); int pass=0, fail=0;
  Application.Current!.Resources.MergedDictionaries.Add(new ResourceDictionary { Source = new Uri("/OrbyLauncher;component/Views/CalendarTheme.xaml", UriKind.Relative) });
  foreach(string theme in new[]{"Dark","Light"})
  foreach(double scale in new[]{1d,1.4})
  {
   string file=Path.Combine(Path.GetTempPath(),Guid.NewGuid().ToString("N"),"alarms.json");
   try
   {
    ThemeService.Apply(new Preferences{Theme=theme});
    using var alarms=new AlarmService(file); using var panel=new AgendaPanel(alarms);
    panel.SelectDay(DateTime.Today.AddDays(1),true);
    var body=(StackPanel)typeof(AgendaPanel).GetField("body",BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(panel)!;
    var toggle=Find<ToggleButton>(body) ?? throw new Exception("Seletor ausente");
    panel.LayoutTransform=new ScaleTransform(scale,scale);
    panel.Measure(new Size(428,double.PositiveInfinity)); panel.Arrange(new Rect(0,0,428,panel.DesiredSize.Height)); panel.UpdateLayout();
    foreach(var child in body.Children)
     if(child is StackPanel row && row.Children.Count==2 && row.Children[0] is TextBlock icon && row.Children[1] is TextBlock label)
      if(icon.Foreground!=Application.Current!.FindResource("Think.Text") || label.Foreground!=Application.Current!.FindResource("Think.Text")) throw new Exception("Rótulo sem recurso de contraste");
    toggle.IsChecked=true; toggle.IsChecked=false;
    if(Find<TextBox>(body) is null) throw new Exception("Campos perdidos ao alternar");
    foreach(double available in new[]{480d,720d,1040d})
    {
     double header=52, height=Math.Min(available,panel.DesiredSize.Height+header+32);
     var scroll=new ScrollViewer{Content=panel,MaxHeight=height-header-32,VerticalScrollBarVisibility=ScrollBarVisibility.Auto};
     scroll.Measure(new Size(428,height-header-32)); scroll.Arrange(new Rect(0,0,428,height-header-32)); scroll.UpdateLayout(); scroll.ScrollToEnd(); scroll.UpdateLayout();
     if(scroll.ScrollableHeight>0 && scroll.VerticalOffset < scroll.ScrollableHeight-1) throw new Exception("Fim inacessível");
     scroll.Content=null;
    }
    Console.WriteLine($"PASS Agenda {theme} fonte {scale}: contraste, seletor e fim acessível");pass++;
   }catch(Exception e){Console.WriteLine("FAIL "+e);fail++;}
  }
  Console.WriteLine($"Agenda: {pass} aprovados; {fail} falhas");return fail==0?0:1;
 }
 static T? Find<T>(DependencyObject root) where T:DependencyObject
 {
  if(root is T found)return found;
  for(int i=0;i<VisualTreeHelper.GetChildrenCount(root);i++){var result=Find<T>(VisualTreeHelper.GetChild(root,i));if(result is not null)return result;}
  if(root is Panel p)foreach(UIElement c in p.Children){var result=Find<T>(c);if(result is not null)return result;}
  return null;
 }
}
