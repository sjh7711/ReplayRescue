using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Collections.Generic;
using System.Web.Script.Serialization;
using System.Globalization;

namespace ReplayRescue {
  public static class Files {
    public static readonly string Root = AppDomain.CurrentDomain.BaseDirectory;
    public static readonly string Data = Path.Combine(Root, "data");
    public static string Json(object value) { return new JavaScriptSerializer().Serialize(value); }
    public static T Read<T>(string path) { return new JavaScriptSerializer().Deserialize<T>(ReadShared(path)); }
    public static string ReadShared(string path) {
      using (var f = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
      using (var r = new StreamReader(f, Encoding.UTF8)) return r.ReadToEnd();
    }
    public static void Write(string path, object value) {
      Directory.CreateDirectory(Path.GetDirectoryName(path));
      string tmp = path + "." + System.Diagnostics.Process.GetCurrentProcess().Id + ".tmp";
      File.WriteAllText(tmp, Json(value), new UTF8Encoding(false));
      if (File.Exists(path)) File.Replace(tmp, path, null); else File.Move(tmp, path);
    }
    public static void Log(string message) {
      try {
        Directory.CreateDirectory(Data);
        string p = Path.Combine(Data, "activity.log");
        if (File.Exists(p) && new FileInfo(p).Length > 1024 * 1024) {
          File.Copy(p, p + ".previous", true); File.WriteAllText(p, "");
        }
        File.AppendAllText(p, DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + " " + message + Environment.NewLine, Encoding.UTF8);
      } catch { }
    }
  }
  public class Settings {
    public const int MinPollSeconds = 1;
    public const int MaxPollSeconds = 300;
    public bool Enabled = true;
    public string Language = Localizer.DefaultLanguage;
    public bool CheckUpdates = true;
    public string[] Domains = new string[] { "netflix.com", "coupangplay.com" };
    public int PollSeconds = 2;
    public int ResumeDelaySeconds = 5;
    public string PolicyVersion { get { return string.Join("|", Domains.OrderBy(x => x, StringComparer.Ordinal)); } }
    public static string PathName { get { return Path.Combine(Files.Data, "settings.json"); } }
    public static Settings Load() {
      if (!File.Exists(PathName)) return new Settings();
      Settings s = Files.Read<Settings>(PathName);
      if (s == null || s.Domains == null) throw new InvalidDataException("설정 파일을 읽을 수 없습니다.");
      s.Domains = s.Domains.Select(NormalizeDomain).Distinct().ToArray();
      s.Language = Localizer.Normalize(s.Language);
      s.PollSeconds = Math.Max(MinPollSeconds, Math.Min(MaxPollSeconds, s.PollSeconds));
      s.ResumeDelaySeconds = Math.Max(3, Math.Min(60, s.ResumeDelaySeconds));
      return s;
    }
    public static string NormalizeDomain(string input) {
      string d = input.Trim().ToLowerInvariant();
      if (d.StartsWith("*.")) d = d.Substring(2);
      if (d.Contains("://")) {
        Uri u;
        if (!Uri.TryCreate(d, UriKind.Absolute, out u) || (u.Scheme != "http" && u.Scheme != "https")) throw new ArgumentException("웹사이트 도메인만 입력하세요: " + input);
        d = u.DnsSafeHost;
      }
      d = d.TrimEnd('.');
      if (d.StartsWith("www.")) d = d.Substring(4);
      try { d = new IdnMapping().GetAscii(d); } catch { throw new ArgumentException("도메인 형식이 올바르지 않습니다: " + input); }
      if (d.Length > 253 || !d.Contains('.') || d.Split('.').Any(x => x.Length == 0 || x.Length > 63 || !System.Text.RegularExpressions.Regex.IsMatch(x, @"^[a-z0-9](?:[a-z0-9-]*[a-z0-9])?$"))) throw new ArgumentException("도메인 형식이 올바르지 않습니다: " + input);
      return d;
    }
  }
  public class BrowserReport {
    public string ExtensionVersion;
    public int Pid;
    public string SeenUtc;
    public bool Ready;
    public string PolicyVersion;
    public string[] BlockedDomains = new string[0];
    public int WindowCount;
    public bool IncognitoAccess;
  }
  public class BrowserState {
    public bool Safe;
    public bool WebCheckSkipped;
    public int Connections;
    public bool IncognitoAccess;
    public string Reason;
    public string[] BlockedDomains = new string[0];
    public static BrowserState Read(Settings settings, int chromeWindowCount) {
      var reports = new List<BrowserReport>();
      bool unknown = false;
      if (Directory.Exists(Files.Data)) foreach (string p in Directory.GetFiles(Files.Data, "browser-*.json")) {
        try {
          BrowserReport r = Files.Read<BrowserReport>(p);
          if (r == null) { unknown = true; continue; }
          // A disconnected native host cannot keep an old Netflix report blocking forever.
          if (!Native.IsOurProcess(r.Pid)) continue;
          reports.Add(r);
        } catch { unknown = true; }
      }
      return FromReports(settings,chromeWindowCount,reports,unknown,DateTime.UtcNow);
    }
    public static BrowserState FromReports(Settings settings,int chromeWindowCount,IEnumerable<BrowserReport> reports,bool unreadable,DateTime now) {
      // An absent or expired extension is optional. A live extension still guards
      // protected tabs, including while an updated domain policy is syncing.
      var recent=reports.Where(r=>r!=null && IsRecent(r,now)).ToArray();
      var blocked=recent.SelectMany(r=>r.BlockedDomains ?? new string[0]).Where(d=>settings.Domains.Contains(d)).Distinct().ToArray();
      var result=new BrowserState{Connections=recent.Length,IncognitoAccess=recent.Length>0 && recent.All(r=>r.IncognitoAccess),BlockedDomains=blocked};
      if(blocked.Length>0){result.Reason="보호 사이트가 열려 있어 대기 중";return result;}
      if(chromeWindowCount==0 && recent.All(r=>r.WindowCount==0)){result.Safe=true;result.Reason="열린 Chrome 창 없음";return result;}
      if(recent.Length==0){result.Safe=true;result.WebCheckSkipped=true;result.Reason="Chrome 확장 미연결 · 웹 체크 생략";return result;}
      result.Safe=!unreadable && recent.All(r=>IsFresh(r,settings,now));
      result.Reason=result.Safe?"보호 사이트 없음":"Chrome 확장 연결 / 최신 탭 확인 대기";
      return result;
    }
    public static bool IsFresh(BrowserReport r, Settings s, DateTime now) {
      return r.Ready && r.PolicyVersion==s.PolicyVersion && IsRecent(r,now);
    }
    static bool IsRecent(BrowserReport r,DateTime now) {
      DateTime t;
      return DateTime.TryParse(r.SeenUtc,CultureInfo.InvariantCulture,DateTimeStyles.RoundtripKind,out t) && (now-t.ToUniversalTime()).TotalSeconds>=-5 && (now-t.ToUniversalTime()).TotalSeconds<=45;
    }
  }
  public class NvidiaState {
    public bool? Enabled;
    public bool? Running;
    public bool? DesktopCapture;
    public DateTime EnabledEventAt;
    public DateTime RuntimeAt;
    public DateTime ProtectedAt;
    public bool Overlay;
    public int[] Hotkey;
    public string Error;
  }
  public class Decision {
    public bool Send;
    public string Status;
  }
  // All timestamps are UTC; decisions are deterministic and independently testable.
  public class RecoveryPolicy {
    DateTime safeAfter = DateTime.MinValue, offSince = DateTime.MinValue, nextTry = DateTime.MinValue;
    DateTime attempt = DateTime.MinValue;
    int failures;
    bool wasBlocked = true;
    public void RetryNow() { nextTry=DateTime.MinValue; failures=0; }
    public void AttemptSent(DateTime now) { attempt=now; nextTry=now.AddSeconds(15); }
    public Decision Evaluate(DateTime now, Settings s, BrowserState b, NvidiaState n) {
      if (!s.Enabled) { wasBlocked=true; return new Decision { Status="자동 복구 일시 정지" }; }
      if (!b.Safe) { wasBlocked=true; offSince=DateTime.MinValue; attempt=DateTime.MinValue; return new Decision { Status=b.Reason }; }
      if (wasBlocked) { safeAfter=now.AddSeconds(s.ResumeDelaySeconds); nextTry=safeAfter; wasBlocked=false; failures=0; }
      if (now < safeAfter) return new Decision { Status="사이트 종료 확인 · " + Math.Ceiling((safeAfter-now).TotalSeconds) + "초 후 감시 재개" };
      if (!n.Overlay) return new Decision { Status="NVIDIA 오버레이 실행 대기" };
      if (!n.Enabled.HasValue) return new Decision { Status="NVIDIA 상태 확인 불가 · " + n.Error };
      if (n.Enabled.Value) {
        offSince=DateTime.MinValue;
        if (n.Running == true && (attempt == DateTime.MinValue || n.RuntimeAt >= attempt.AddSeconds(-1))) {
          failures=0; attempt=DateTime.MinValue;
          return new Decision { Status="즉시 리플레이 켜짐 · 녹화 시작 확인" };
        }
        if(n.DesktopCapture==false) return new Decision { Status="즉시 리플레이 켜짐 · 게임 실행 대기" };
        return new Decision { Status="설정 켜짐 · 실제 녹화 시작 확인 대기" };
      }
      if (offSince == DateTime.MinValue) offSince=now;
      if (now < nextTry) return new Decision { Status="복구 확인 / 재시도 대기 · " + Math.Ceiling((nextTry-now).TotalSeconds) + "초" };
      if (attempt != DateTime.MinValue) { failures++; attempt=DateTime.MinValue; nextTry=now.AddSeconds(Math.Min(300,15*Math.Pow(2,Math.Min(5,failures)))); return new Decision { Status="활성화 미확인 · 재시도 간격 조정" }; }
      if (n.ProtectedAt > now.AddSeconds(-20)) return new Decision { Status="NVIDIA 보호 콘텐츠 알림 · 해제 대기" };
      if ((now-offSince).TotalSeconds < 3) return new Decision { Status="꺼짐 상태 재확인 중" };
      if (n.Hotkey == null || n.Hotkey.Length == 0) return new Decision { Status="NVIDIA 즉시 리플레이 전환 단축키가 필요합니다" };
      return new Decision { Send=true, Status="즉시 리플레이 다시 켜는 중" };
    }
  }
}
