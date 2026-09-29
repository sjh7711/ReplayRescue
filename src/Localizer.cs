using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.RegularExpressions;

namespace ReplayRescue {
  // Keep policy/diagnostic messages stable; translate only at the presentation boundary.
  public sealed class Localizer {
    public string Language { get; private set; }
    public static string DefaultLanguage { get {return CultureInfo.CurrentUICulture.TwoLetterISOLanguageName=="ko"?"ko":"en";} }
    public static string Normalize(string language){if(string.IsNullOrEmpty(language))return DefaultLanguage;return language.StartsWith("ko",StringComparison.OrdinalIgnoreCase)?"ko":language.StartsWith("en",StringComparison.OrdinalIgnoreCase)?"en":DefaultLanguage;}
    public Localizer(string language){Language=Normalize(language);}
    public static Localizer ForSettings(){try{return new Localizer(Settings.Load().Language);}catch{return new Localizer(DefaultLanguage);}}
    static readonly Dictionary<string,string> English=new Dictionary<string,string>{
      {"NVIDIA Instant Replay · 자동 복구","NVIDIA Instant Replay · Automatic recovery"},
      {"트레이로 최소화","Minimize to tray"},
      {"상태를 확인하고 있어요","Checking replay status"},
      {"NVIDIA와 Chrome 연결 상태를 확인합니다.","Checking NVIDIA and the Chrome connection."},
      {"일시 정지","Pause"},{"감시 재개","Resume"},{"지금 확인","Check now"},
      {"NVIDIA 설정","NVIDIA"},{"실제 녹화","Recording"},{"확인 중","Checking"},
      {"보호 사이트","Protected sites"},{"{0}개 등록","{0} saved"},
      {"사이트가 열려 있으면 자동 복구를 잠시 멈춥니다.","Pause recovery while these sites are open."},
      {"보호 사이트 목록, 한 줄에 도메인 하나","Protected sites, one domain per line"},
      {"목록 저장","Save sites"},{"한 줄에 하나 · 하위 도메인 포함","One per line · Includes subdomains"},
      {"변경 후 목록을 저장해 주세요","Save to apply your changes"},
      {"저장했어요 · 다음 확인부터 적용","Saved · Applies on the next check"},
      {"자동 복구 설정","Recovery settings"},{"{0}초마다 상태를 확인합니다.","Checking every {0} seconds."},
      {"확인 간격","Check interval"},{"1–300초","1–300 sec"},{"초","sec"},{"적용","Apply"},
      {"Windows와 함께 시작","Start with Windows"},{"로그인하면 트레이에서 실행","Run in the tray when you sign in"},
      {"Windows 로그인 시 자동 실행","Start automatically when signing in to Windows"},
      {"Windows 자동 실행 켜짐","Windows startup enabled"},{"Windows 자동 실행 꺼짐","Windows startup disabled"},
      {"자동 실행 설정","Startup settings"},
      {"한 줄에 도메인 하나씩 입력하세요. URL을 붙여 넣어도 됩니다.\n목록이 길면 마우스 휠이나 방향키로 이동할 수 있습니다.","Enter one domain per line. You can also paste a URL.\nUse the mouse wheel or arrow keys to scroll a long list."},
      {"Windows 로그인 시 창을 띄우지 않고 트레이에서 시작합니다.","Start in the tray without opening a window when you sign in."},
      {"1~300초를 직접 입력하거나 − / + 버튼으로 조절하세요.","Enter 1–300 seconds, or adjust with the − / + buttons."},
      {"최근 활동","Recent activity"},{"최근 활동: {0}","Recent activity: {0}"},
      {"진단 저장","Save diagnostics"},{"전체 로그 ↗","Full log ↗"},
      {"창을 닫아도 트레이에서 계속 실행됩니다.","Closing the window keeps Replay Rescue running in the tray."},
      {"Chrome 확장","Chrome extension"},{"설치 안내 ↗","Setup guide ↗"},
      {"Replay Rescue · 즉시 리플레이 확인 중","Replay Rescue · Checking Instant Replay"},
      {"창 열기","Open window"},{"자동 복구 일시 정지 / 재개","Pause / resume automatic recovery"},
      {"지금 상태 확인","Check status now"},{"완전히 종료","Exit Replay Rescue"},
      {"앱 시작 · 자동 복구 켜짐","App started · Automatic recovery enabled"},
      {"앱 시작 · 일시 정지","App started · Recovery paused"},
      {"도메인 목록 저장 · {0}개","Saved {0} protected sites"},{"도메인 확인","Check domain"},
      {"확인 간격 저장 오류","Could not save interval"},{"확인 간격 적용 · {0}초","Check interval set to {0} seconds"},
      {"{0}초마다 상태 확인 · 설정은 자동으로 유지됩니다.","Checking every {0} seconds · Saved for next time"},
      {"자동 복구를 잠시 멈췄어요","Automatic recovery is paused"},
      {"보호 사이트가 열려 있어요","A protected site is open"},
      {"Chrome 상태를 확인해 주세요","Check the Chrome connection"},
      {"즉시 리플레이가 켜져 있어요","Instant Replay is on"},
      {"게임 녹화를 기다리고 있어요","Waiting for game capture"},
      {"즉시 리플레이 복구 대기 중","Waiting to restore Instant Replay"},
      {"리플레이 상태를 확인해 주세요","Check the Instant Replay status"},
      {"자동 복구 대기 · {0}","Recovery paused · {0}"},{"웹 체크 생략","Site check off"},
      {"켜짐","On"},{"꺼짐","Off"},{"확인 불가","Unknown"},{"녹화 중","Recording"},{"대기 중","Waiting"},
      {"연결됨 · {0}개","Connected · {0}"},{"열린 창 없음","No open windows"},
      {"NVIDIA 설정: {0}\n전환 단축키: {1}","NVIDIA setting: {0}\nToggle shortcut: {1}"},
      {"NVIDIA 녹화 시작 기록을 별도로 확인합니다.\n데스크톱 캡처가 꺼져 있으면 게임 실행을 기다립니다.","Recording is confirmed separately using NVIDIA's runtime log.\nWith desktop capture off, recording waits for a game."},
      {"확장이 연결되면 보호 사이트를 확인합니다.\n미연결 시 웹 체크 없이 설정한 간격으로 복구합니다.\n시크릿 창은 확장의 '시크릿 모드에서 허용'이 필요합니다.","Connected: checks for protected sites.\nDisconnected: restores replay at your interval without checking sites.\nFor incognito windows, enable 'Allow in incognito' for the extension."},
      {"설정 저장 오류","Could not save settings"},{"사용자: 감시 재개","User resumed automatic recovery"},
      {"사용자: 감시 일시 정지","User paused automatic recovery"},
      {"Replay Rescue · 즉시 리플레이 {0}","Replay Rescue · Instant Replay {0}"},
      {"입력 직전 상태가 변경되어 다음 주기에 재확인","State changed before the shortcut; checking again next cycle"},
      {"다시 켜기 명령 전송 · {0} · 실제 시작 확인 대기","Sent {0} to enable replay · Waiting for recording confirmation"},
      {"Chrome 확장 미연결 · 웹 체크 생략","Chrome extension disconnected · Site check off"},
      {"Chrome 확장 연결 · 보호 사이트 확인 재개","Chrome extension connected · Site checks resumed"},
      {"확인 중 오류 · 다음 주기에 재시도","Status check failed · Retrying next cycle"},
      {"진단 저장: diagnostic.json","Diagnostics saved: diagnostic.json"},
      {"확인 간격 (초)","Check interval in seconds"},{"확인 간격, 1부터 300초","Check interval, from 1 to 300 seconds"},
      {"확인 간격은 1~300초 사이로 입력해 주세요.","Enter a check interval from 1 to 300 seconds."},
      {"언어 설정 저장 오류","Could not save language"},{"언어 변경 · English","Language changed · English"},
      {"언어 변경 · 한국어","Language changed · Korean"},
      {"Replay Rescue가 이미 실행 중입니다.\n작업 표시줄 오른쪽 트레이 아이콘을 두 번 클릭하세요.","Replay Rescue is already running.\nDouble-click its icon in the Windows system tray."},
      {"Replay Rescue 오류","Replay Rescue error"},
      {"데스크톱 잠금 / 다른 보안 화면으로 입력 보류","Shortcut deferred: desktop locked or secure screen active"},
      {"단축키 형식 오류","Invalid shortcut format"},
      {"사용자가 키를 누르고 있어 다음 주기에 재시도","A key is being held down; retrying next cycle"},
      {"키 입력 실패 (Windows 권한 또는 입력 데스크톱 확인 필요)","Shortcut failed: check Windows permissions or the active desktop"},
      {"없음","None"},{"로그인 사용자 NVIDIA 설정 없음","NVIDIA settings were not found for this Windows user"},
      {"알 수 없는 상태값","Unrecognized status value"},{"설정 파일을 읽을 수 없습니다.","The settings file could not be read."},
      {"웹사이트 도메인만 입력하세요: {0}","Enter a website domain: {0}"},
      {"도메인 형식이 올바르지 않습니다: {0}","Invalid domain: {0}"},
      {"보호 사이트가 열려 있어 대기 중","Recovery paused while a protected site is open"},
      {"열린 Chrome 창 없음","No open Chrome windows"},{"보호 사이트 없음","No protected sites open"},
      {"Chrome 확장 연결 / 최신 탭 확인 대기","Waiting for the extension's latest tab report"},
      {"자동 복구 일시 정지","Automatic recovery paused"},
      {"사이트 종료 확인 · {0}초 후 감시 재개","Site check clear · Resuming in {0} seconds"},
      {"NVIDIA 오버레이 실행 대기","Waiting for NVIDIA Overlay"},
      {"NVIDIA 상태 확인 불가 · {0}","NVIDIA status unavailable · {0}"},
      {"즉시 리플레이 켜짐 · 녹화 시작 확인","Instant Replay enabled · Recording confirmed"},
      {"즉시 리플레이 켜짐 · 게임 실행 대기","Instant Replay enabled · Waiting for a game"},
      {"설정 켜짐 · 실제 녹화 시작 확인 대기","Replay enabled · Waiting for recording confirmation"},
      {"복구 확인 / 재시도 대기 · {0}초","Waiting for recovery / retry · {0} seconds"},
      {"활성화 미확인 · 재시도 간격 조정","Recovery not confirmed · Adjusting retry interval"},
      {"NVIDIA 보호 콘텐츠 알림 · 해제 대기","NVIDIA reported protected content · Waiting for it to clear"},
      {"꺼짐 상태 재확인 중","Confirming that Instant Replay is off"},
      {"NVIDIA 즉시 리플레이 전환 단축키가 필요합니다","Set an NVIDIA Instant Replay toggle shortcut"},
      {"즉시 리플레이 다시 켜는 중","Turning Instant Replay back on"}
    };
    sealed class Template {public Regex Pattern;public string English;public int Count;}
    static readonly List<Template> Templates=BuildTemplates();
    static List<Template> BuildTemplates(){var result=new List<Template>();foreach(var pair in English){if(!pair.Key.Contains("{0}"))continue;string pattern=Regex.Escape(pair.Key);int count=0;while(pair.Key.Contains("{"+count+"}")){pattern=pattern.Replace(Regex.Escape("{"+count+"}"),"(?<p"+count+">.*?)");count++;}result.Add(new Template{Pattern=new Regex("^"+pattern+"$",RegexOptions.Singleline),English=pair.Value,Count=count});}return result;}
    public string Translate(string original){
      if(Language=="ko" || string.IsNullOrEmpty(original))return original;
      string english;if(English.TryGetValue(original,out english))return english;
      foreach(var template in Templates){var match=template.Pattern.Match(original);if(!match.Success)continue;var args=new object[template.Count];for(int i=0;i<args.Length;i++)args[i]=Translate(match.Groups["p"+i].Value);return string.Format(CultureInfo.InvariantCulture,template.English,args);}
      int separator=original.IndexOf(": ",StringComparison.Ordinal);if(separator>0 && original.Substring(0,separator).EndsWith("Exception",StringComparison.Ordinal))return original.Substring(0,separator+2)+Translate(original.Substring(separator+2));
      return original;
    }
  }
}
