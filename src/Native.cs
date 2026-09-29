using System;
using System.IO;
using System.Text;
using System.Linq;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;
using System.Collections.Generic;
using Microsoft.Win32;
using System.Globalization;
using System.Text.RegularExpressions;

namespace ReplayRescue {
  public static class Native {
    [StructLayout(LayoutKind.Sequential)] struct KEYBDINPUT { public ushort wVk,wScan; public uint dwFlags,time; public UIntPtr dwExtraInfo; }
    [StructLayout(LayoutKind.Sequential)] struct MOUSEINPUT { public int dx,dy; public uint mouseData,dwFlags,time; public UIntPtr dwExtraInfo; }
    [StructLayout(LayoutKind.Explicit)] struct UNION { [FieldOffset(0)] public KEYBDINPUT ki; [FieldOffset(0)] public MOUSEINPUT mi; }
    [StructLayout(LayoutKind.Sequential)] struct INPUT { public uint type; public UNION u; }
    [DllImport("user32.dll", SetLastError=true)] static extern uint SendInput(uint n, INPUT[] p, int cb);
    [DllImport("user32.dll")] static extern short GetAsyncKeyState(int key);
    [DllImport("user32.dll")] static extern IntPtr OpenInputDesktop(uint flags, bool inherit, uint access);
    [DllImport("user32.dll")] static extern bool CloseDesktop(IntPtr h);
    [DllImport("user32.dll", CharSet=CharSet.Unicode)] static extern bool GetUserObjectInformation(IntPtr h, int index, StringBuilder name, int length, out int needed);
    delegate bool EnumWindowsProc(IntPtr h, IntPtr p);
    [DllImport("user32.dll")] static extern bool EnumWindows(EnumWindowsProc callback, IntPtr p);
    [DllImport("user32.dll")] static extern bool IsWindowVisible(IntPtr h);
    [DllImport("user32.dll")] static extern uint GetWindowThreadProcessId(IntPtr h, out uint pid);
    [DllImport("user32.dll", CharSet=CharSet.Unicode)] static extern int GetClassName(IntPtr h, StringBuilder name, int n);
    public static bool DesktopReady() {
      IntPtr h=OpenInputDesktop(0,false,1); if (h==IntPtr.Zero) return false;
      try { int n; var b=new StringBuilder(256); return GetUserObjectInformation(h,2,b,512,out n) && b.ToString().Equals("Default",StringComparison.OrdinalIgnoreCase); }
      finally { CloseDesktop(h); }
    }
    public static bool IsOurProcess(int pid) {
      try { using(var p=Process.GetProcessById(pid)) return p.SessionId==Process.GetCurrentProcess().SessionId && p.ProcessName=="ReplayRescue" && string.Equals(p.MainModule.FileName,Path.Combine(Files.Root,"ReplayRescue.exe"),StringComparison.OrdinalIgnoreCase); }
      catch { return false; }
    }
    public static int ChromeWindowCount() {
      int found=0;
      var ids=new HashSet<int>();
      foreach(var p in Process.GetProcessesByName("chrome")) using(p) { try { if(p.SessionId==Process.GetCurrentProcess().SessionId) ids.Add(p.Id); } catch{} }
      EnumWindows(delegate(IntPtr h,IntPtr extra) {
        if(!IsWindowVisible(h)) return true;
        uint pid; GetWindowThreadProcessId(h,out pid);
        if(!ids.Contains((int)pid)) return true;
        var b=new StringBuilder(256); GetClassName(h,b,b.Capacity);
        if(b.ToString()=="Chrome_WidgetWin_1") found++;
        return true;
      },IntPtr.Zero);
      return found;
    }
    public static bool HasOverlay() {
      foreach(var p in Process.GetProcessesByName("NVIDIA Overlay")) using(p) { try { if(p.SessionId==Process.GetCurrentProcess().SessionId) return true; } catch{} }
      return false;
    }
    public static string SendHotkey(int[] keys) {
      if(!DesktopReady()) return "데스크톱 잠금 / 다른 보안 화면으로 입력 보류";
      if(keys==null || keys.Length<2 || keys.Length>8 || keys.Any(x=>x<1 || x>254) || keys.Distinct().Count()!=keys.Length) return "단축키 형식 오류";
      foreach(int k in new int[]{16,17,18,91,92}.Concat(keys).Distinct()) if((GetAsyncKeyState(k)&0x8000)!=0) return "사용자가 키를 누르고 있어 다음 주기에 재시도";
      var down=keys.Select(k=>new INPUT { type=1, u=new UNION {ki=new KEYBDINPUT {wVk=(ushort)k}} }).ToArray();
      var up=keys.Reverse().Select(k=>new INPUT {type=1,u=new UNION {ki=new KEYBDINPUT {wVk=(ushort)k,dwFlags=2}}}).ToArray();
      uint sent=SendInput((uint)down.Length,down,Marshal.SizeOf(typeof(INPUT)));
      try { if(sent==down.Length) Thread.Sleep(100); }
      finally { SendInput((uint)up.Length,up,Marshal.SizeOf(typeof(INPUT))); }
      return sent==down.Length ? null : "키 입력 실패 (Windows 권한 또는 입력 데스크톱 확인 필요)";
    }
    public static string HotkeyName(int[] keys) {
      if(keys==null || keys.Length==0) return "없음";
      return string.Join("+",keys.Select(k=>k==18?"Alt":k==17?"Ctrl":k==16?"Shift":((System.Windows.Forms.Keys)k).ToString()));
    }
  }
  public class RuntimeLog {
    public bool? Running;
    public DateTime EnabledAt=DateTime.MinValue;
    public DateTime RuntimeAt=DateTime.MinValue, ProtectedAt=DateTime.MinValue;
    long position=-1;
    DateTime created;
    string pending="";
    DateTime entryTime;
    string block="", kind="";
    public string Error;
    static readonly Regex Header=new Regex(@"^(\d{4}-\d{2}-\d{2} \d{2}:\d{2}:\d{2}\.\d{3}) (?:INFO|WARN|ERROR) ",RegexOptions.Compiled);
    public void Poll() {
      try {
        string p=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"NVIDIA Corporation","NVIDIA Overlay","console.log");
        DateTime c=File.GetCreationTimeUtc(p);
        using(var f=new FileStream(p,FileMode.Open,FileAccess.Read,FileShare.ReadWrite|FileShare.Delete)) {
          if(position<0 || f.Length<position || c!=created) { position=Math.Max(0,f.Length-512*1024); pending=""; kind=""; Running=null; RuntimeAt=DateTime.MinValue; EnabledAt=DateTime.MinValue; created=c; }
          f.Position=position;
          byte[] bytes=new byte[(int)Math.Min(1024*1024,f.Length-position)];
          int count=f.Read(bytes,0,bytes.Length); position+=count;
          string text=pending+Encoding.UTF8.GetString(bytes,0,count);
          int end=text.LastIndexOf('\n');
          if(end>=0) { foreach(string line in text.Substring(0,end).Split('\n')) Feed(line.TrimEnd('\r')); pending=text.Substring(end+1); }
          else pending=text;
          if(pending.Length>65536) pending="";
        }
        Error=null;
      } catch(Exception e) { Error=e.GetType().Name; }
    }
    public void Feed(string line) {
      var m=Header.Match(line);
      if(m.Success) {
        kind=""; block="";
        DateTime local;
        if(!DateTime.TryParseExact(m.Groups[1].Value,"yyyy-MM-dd HH:mm:ss.fff",CultureInfo.InvariantCulture,DateTimeStyles.AssumeLocal,out local)) return;
        entryTime=local.ToUniversalTime();
        if(line.Contains("ShadowPlayService  IR Enabled")) EnabledAt=entryTime;
        else if(line.Contains("ShadowPlayService  IR Started")) Set(true);
        else if(line.Contains("ShadowPlayService  IR Disabled") || line.Contains("ShadowPlayService  IR Stopped")) Set(false);
        else if(line.Contains("ShadowPlayService  Protected content notification")) ProtectedAt=entryTime;
        else if(line.Contains("ShadowPlayService  GetInstantReplayRunningStatus returns:")) kind="status";
        else if(line.Contains("ShadowPlayService  Capture Event:")) kind="capture";
      } else if(kind.Length>0) {
        block+=line;
        if(block.Length>16384) {kind="";return;}
        if(line.Trim().StartsWith("}")) {
          if(kind=="status") { if(Regex.IsMatch(block,@"""status""\s*:\s*true")) Set(true); else if(Regex.IsMatch(block,@"""status""\s*:\s*false")) Set(false); }
          else if(Regex.IsMatch(block,@"""captureMode""\s*:\s*""dvr""")) {
            if(Regex.IsMatch(block,@"""recordingState""\s*:\s*""started""")) Set(true);
            else if(Regex.IsMatch(block,@"""recordingState""\s*:\s*""(?:stopped|disabled|available)""")) Set(false);
          }
          kind="";
        }
      }
    }
    void Set(bool value) { if(entryTime>=RuntimeAt) {Running=value;RuntimeAt=entryTime;} }
  }
  public class NvidiaProbe {
    readonly RuntimeLog log=new RuntimeLog();
    const string RegPath=@"SOFTWARE\NVIDIA Corporation\Global\ShadowPlay\NVSPCAPS";
    const string Active="{1B1D3DAA-601D-49E5-8508-81736CA28C6D}";
    public static int? Number(object value) {
      if(value is byte[] && ((byte[])value).Length==4) return BitConverter.ToInt32((byte[])value,0);
      if(value is int) return (int)value;
      return null;
    }
    public NvidiaState Read() {
      log.Poll();
      var n=new NvidiaState {Overlay=Native.HasOverlay(),Running=log.Running,RuntimeAt=log.RuntimeAt,ProtectedAt=log.ProtectedAt,EnabledEventAt=log.EnabledAt};
      try {
        using(var root=RegistryKey.OpenBaseKey(RegistryHive.CurrentUser,RegistryView.Registry64))
        using(var k=root.OpenSubKey(RegPath)) {
          if(k==null) throw new InvalidOperationException("로그인 사용자 NVIDIA 설정 없음");
          int? value=Number(k.GetValue(Active));
          n.Enabled=value==0?false:value==1?(bool?)true:null;
          int? desktop=Number(k.GetValue("DwmEnabled"));
          n.DesktopCapture=desktop==0?false:desktop==1?(bool?)true:null;
          int? count=Number(k.GetValue("IRToggleHKeyCount"));
          if(count==0)n.Hotkey=new int[0];
          if(count.HasValue && count.Value>=2 && count.Value<=8) {
            var list=new List<int>();
            for(int i=0;i<count.Value;i++) { int? key=Number(k.GetValue("IRToggleHKey"+i)); if(!key.HasValue || key.Value<1 || key.Value>254) {list.Clear();break;} list.Add(key.Value); }
            if(list.Count==count.Value) n.Hotkey=list.ToArray();
          }
          if(!n.Enabled.HasValue) n.Error="알 수 없는 상태값";
        }
      } catch(Exception e) {n.Error=e.Message;}
      // A stale 'started' record predating this overlay launch is not runtime proof.
      DateTime oldest=DateTime.MaxValue;
      foreach(var p in Process.GetProcessesByName("NVIDIA Overlay")) using(p) try { if(p.SessionId==Process.GetCurrentProcess().SessionId && p.StartTime.ToUniversalTime()<oldest) oldest=p.StartTime.ToUniversalTime(); } catch{}
      if(!n.Overlay || n.RuntimeAt<oldest) n.Running=null;
      if(n.Hotkey==null) {
        try {
          string path=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"NVIDIA Corporation","NVIDIA Overlay","ShareSettings.json");
          var j=new System.Web.Script.Serialization.JavaScriptSerializer().DeserializeObject(Files.ReadShared(path)) as Dictionary<string,object>;
          var settings=j["settings"] as Dictionary<string,object>;
          var shortcuts=settings["shortcuts"] as Dictionary<string,object>;
          n.Hotkey=((object[])shortcuts["DVRToggle"]).Select(Convert.ToInt32).ToArray();
        } catch { }
      }
      return n;
    }
  }
}
