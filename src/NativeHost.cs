using System;
using System.IO;
using System.Text;
using System.Linq;
using System.Diagnostics;
using System.Threading;
using System.Collections.Generic;
using System.Web.Script.Serialization;
using System.Globalization;

namespace ReplayRescue {
  public static class NativeHost {
    class MonitorStatus {
      public string at { get; set; }
      public int monitorPid { get; set; }
      public int pollSeconds { get; set; }
      public bool? enabled { get; set; }
    }
    static readonly object outputLock=new object();
    public static byte[] ReadFrame(Stream input) {
      byte[] prefix=new byte[4];
      if(!ReadExact(input,prefix,4,true)) return null;
      int size=BitConverter.ToInt32(prefix,0);
      if(size<2 || size>65536) throw new InvalidDataException("Invalid native message length");
      byte[] body=new byte[size]; ReadExact(input,body,size,false); return body;
    }
    static bool ReadExact(Stream input,byte[] buffer,int size,bool allowEof) {
      int offset=0;
      while(offset<size) {int n=input.Read(buffer,offset,size-offset);if(n==0) {if(allowEof && offset==0)return false;throw new EndOfStreamException();}offset+=n;}
      return true;
    }
    public static void WriteFrame(Stream output,object message) {
      byte[] b=Encoding.UTF8.GetBytes(Files.Json(message));
      lock(outputLock) {output.Write(BitConverter.GetBytes(b.Length),0,4);output.Write(b,0,b.Length);output.Flush();}
    }
    static object PolicyMessage(Settings settings) {
      bool connected=false; bool? enabled=null; string observedAt=null;
      try {
        var snapshot=Files.Read<MonitorStatus>(Path.Combine(Files.Data,"status.json"));
        DateTime at;
        if(snapshot!=null && snapshot.monitorPid!=Process.GetCurrentProcess().Id && Native.IsOurProcess(snapshot.monitorPid) && DateTime.TryParse(snapshot.at,CultureInfo.InvariantCulture,DateTimeStyles.RoundtripKind,out at)) {
          int interval=Math.Max(Settings.MinPollSeconds,Math.Min(Settings.MaxPollSeconds,settings.PollSeconds));
          double age=(DateTime.UtcNow-at.ToUniversalTime()).TotalSeconds;
          connected=age>=-5 && age<=Math.Max(15,interval*2+10);
          if(connected){enabled=snapshot.enabled;observedAt=snapshot.at;}
        }
      }catch { }
      return new {type="policy",domains=settings.Domains,version=settings.PolicyVersion,language=settings.Language,appConnected=connected,instantReplayEnabled=enabled,observedAt=observedAt,pollSeconds=settings.PollSeconds};
    }
    public static int Run() {
      string reportPath=Path.Combine(Files.Data,"browser-"+Process.GetCurrentProcess().Id+".json");
      Timer heartbeat=null;
      try {
        Directory.CreateDirectory(Files.Data);
        // The manifest allowlist restricts callers; no TCP port, token, or web endpoint is exposed.
        var report=new BrowserReport {Pid=Process.GetCurrentProcess().Id,Ready=false,SeenUtc=DateTime.UtcNow.ToString("o")};
        Files.Write(reportPath,report);
        var input=Console.OpenStandardInput(); var output=Console.OpenStandardOutput();
        Settings initial=Settings.Load();
        WriteFrame(output,PolicyMessage(initial));
        heartbeat=new Timer(delegate {
          try {Settings s=Settings.Load();WriteFrame(output,PolicyMessage(s));} catch { }
        },null,5000,5000);
        for(;;) {
          byte[] bytes=ReadFrame(input); if(bytes==null) break;
          var j=new JavaScriptSerializer().DeserializeObject(Encoding.UTF8.GetString(bytes)) as Dictionary<string,object>;
          if(j==null || !j.ContainsKey("type") || Convert.ToString(j["type"])!="report") continue;
          Settings s=Settings.Load();
          var matches=j.ContainsKey("blockedDomains")?j["blockedDomains"] as object[]:null;
          if(matches==null || matches.Length>200) continue;
          report.BlockedDomains=matches.Select(Convert.ToString).Where(d=>s.Domains.Contains(d)).Distinct().ToArray();
          report.PolicyVersion=j.ContainsKey("version")?Convert.ToString(j["version"]):"";
          report.WindowCount=j.ContainsKey("windowCount")?Math.Max(0,Math.Min(1000,Convert.ToInt32(j["windowCount"]))):0;
          report.IncognitoAccess=j.ContainsKey("incognitoAccess") && Convert.ToBoolean(j["incognitoAccess"]);
          report.ExtensionVersion=j.ContainsKey("extensionVersion")?Convert.ToString(j["extensionVersion"]):"unknown";
          report.Ready=true;report.SeenUtc=DateTime.UtcNow.ToString("o");
          Files.Write(reportPath,report);
        }
        return 0;
      } catch { return 1; }
      finally {if(heartbeat!=null)heartbeat.Dispose();try{File.Delete(reportPath);}catch{}}
    }
  }
}
