using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Collections.Generic;

namespace ReplayRescue {
  public static class Tests {
    static List<string> passed=new List<string>();
    static void Assert(bool condition,string name){if(!condition)throw new Exception("FAIL: "+name);passed.Add(name);}
    public static int RunUI(){
      MainForm form=null;
      try{
        form=new MainForm(true,true);form.Show();System.Windows.Forms.Application.DoEvents();
        Assert(!form.Visible,"Startup is hidden");
        form.Show();System.Windows.Forms.Application.DoEvents();
        Assert(form.Visible,"First tray restore shows window");
        var language=(LanguagePicker)form.Controls.Find("Language",true)[0];
        var minimize=form.Controls.Find("MinimizeTray",true)[0];
        var domains=(System.Windows.Forms.TextBox)form.Controls.Find("ProtectedDomains",true)[0];
        var interval=(IntervalInput)form.Controls.Find("PollSeconds",true)[0];
        domains.Text="example.com\npending.example";interval.Value=42;
        language.SelectedIndex=1;language.SelectedIndex=0;
        Assert(minimize.Text=="Minimize to tray","Language selector updates existing controls to English");
        Assert(domains.Text=="example.com\npending.example" && interval.Value==42,"Language switch preserves unsaved sites and interval");
        language.SelectedIndex=1;
        Assert(minimize.Text=="트레이로 최소화","Language selector switches back to Korean without restarting");
        var tray=(System.Windows.Forms.NotifyIcon)typeof(MainForm).GetField("tray",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).GetValue(form);
        Assert(tray.ContextMenuStrip.Items[0].Text=="창 열기","Tray menu follows the selected language");
        form.WindowState=System.Windows.Forms.FormWindowState.Minimized;System.Windows.Forms.Application.DoEvents();
        Assert(!form.Visible,"Minimize hides window");
        form.Show();form.WindowState=System.Windows.Forms.FormWindowState.Normal;System.Windows.Forms.Application.DoEvents();
        form.Close();System.Windows.Forms.Application.DoEvents();
        Assert(!form.Visible && !form.IsDisposed,"Close hides instead of exits");
        Files.Write(Path.Combine(Files.Data,"ui-test-results.json"),new{passed=passed.Count,tests=passed});return 0;
      }catch(Exception e){Files.Write(Path.Combine(Files.Data,"ui-test-results.json"),new{error=e.ToString(),tests=passed});return 1;}
      finally{if(form!=null)form.Dispose();}
    }
    public static int Run(){
      try{
        var settings=new Settings();
        DateTime t=DateTime.UtcNow;
        var safe=new BrowserState{Safe=true,Reason="clear"};
        var blocked=new BrowserState{Safe=false,Reason="blocked"};
        var off=new NvidiaState{Overlay=true,Enabled=false,Running=false,Hotkey=new[]{18,16,121}};
        var on=new NvidiaState{Overlay=true,Enabled=true,Running=true,RuntimeAt=t.AddSeconds(15)};
        var policy=new RecoveryPolicy();
        Assert(!policy.Evaluate(t,settings,blocked,off).Send,"Protected domain prevents input");
        Assert(!policy.Evaluate(t.AddSeconds(1),settings,safe,off).Send,"Domain closure has grace period");
        Assert(!policy.Evaluate(t.AddSeconds(7),settings,safe,off).Send,"Known-off state is debounced");
        Assert(policy.Evaluate(t.AddSeconds(11),settings,safe,off).Send,"Known-off state recovers after closure");
        policy.AttemptSent(t.AddSeconds(11));
        Assert(!policy.Evaluate(t.AddSeconds(13),settings,safe,off).Send,"No repeated keypress during verification");
        Assert(!policy.Evaluate(t.AddSeconds(16),settings,safe,on).Send,"Already-on state is never toggled off");
        on.Running=false;
        Assert(policy.Evaluate(t.AddSeconds(17),settings,safe,on).Status.Contains("실제 녹화"),"Persisted on is not reported as running");
        off.Enabled=null;
        Assert(!policy.Evaluate(t.AddSeconds(60),settings,safe,off).Send,"Unknown registry state never triggers a blind toggle");off.Enabled=false;
        settings.Enabled=false;
        Assert(!policy.Evaluate(t.AddSeconds(65),settings,safe,off).Send,"Explicit user pause suppresses recovery");settings.Enabled=true;
        var retry=new RecoveryPolicy();retry.Evaluate(t,settings,safe,off);retry.Evaluate(t.AddSeconds(6),settings,safe,off);retry.AttemptSent(t.AddSeconds(10));
        Assert(!retry.Evaluate(t.AddSeconds(26),settings,safe,off).Send,"Failed attempt enters backoff");
        Assert(!retry.Evaluate(t.AddSeconds(40),settings,safe,off).Send,"Backoff is respected");
        Assert(retry.Evaluate(t.AddSeconds(57),settings,safe,off).Send,"Recovery retries after backoff");
        Assert(!retry.Evaluate(t.AddSeconds(58),settings,blocked,off).Send,"New blocked tab preempts retry");
        Assert(Settings.NormalizeDomain("HTTPS://WWW.NETFLIX.COM/browse")=="netflix.com","Pasted URL normalized");
        Assert(Settings.NormalizeDomain("*.coupangplay.com")=="coupangplay.com","Wildcard normalized to base domain");
        bool invalid=false;try{Settings.NormalizeDomain("not a domain");}catch(ArgumentException){invalid=true;}Assert(invalid,"Invalid domain rejected");
        var report=new BrowserReport{Ready=true,PolicyVersion=settings.PolicyVersion,SeenUtc=t.ToString("o")};
        Assert(BrowserState.IsFresh(report,settings,t.AddSeconds(10)),"Fresh browser report accepted");
        Assert(!BrowserState.IsFresh(report,settings,t.AddSeconds(46)),"Stale browser report rejected");
        report.PolicyVersion="old";
        Assert(!BrowserState.IsFresh(report,settings,t.AddSeconds(10)),"Old domain policy cannot authorize capture");
        var noExtension=BrowserState.FromReports(settings,2,new BrowserReport[0],false,t);
        Assert(noExtension.Safe && noExtension.WebCheckSkipped && noExtension.Connections==0,"Open Chrome without extension skips web check and allows recovery");
        var closedChrome=BrowserState.FromReports(settings,0,new BrowserReport[0],false,t);
        Assert(closedChrome.Safe && !closedChrome.WebCheckSkipped,"Closed Chrome remains eligible without an extension");
        var liveReport=new BrowserReport{Ready=true,PolicyVersion=settings.PolicyVersion,SeenUtc=t.ToString("o"),WindowCount=2};
        var connected=BrowserState.FromReports(settings,2,new[]{liveReport},false,t);
        Assert(connected.Safe && !connected.WebCheckSkipped && connected.Connections==1,"Connected extension keeps web checks enabled");
        liveReport.BlockedDomains=new[]{"netflix.com"};
        var protectedTab=BrowserState.FromReports(settings,2,new[]{liveReport},false,t);
        Assert(!protectedTab.Safe && !protectedTab.WebCheckSkipped && protectedTab.BlockedDomains.Contains("netflix.com"),"Connected Netflix report still blocks recovery");
        var expired=BrowserState.FromReports(settings,2,new[]{liveReport},false,t.AddSeconds(46));
        Assert(expired.Safe && expired.WebCheckSkipped && expired.BlockedDomains.Length==0,"Expired extension cannot keep a stale protected tab blocking fallback");
        liveReport.BlockedDomains=new string[0];liveReport.PolicyVersion="old";
        Assert(!BrowserState.FromReports(settings,2,new[]{liveReport},false,t).Safe,"Live extension policy sync does not bypass web checks");
        liveReport.PolicyVersion=settings.PolicyVersion;liveReport.Ready=false;
        Assert(!BrowserState.FromReports(settings,2,new[]{liveReport},false,t).Safe,"Connected extension initial report is awaited");
        var fallbackPolicy=new RecoveryPolicy();fallbackPolicy.Evaluate(t,settings,noExtension,off);fallbackPolicy.Evaluate(t.AddSeconds(6),settings,noExtension,off);
        Assert(fallbackPolicy.Evaluate(t.AddSeconds(10),settings,noExtension,off).Send,"Known-off replay recovers on periodic checks without an extension");
        settings.Enabled=false;
        Assert(!fallbackPolicy.Evaluate(t.AddSeconds(12),settings,noExtension,off).Send,"User pause still prevents extension-free recovery");settings.Enabled=true;
        Assert(!fallbackPolicy.Evaluate(t.AddSeconds(13),settings,protectedTab,off).Send,"Reconnected extension with protected tab preempts fallback recovery");
        var en=new Localizer("en-US");var ko=new Localizer("ko-KR");
        Assert(en.Translate("사이트 종료 확인 · 5초 후 감시 재개")=="Site check clear · Resuming in 5 seconds","Countdown localization preserves the dynamic number");
        Assert(en.Translate("NVIDIA 상태 확인 불가 · 로그인 사용자 NVIDIA 설정 없음")=="NVIDIA status unavailable · NVIDIA settings were not found for this Windows user","Nested runtime errors are localized");
        Assert(ko.Translate("즉시 리플레이가 켜져 있어요")=="즉시 리플레이가 켜져 있어요","Korean messages remain intact");
        var languageSettings=new Settings{Enabled=false,PollSeconds=42,Language="en",Domains=new[]{"example.com"}};
        var restored=new System.Web.Script.Serialization.JavaScriptSerializer().Deserialize<Settings>(Files.Json(languageSettings));
        Assert(restored.Language=="en" && restored.PollSeconds==42 && !restored.Enabled && restored.Domains[0]=="example.com","Saved language round-trips together with existing settings");
        string domainPolicy=languageSettings.PolicyVersion;languageSettings.Language="ko";
        Assert(languageSettings.PolicyVersion==domainPolicy,"Language changes do not invalidate browser domain policy");
        var legacy=new System.Web.Script.Serialization.JavaScriptSerializer().Deserialize<Settings>("{\"Enabled\":true,\"Domains\":[\"netflix.com\"],\"PollSeconds\":15}");
        Assert(legacy.Language==Localizer.DefaultLanguage && legacy.PollSeconds==15,"Legacy settings acquire the system language without losing the interval");
        var log=new RuntimeLog();
        log.Feed("2026-09-29 11:00:00.000 INFO  ShadowPlayService  IR Started ");Assert(log.Running==true,"Runtime start parsed");
        log.Feed("2026-09-29 11:01:00.000 INFO  ShadowPlayService  Protected content notification ");
        log.Feed("2026-09-29 11:01:00.010 INFO  ShadowPlayService  Capture Event: {");
        log.Feed("\"captureMode\": \"dvr\",");log.Feed("\"recordingState\": \"available\",");log.Feed("}");
        Assert(log.Running==false && log.ProtectedAt!=DateTime.MinValue,"DRM event invalidates runtime state");
        log.Feed("2026-09-29 11:02:00.000 INFO  ShadowPlayService  GetInstantReplayRunningStatus returns: {");log.Feed("\"status\": true,");log.Feed("}");
        Assert(log.Running==true,"Runtime status query parsed");
        log.Feed("2026-09-29 10:59:00.000 INFO  ShadowPlayService  IR Disabled ");Assert(log.Running==true,"Older log event cannot overwrite newer state");
        Assert(NvidiaProbe.Number(new byte[]{1,0,0,0})==1 && NvidiaProbe.Number(new byte[]{1})==null,"Registry binary type validated");
        using(var ms=new MemoryStream()){
          NativeHost.WriteFrame(ms,new{type="report",value="한국어"});ms.Position=0;
          Assert(Encoding.UTF8.GetString(NativeHost.ReadFrame(ms)).Contains("한국어"),"Native framing preserves UTF-8");
          Assert(NativeHost.ReadFrame(ms)==null,"Clean pipe EOF exits");
        }
        invalid=false;try{NativeHost.ReadFrame(new MemoryStream(new byte[]{255,255,255,127}));}catch(InvalidDataException){invalid=true;}Assert(invalid,"Oversized native message rejected");
        Files.Write(Path.Combine(Files.Data,"test-results.json"),new{passed=passed.Count,tests=passed});return 0;
      }catch(Exception e){Files.Write(Path.Combine(Files.Data,"test-results.json"),new{passed=passed.Count,error=e.ToString(),tests=passed});return 1;}
    }
  }
}
