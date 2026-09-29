using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Windows.Forms;

namespace ReplayRescue {
  static class Program {
    [STAThread] static int Main(string[] args) {
      if(args.Any(a=>a.StartsWith("chrome-extension://",StringComparison.Ordinal)) || args.Contains("--native-host"))return NativeHost.Run();
      try {
        Directory.CreateDirectory(Files.Data);
        if(args.Contains("--diagnose")){
          var s=Settings.Load();var n=new NvidiaProbe().Read();
          Files.Write(Path.Combine(Files.Data,"diagnostic.json"),new{at=DateTime.UtcNow.ToString("o"),user=Environment.UserName,nvidia=n,browser=BrowserState.Read(s,Native.ChromeWindowCount()),chromeWindowCount=Native.ChromeWindowCount(),hotkey=Native.HotkeyName(n.Hotkey),desktopReady=Native.DesktopReady()});return 0;
        }
        if(args.Contains("--self-test")) return Tests.Run();
        if(args.Contains("--recover-once")) return RecoverOnce();
        if(args.Contains("--integration-test"))return IntegrationTest();
        Application.EnableVisualStyles();Application.SetCompatibleTextRenderingDefault(false);
        if(args.Contains("--ui-test"))return Tests.RunUI();
        if(args.Contains("--preview")){
          using(var preview=new MainForm(false,true)){string language=args.FirstOrDefault(a=>a.StartsWith("--language="));if(language!=null)preview.ChangeLanguage(language.Substring(11));preview.Show();DateTime until=DateTime.UtcNow.AddSeconds(Settings.Load().ResumeDelaySeconds+1);while(DateTime.UtcNow<until){Application.DoEvents();Thread.Sleep(20);}var refresh=preview.RefreshPreview();while(!refresh.IsCompleted){Application.DoEvents();Thread.Sleep(20);}refresh.GetAwaiter().GetResult();Application.DoEvents();preview.SavePreview(Path.Combine(Files.Root,"preview.png"));}return 0;
        }
        bool created;
        using(var mutex=new Mutex(true,"Local\\ReplayRescue-"+System.Security.Principal.WindowsIdentity.GetCurrent().User.Value,out created)){
          if(!created){if(!args.Contains("--tray"))MessageBox.Show(Localizer.ForSettings().Translate("Replay Rescue가 이미 실행 중입니다.\n작업 표시줄 오른쪽 트레이 아이콘을 두 번 클릭하세요."),"Replay Rescue");return 0;}
          Integration.Register();
          Application.Run(new MainForm(args.Contains("--tray")));
        }
        return 0;
      }catch(Exception e){Files.Log("Fatal: "+e.ToString());if(!args.Any(a=>a.StartsWith("--"))){var text=Localizer.ForSettings();MessageBox.Show(text.Translate(e.Message),text.Translate("Replay Rescue 오류"));}return 1;}
    }
    static int RecoverOnce(){
      // Explicit diagnostic command: checks NVIDIA only; never used by automatic monitoring.
      var probe=new NvidiaProbe();var before=probe.Read();var at=DateTime.UtcNow;
      string error=null;bool sent=false;
      if(before.Enabled==false && before.Overlay){error=Native.SendHotkey(before.Hotkey);sent=error==null;}
      else error="Not sent: NVIDIA must be known-off with its overlay running.";
      NvidiaState after=before;
      if(sent)for(int i=0;i<24;i++){Thread.Sleep(500);after=probe.Read();if(after.Enabled==true && after.EnabledEventAt>=at.AddSeconds(-1) && (after.Running==true || after.DesktopCapture==false))break;}
      bool verified=sent && after.Enabled==true && after.EnabledEventAt>=at.AddSeconds(-1);
      Files.Write(Path.Combine(Files.Data,"recovery-test.json"),new{before=before,after=after,sent=sent,error=error,verifiedEnabled=verified,verifiedRecording=verified && after.Running==true && after.RuntimeAt>=at.AddSeconds(-1),at=at.ToString("o")});
      return verified?0:2;
    }
    static int IntegrationTest(){
      var probe=new NvidiaProbe();var n=probe.Read();var s=Settings.Load();var browser=BrowserState.Read(s,Native.ChromeWindowCount());
      string error=null;bool observedOff=false,restored=false;DateTime started=DateTime.UtcNow;
      // Do not discard an existing recording buffer for a test.
      if(!s.Enabled || !browser.Safe || browser.Connections<1 || n.Enabled!=true || n.Running!=false || n.DesktopCapture!=false) error="Test requires a connected clear browser, enabled monitoring, and known idle game-only capture.";
      else{
        error=Native.SendHotkey(n.Hotkey);
        if(error==null)for(int i=0;i<60;i++){
          Thread.Sleep(500);n=probe.Read();
          if(n.Enabled==false)observedOff=true;
          if(observedOff && n.Enabled==true && n.EnabledEventAt>=started){restored=true;break;}
        }
      }
      Files.Write(Path.Combine(Files.Data,"integration-test.json"),new{started=started.ToString("o"),observedOff=observedOff,restoredByMonitor=restored,browserConnections=browser.Connections,error=error,after=n});
      return restored?0:2;
    }
  }
}
