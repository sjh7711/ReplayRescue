using System;
using System.IO;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;
using Microsoft.Win32;

namespace ReplayRescue {
  public static class Integration {
    public static void Register(){
      string id=File.ReadAllText(Path.Combine(Files.Root,"extension-id.txt")).Trim();
      if(!System.Text.RegularExpressions.Regex.IsMatch(id,"^[a-p]{32}$"))throw new InvalidDataException("Invalid extension ID");
      string manifest=Path.Combine(Files.Root,"native-host.json");
      Files.Write(manifest,new{name="com.local.replayrescue",description="Local Replay Rescue domain bridge",path=Path.Combine(Files.Root,"ReplayRescue.exe"),type="stdio",allowed_origins=new[]{"chrome-extension://"+id+"/"}});
      using(var k=Registry.CurrentUser.CreateSubKey(@"Software\Google\Chrome\NativeMessagingHosts\com.local.replayrescue"))k.SetValue("",manifest);
      string marker=Path.Combine(Files.Data,"setup-complete.json");
      if(!File.Exists(marker)){
        MainForm.SetStartup(true);
        if(!File.Exists(Settings.PathName))Files.Write(Settings.PathName,new Settings());
        Files.Write(marker,new{version=1});
      }
    }
  }
  public static class AppIcon {
    [DllImport("user32.dll")]static extern bool DestroyIcon(IntPtr handle);
    public static Icon Create(Color color){
      using(var bitmap=new Bitmap(32,32)){
        using(var g=Graphics.FromImage(bitmap)){
          g.SmoothingMode=SmoothingMode.AntiAlias;
          using(var back=new SolidBrush(Color.FromArgb(19,24,32)))g.FillEllipse(back,1,1,30,30);
          using(var pen=new Pen(color,3))g.DrawArc(pen,5,5,22,22,35,280);
          using(var brush=new SolidBrush(color)){
            g.FillPolygon(brush,new[]{new PointF(26,3),new PointF(28,13),new PointF(18,9)});
            g.FillRectangle(brush,13,11,3,10);g.FillRectangle(brush,18,11,3,10);
          }
        }
        IntPtr h=bitmap.GetHicon();try{return (Icon)Icon.FromHandle(h).Clone();}finally{DestroyIcon(h);}
      }
    }
  }
}
