using System;
using System.Globalization;
using System.Windows;
using System.Windows.Media;
using OrbyLauncher.Services;
internal static class ClockLayoutChecks
{
 internal static int Run()
 {
  int passed=0,failed=0;
  foreach(string family in new[]{"Segoe UI Variable","Arial","Consolas"})
  foreach(double scale in new[]{1d,1.4})
  foreach(double dpi in new[]{1d,1.25,1.5,2})
  {
   try
   {
    double size=14*scale, reserve=Math.Ceiling(size*3.8);
    for(int hour=0;hour<24;hour++)for(int minute=0;minute<60;minute++)
    {
     var text=new FormattedText($"{hour:00}:{minute:00}",CultureInfo.GetCultureInfo("pt-BR"),FlowDirection.LeftToRight,new Typeface(family),size,Brushes.White,dpi);
     if(text.WidthIncludingTrailingWhitespace>reserve)throw new Exception("Hora excedeu a reserva");
    }
    Console.WriteLine($"PASS Relógio {family} fonte={scale} DPI={dpi}");passed++;
   }catch(Exception e){Console.WriteLine("FAIL "+e.Message);failed++;}
  }
  Console.WriteLine($"Relógio: {passed} aprovados; {failed} falhas");return failed==0?0:1;
 }
}
