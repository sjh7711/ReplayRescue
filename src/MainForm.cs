using System;
using System.IO;
using System.Drawing;
using System.Windows.Forms;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Win32;
using System.Collections.Generic;

namespace ReplayRescue {
  public class MainForm : Form {
    readonly NotifyIcon tray;
    readonly Icon trayOnIcon=AppIcon.Create(Theme.Accent);
    readonly Icon trayOffIcon=AppIcon.Create(Color.FromArgb(250,204,74));
    readonly Icon trayUnknownIcon=AppIcon.Create(Color.FromArgb(150,160,173));
    readonly System.Windows.Forms.Timer timer=new System.Windows.Forms.Timer();
    readonly NvidiaProbe probe=new NvidiaProbe();
    readonly RecoveryPolicy policy=new RecoveryPolicy();
    readonly ToolTip tips=new ToolTip{AutoPopDelay=20000,InitialDelay=400,ReshowDelay=150};
    Settings settings;
    Localizer localizer;
    LanguagePicker language;
    readonly List<Action> textBindings=new List<Action>();
    string domainFeedback="한 줄에 하나 · 하위 도메인 포함",lastPolicyStatus="상태를 확인하고 있어요";
    Label status,detail,domainCount,cadence,feedback;
    Signal nvidia,recording,chrome;
    StateGlyph stateGlyph;
    TextBox domains;
    RecentActivity history;
    Button pause;
    CheckBox startup;
    IntervalInput pollSeconds;
    bool busy,exiting,loading=true,lastCheckFailed;
    readonly bool startHidden;
    readonly bool previewOnly;
    string previous="";
    bool? previousWebCheckSkipped;
    NvidiaState lastN;
    BrowserState lastB;
    public MainForm(bool hidden,bool preview=false) {
      startHidden=hidden;
      previewOnly=preview;
      settings=Settings.Load();
      localizer=new Localizer(settings.Language);
      Text="Replay Rescue"; Font=new Font("맑은 고딕",9); BackColor=Theme.Background;ForeColor=Theme.Text;
      ClientSize=new Size(900,788);MinimumSize=new Size(916,827);StartPosition=FormStartPosition.CenterScreen;
      Icon=AppIcon.Create(Theme.Accent); AutoScaleMode=AutoScaleMode.Dpi;AutoScaleDimensions=new SizeF(96,96);
      var root=new TableLayoutPanel {Dock=DockStyle.Fill,Padding=new Padding(28,22,28,16),ColumnCount=1,RowCount=8};
      root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));
      foreach(float h in new float[]{76,174,18,282,18,128,54}) root.RowStyles.Add(new RowStyle(SizeType.Absolute,h));
      root.RowStyles.Add(new RowStyle(SizeType.Percent,100));
      Controls.Add(root);
      var header=new Panel{Dock=DockStyle.Fill,Margin=Padding.Empty};root.Controls.Add(header,0,0);
      header.Controls.Add(new PictureBox{Image=Icon.ToBitmap(),SizeMode=PictureBoxSizeMode.CenterImage,Bounds=new Rectangle(0,2,42,46)});
      header.Controls.Add(LabelAt("Replay Rescue",56,0,330,34,20,true,Theme.Text,"Segoe UI"));
      header.Controls.Add(LabelAt("NVIDIA Instant Replay · 자동 복구",58,38,380,22,9,false,Theme.Muted));
      var minimize=MakeButton("트레이로 최소화",delegate{Hide();},138);minimize.Name="MinimizeTray";minimize.Location=new Point(706,12);minimize.Anchor=AnchorStyles.Top|AnchorStyles.Right;header.Controls.Add(minimize);
      language=new LanguagePicker{Name="Language",Location=new Point(560,12),Size=new Size(132,36),Anchor=AnchorStyles.Top|AnchorStyles.Right};language.SelectedIndex=settings.Language=="ko"?1:0;
      language.SelectedIndexChanged+=delegate{if(!loading)ChangeLanguage(language.SelectedIndex==1?"ko":"en");};header.Controls.Add(language);

      var card=new Surface{Dock=DockStyle.Fill,Margin=Padding.Empty};root.Controls.Add(card,0,1);
      stateGlyph=new StateGlyph{Location=new Point(22,24)};card.Controls.Add(stateGlyph);
      status=LabelAt("상태를 확인하고 있어요",82,23,510,34,18,true,Theme.Text);status.Anchor=AnchorStyles.Top|AnchorStyles.Left|AnchorStyles.Right;card.Controls.Add(status);
      detail=LabelAt("NVIDIA와 Chrome 연결 상태를 확인합니다.",84,64,720,23,9,false,Theme.Muted);detail.Anchor=AnchorStyles.Top|AnchorStyles.Left|AnchorStyles.Right;card.Controls.Add(detail);
      var actions=new FlowLayoutPanel{Location=new Point(604,23),Size=new Size(218,38),Anchor=AnchorStyles.Top|AnchorStyles.Right,WrapContents=false,BackColor=Theme.Card};
      pause=MakeButton(settings.Enabled?"일시 정지":"감시 재개",delegate{TogglePause();},104);actions.Controls.Add(pause);
      actions.Controls.Add(MakeButton("지금 확인",async delegate{policy.RetryNow();await Check();},104));card.Controls.Add(actions);
      card.Controls.Add(Divider(22,107,800));
      var signals=new TableLayoutPanel{Location=new Point(22,116),Size=new Size(800,40),Anchor=AnchorStyles.Left|AnchorStyles.Right|AnchorStyles.Top,ColumnCount=3,RowCount=1,BackColor=Theme.Card,Margin=Padding.Empty};
      signals.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,33));signals.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,33));signals.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,34));
      nvidia=new Signal{Dock=DockStyle.Fill,Margin=Padding.Empty};recording=new Signal{Dock=DockStyle.Fill,Margin=Padding.Empty};chrome=new Signal{Dock=DockStyle.Fill,Margin=Padding.Empty};
      nvidia.Set("NVIDIA 설정","확인 중",Theme.Muted);recording.Set("실제 녹화","확인 중",Theme.Muted);chrome.Set("Chrome","확인 중",Theme.Muted);
      signals.Controls.Add(nvidia,0,0);signals.Controls.Add(recording,1,0);signals.Controls.Add(chrome,2,0);card.Controls.Add(signals);

      var columns=new TableLayoutPanel{Dock=DockStyle.Fill,Margin=Padding.Empty,ColumnCount=3,RowCount=1};columns.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,50));columns.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,18));columns.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,50));root.Controls.Add(columns,0,3);
      var protection=new Surface{Dock=DockStyle.Fill,Margin=Padding.Empty};columns.Controls.Add(protection,0,0);
      protection.Controls.Add(LabelAt("보호 사이트",20,18,250,26,12,true,Theme.Text));
      domainCount=LabelAt(settings.Domains.Length+"개 등록",310,21,82,24,9,false,Theme.Muted);domainCount.TextAlign=ContentAlignment.MiddleRight;domainCount.Anchor=AnchorStyles.Top|AnchorStyles.Right;protection.Controls.Add(domainCount);
      protection.Controls.Add(LabelAt("사이트가 열려 있으면 자동 복구를 잠시 멈춥니다.",20,49,380,24,9,false,Theme.Muted));
      var editor=new Surface{Location=new Point(20,85),Size=new Size(373,116),Anchor=AnchorStyles.Top|AnchorStyles.Left|AnchorStyles.Right,BackColor=Theme.Input,Radius=8,Padding=new Padding(12,10,12,10)};
      domains=new TextBox{Name="ProtectedDomains",Multiline=true,ScrollBars=ScrollBars.None,Dock=DockStyle.Fill,BackColor=Theme.Input,ForeColor=Theme.Text,BorderStyle=BorderStyle.None,Font=new Font("Segoe UI",11),Text=string.Join(Environment.NewLine,settings.Domains),AccessibleName="보호 사이트 목록, 한 줄에 도메인 하나",AcceptsReturn=true};editor.Controls.Add(domains);protection.Controls.Add(editor);
      var save=MakeButton("목록 저장",delegate{SaveDomains();},104);((SoftButton)save).Primary=true;save.Location=new Point(20,223);protection.Controls.Add(save);
      feedback=LabelAt("한 줄에 하나 · 하위 도메인 포함",136,230,257,23,8.5f,false,Theme.Muted);feedback.Anchor=AnchorStyles.Top|AnchorStyles.Left|AnchorStyles.Right;protection.Controls.Add(feedback);
      domains.TextChanged+=delegate{domainFeedback="변경 후 목록을 저장해 주세요";feedback.Text=T(domainFeedback);feedback.ForeColor=Theme.Amber;};

      var preferences=new Surface{Dock=DockStyle.Fill,Margin=Padding.Empty};columns.Controls.Add(preferences,2,0);
      preferences.Controls.Add(LabelAt("자동 복구 설정",20,18,300,26,12,true,Theme.Text));
      cadence=LabelAt(settings.PollSeconds+"초마다 상태를 확인합니다.",20,49,373,24,9,false,Theme.Muted);preferences.Controls.Add(cadence);
      preferences.Controls.Add(LabelAt("확인 간격",20,88,130,22,9,true,Theme.Text));
      var range=LabelAt("1–300초",306,88,86,22,9,false,Theme.Muted);range.TextAlign=ContentAlignment.MiddleRight;range.Anchor=AnchorStyles.Top|AnchorStyles.Right;preferences.Controls.Add(range);
      pollSeconds=new IntervalInput{Name="PollSeconds",Value=settings.PollSeconds,Location=new Point(20,117),Size=new Size(160,38)};preferences.Controls.Add(pollSeconds);
      preferences.Controls.Add(LabelAt("초",190,122,32,28,9,false,Theme.Muted));
      var apply=MakeButton("적용",delegate{ApplyPollInterval();},88);apply.Location=new Point(305,118);apply.Anchor=AnchorStyles.Top|AnchorStyles.Right;preferences.Controls.Add(apply);
      preferences.Controls.Add(Divider(20,180,373));
      preferences.Controls.Add(LabelAt("Windows와 함께 시작",20,204,300,25,10,true,Theme.Text));
      preferences.Controls.Add(LabelAt("로그인하면 트레이에서 실행",20,236,300,22,9,false,Theme.Muted));
      startup=new ToggleSwitch{Checked=StartupEnabled(),Location=new Point(347,212),AccessibleName="Windows 로그인 시 자동 실행",Anchor=AnchorStyles.Top|AnchorStyles.Right};
      startup.CheckedChanged+=delegate{if(!loading)try{SetStartup(startup.Checked);AddHistory(startup.Checked?"Windows 자동 실행 켜짐":"Windows 자동 실행 꺼짐");}catch(Exception e){loading=true;startup.Checked=!startup.Checked;loading=false;MessageBox.Show(this,T(e.Message),T("자동 실행 설정"));}};preferences.Controls.Add(startup);
      BindTip(domains,"한 줄에 도메인 하나씩 입력하세요. URL을 붙여 넣어도 됩니다.\n목록이 길면 마우스 휠이나 방향키로 이동할 수 있습니다.");
      BindTip(startup,"Windows 로그인 시 창을 띄우지 않고 트레이에서 시작합니다.");
      BindTip(pollSeconds,"1~300초를 직접 입력하거나 − / + 버튼으로 조절하세요.");

      var activity=new Surface{Dock=DockStyle.Fill,Margin=Padding.Empty};root.Controls.Add(activity,0,5);
      activity.Controls.Add(LabelAt("최근 활동",20,14,220,24,10,true,Theme.Text));
      var activityLinks=new FlowLayoutPanel{Location=new Point(606,13),Size=new Size(220,30),Anchor=AnchorStyles.Top|AnchorStyles.Right,WrapContents=false,BackColor=Theme.Card};
      activityLinks.Controls.Add(Link("진단 저장",delegate{SaveDiagnostic();}));activityLinks.Controls.Add(Link("전체 로그 ↗",delegate{OpenFolder(Files.Data);}));activity.Controls.Add(activityLinks);
      history=new RecentActivity{Location=new Point(20,44),Size=new Size(804,72),Anchor=AnchorStyles.Top|AnchorStyles.Left|AnchorStyles.Right,Localize=T};activity.Controls.Add(history);
      var footer=new Panel{Dock=DockStyle.Fill,Margin=Padding.Empty};root.Controls.Add(footer,0,6);
      footer.Controls.Add(LabelAt("창을 닫아도 트레이에서 계속 실행됩니다.",0,20,480,24,8.5f,false,Theme.Muted));
      var links=new FlowLayoutPanel{Location=new Point(576,16),Size=new Size(268,32),Anchor=AnchorStyles.Top|AnchorStyles.Right,WrapContents=false};links.Controls.Add(Link("Chrome 확장",delegate{OpenFolder(Path.Combine(Files.Root,"extension"));}));links.Controls.Add(Link("설치 안내 ↗",delegate{OpenFile(Path.Combine(Files.Root,settings.Language=="ko"?"시작하기.html":"GettingStarted.html"));}));footer.Controls.Add(links);
      tray=new NotifyIcon {Icon=trayUnknownIcon,Text="Replay Rescue · 즉시 리플레이 확인 중",Visible=!preview};
      var menu=new ContextMenuStrip();
      BindMenu(menu,"창 열기",delegate{ShowWindow();});
      BindMenu(menu,"자동 복구 일시 정지 / 재개",delegate{TogglePause();});
      BindMenu(menu,"지금 상태 확인",async delegate{policy.RetryNow();await Check();});
      menu.Items.Add(new ToolStripSeparator());BindMenu(menu,"완전히 종료",delegate{exiting=true;Close();});
      tray.ContextMenuStrip=menu;tray.DoubleClick+=delegate{ShowWindow();};
      Resize+=delegate{if(WindowState==FormWindowState.Minimized)Hide();};
      FormClosing+=delegate(object sender,FormClosingEventArgs e){if(!exiting && e.CloseReason==CloseReason.UserClosing){e.Cancel=true;Hide();}};
      FormClosed+=delegate{timer.Stop();tray.Visible=false;tray.Dispose();};
      Microsoft.Win32.SystemEvents.SessionSwitch+=OnSessionSwitch;
      timer.Interval=settings.PollSeconds*1000;timer.Tick+=async delegate{await Check();};timer.Start();
      ApplyLanguage();loading=false;
      Shown+=async delegate{await Check();};
      AddHistory("앱 시작 · " + (settings.Enabled?"자동 복구 켜짐":"일시 정지"));
    }
    protected override void SetVisibleCore(bool value) { if(startHidden && !IsHandleCreated){base.SetVisibleCore(false);CreateHandle();BeginInvoke(new Action(async delegate{await Check();}));return;}base.SetVisibleCore(value); }
    void OnSessionSwitch(object sender,SessionSwitchEventArgs e) {if(e.Reason==SessionSwitchReason.SessionUnlock)try{BeginInvoke(new Action(delegate{policy.RetryNow();}));}catch{}}
    protected override void Dispose(bool disposing){if(disposing){Microsoft.Win32.SystemEvents.SessionSwitch-=OnSessionSwitch;timer.Stop();timer.Dispose();tips.Dispose();if(tray!=null)tray.Dispose();trayOnIcon.Dispose();trayOffIcon.Dispose();trayUnknownIcon.Dispose();}base.Dispose(disposing);}
    string T(string original){return localizer.Translate(original);}
    void BindText(Control control,string original){Action update=delegate{control.Text=T(original);};textBindings.Add(update);update();}
    void BindTip(Control control,string original){Action update=delegate{tips.SetToolTip(control,T(original));};textBindings.Add(update);update();}
    void BindMenu(ContextMenuStrip menu,string original,EventHandler action){var item=menu.Items.Add(T(original),null,action);textBindings.Add(delegate{item.Text=T(original);});}
    internal void ChangeLanguage(string code){
      code=Localizer.Normalize(code);string previousLanguage=settings.Language;
      if(code==previousLanguage)return;
      settings.Language=code;
      try{if(!previewOnly)Files.Write(Settings.PathName,settings);}catch(Exception e){settings.Language=previousLanguage;loading=true;language.SelectedIndex=previousLanguage=="ko"?1:0;loading=false;MessageBox.Show(this,T(e.Message),T("언어 설정 저장 오류"));return;}
      localizer=new Localizer(code);loading=true;language.SelectedIndex=code=="ko"?1:0;loading=false;
      ApplyLanguage();AddHistory(code=="ko"?"언어 변경 · 한국어":"언어 변경 · English");
    }
    void ApplyLanguage(){
      SuspendLayout();foreach(var update in textBindings)update();
      pause.Text=T(settings.Enabled?"일시 정지":"감시 재개");domainCount.Text=T(settings.Domains.Length+"개 등록");feedback.Text=T(domainFeedback);
      domains.AccessibleName=T("보호 사이트 목록, 한 줄에 도메인 하나");startup.AccessibleName=T("Windows 로그인 시 자동 실행");pollSeconds.SetLanguage(localizer);
      UpdateDetail();history.RefreshLanguage();
      if(lastCheckFailed){status.Text=T(lastPolicyStatus);UpdateTray(null,lastPolicyStatus);}
      else if(lastN!=null && lastB!=null){UpdateStatus(lastPolicyStatus);UpdateTray(lastN.Enabled,lastPolicyStatus);}
      else{nvidia.Set(T("NVIDIA 설정"),T("확인 중"),Theme.Muted);recording.Set(T("실제 녹화"),T("확인 중"),Theme.Muted);chrome.Set("Chrome",T("확인 중"),Theme.Muted);tray.Text=T("Replay Rescue · 즉시 리플레이 확인 중");}
      ResumeLayout(true);
    }
    Label LabelAt(string text,int x,int y,int width,int height,float size,bool bold,Color color,string family="맑은 고딕"){var label=new Label{Bounds=new Rectangle(x,y,width,height),Font=new Font(family,size,bold?FontStyle.Bold:FontStyle.Regular),ForeColor=color,AutoEllipsis=true,TextAlign=ContentAlignment.MiddleLeft};BindText(label,text);return label;}
    static Panel Divider(int x,int y,int width){return new Panel{Bounds=new Rectangle(x,y,width,1),Anchor=AnchorStyles.Top|AnchorStyles.Left|AnchorStyles.Right,BackColor=Theme.Border};}
    Button MakeButton(string text,EventHandler action,int width) {var b=new SoftButton{Width=width,Margin=new Padding(0,0,8,0)};BindText(b,text);b.Click+=action;return b;}
    LinkLabel Link(string text,EventHandler action){var l=new LinkLabel{AutoSize=true,LinkColor=Theme.Muted,ActiveLinkColor=Theme.Accent,VisitedLinkColor=Theme.Muted,LinkBehavior=LinkBehavior.HoverUnderline,Margin=new Padding(0,5,18,0),Font=new Font("맑은 고딕",8.5f)};BindText(l,text);l.Click+=action;return l;}
    void ShowWindow(){Show();WindowState=FormWindowState.Normal;Activate();}
    static void OpenFolder(string path){Directory.CreateDirectory(path);Process.Start(new ProcessStartInfo("explorer.exe","\""+path+"\""){UseShellExecute=true});}
    static void OpenFile(string path){Process.Start(new ProcessStartInfo(path){UseShellExecute=true});}
    public static bool StartupEnabled(){using(var k=Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run"))return k!=null && Convert.ToString(k.GetValue("ReplayRescue")).Contains(Path.Combine(Files.Root,"ReplayRescue.exe"));}
    public static void SetStartup(bool enabled){using(var k=Registry.CurrentUser.CreateSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run")){if(enabled)k.SetValue("ReplayRescue","\""+Path.Combine(Files.Root,"ReplayRescue.exe")+"\" --tray");else k.DeleteValue("ReplayRescue",false);}}
    void SaveDomains(){var old=settings.Domains;try{settings.Domains=domains.Text.Split(new[]{'\r','\n',',',';'},StringSplitOptions.RemoveEmptyEntries).Select(Settings.NormalizeDomain).Distinct().ToArray();Files.Write(Settings.PathName,settings);domains.Text=string.Join(Environment.NewLine,settings.Domains);domainCount.Text=T(settings.Domains.Length+"개 등록");domainFeedback="저장했어요 · 다음 확인부터 적용";feedback.Text=T(domainFeedback);feedback.ForeColor=Theme.Accent;AddHistory("도메인 목록 저장 · "+settings.Domains.Length+"개");policy.RetryNow();}catch(Exception e){settings.Domains=old;MessageBox.Show(this,T(e.Message),T("도메인 확인"),MessageBoxButtons.OK,MessageBoxIcon.Information);}}
    async void ApplyPollInterval(){
      int previousSeconds=settings.PollSeconds;
      try{
        settings.PollSeconds=pollSeconds.Value;
        Files.Write(Settings.PathName,settings);
      }catch(Exception e){settings.PollSeconds=previousSeconds;MessageBox.Show(this,T(e.Message),T("확인 간격 저장 오류"),MessageBoxButtons.OK,MessageBoxIcon.Error);return;}
      timer.Stop();timer.Interval=settings.PollSeconds*1000;timer.Start();
      UpdateDetail();
      AddHistory("확인 간격 적용 · "+settings.PollSeconds+"초");
      await Check();
    }
    void UpdateDetail(){cadence.Text=T(settings.PollSeconds+"초마다 상태 확인 · 설정은 자동으로 유지됩니다.");}
    void UpdateStatus(string policyStatus){
      Color tone=Theme.Muted;
      if(!settings.Enabled){status.Text="자동 복구를 잠시 멈췄어요";tone=Theme.Amber;}
      else if(lastB.BlockedDomains.Length>0){status.Text="보호 사이트가 열려 있어요";tone=Theme.Amber;}
      else if(!lastB.Safe){status.Text="Chrome 상태를 확인해 주세요";tone=Theme.Amber;}
      else if(lastN.Enabled==true){status.Text=lastN.Running==true?"즉시 리플레이가 켜져 있어요":lastN.DesktopCapture==false?"게임 녹화를 기다리고 있어요":"즉시 리플레이가 켜져 있어요";tone=Theme.Accent;}
      else if(lastN.Enabled==false){status.Text="즉시 리플레이 복구 대기 중";tone=Theme.Amber;}
      else{status.Text="리플레이 상태를 확인해 주세요";tone=Theme.Amber;}
      status.Text=T(status.Text);
      detail.Text=T(lastB.BlockedDomains.Length>0?"자동 복구 대기 · "+string.Join(", ",lastB.BlockedDomains):policyStatus);
      if(lastB.WebCheckSkipped)detail.Text+=" · "+T("웹 체크 생략");
      stateGlyph.Tone=tone;stateGlyph.Invalidate();
      nvidia.Set(T("NVIDIA 설정"),T(Flag(lastN.Enabled)),lastN.Enabled==true?Theme.Accent:lastN.Enabled==false?Theme.Red:Theme.Muted);
      recording.Set(T("실제 녹화"),T(lastN.Running==true?"녹화 중":lastN.Running==false?"대기 중":"확인 불가"),lastN.Running==true?Theme.Accent:Theme.Muted);
      chrome.Set("Chrome",T(lastB.WebCheckSkipped?"웹 체크 생략":lastB.Connections>0?"연결됨 · "+lastB.Connections+"개":"열린 창 없음"),lastB.Connections>0?Theme.Accent:Theme.Muted);
      tips.SetToolTip(nvidia,T("NVIDIA 설정: "+Flag(lastN.Enabled)+"\n전환 단축키: "+Native.HotkeyName(lastN.Hotkey)));
      tips.SetToolTip(recording,T("NVIDIA 녹화 시작 기록을 별도로 확인합니다.\n데스크톱 캡처가 꺼져 있으면 게임 실행을 기다립니다."));
      tips.SetToolTip(chrome,T(lastB.Reason)+"\n"+T("확장이 연결되면 보호 사이트를 확인합니다.\n미연결 시 웹 체크 없이 설정한 간격으로 복구합니다.\n시크릿 창은 확장의 '시크릿 모드에서 허용'이 필요합니다."));
      tips.SetToolTip(detail,detail.Text);
      UpdateDetail();
    }
    async void TogglePause(){bool old=settings.Enabled;try{settings.Enabled=!settings.Enabled;Files.Write(Settings.PathName,settings);}catch(Exception e){settings.Enabled=old;MessageBox.Show(this,T(e.Message),T("설정 저장 오류"));return;}pause.Text=T(settings.Enabled?"일시 정지":"감시 재개");AddHistory(settings.Enabled?"사용자: 감시 재개":"사용자: 감시 일시 정지");await Check();}
    void AddHistory(string message){if(!previewOnly)Files.Log(message);history.Add(message);}
    void UpdateTray(bool? enabled,string description){
      // Show NVIDIA's enabled state even while automatic recovery is paused.
      Icon next=enabled==true?trayOnIcon:enabled==false?trayOffIcon:trayUnknownIcon;
      if(!Object.ReferenceEquals(tray.Icon,next))tray.Icon=next;
      string text=T("Replay Rescue · 즉시 리플레이 "+Flag(enabled))+Environment.NewLine+T(description);
      tray.Text=text.Length>63?text.Substring(0,63):text;
    }
    async Task Check(){
      if(busy || exiting)return;busy=true;
      try {
        var snapshot=await Task.Run(delegate{var n=probe.Read();var b=BrowserState.Read(settings,Native.ChromeWindowCount());return Tuple.Create(n,b);});
        if(exiting || IsDisposed)return;
        lastN=snapshot.Item1;lastB=snapshot.Item2;
        var d=policy.Evaluate(DateTime.UtcNow,settings,lastB,lastN);
        if(d.Send && !previewOnly){
          // Read the registry and domain reports again immediately before a toggle.
          string error=await Task.Run(delegate{var b=BrowserState.Read(settings,Native.ChromeWindowCount());var n=probe.Read();if(!settings.Enabled || !b.Safe || n.Enabled!=false || !n.Overlay)return "입력 직전 상태가 변경되어 다음 주기에 재확인";return Native.SendHotkey(n.Hotkey);});
          if(error==null){policy.AttemptSent(DateTime.UtcNow);AddHistory("다시 켜기 명령 전송 · "+Native.HotkeyName(lastN.Hotkey)+" · 실제 시작 확인 대기");}
          else{d.Status=error;AddHistory(error);}
        }
        lastCheckFailed=false;lastPolicyStatus=d.Status;UpdateStatus(d.Status);
        UpdateTray(lastN.Enabled,d.Status);
        if(previousWebCheckSkipped!=lastB.WebCheckSkipped){
          if(lastB.WebCheckSkipped)AddHistory("Chrome 확장 미연결 · 웹 체크 생략");
          else if(previousWebCheckSkipped==true)AddHistory(lastB.Connections>0?"Chrome 확장 연결 · 보호 사이트 확인 재개":"열린 Chrome 창 없음");
          previousWebCheckSkipped=lastB.WebCheckSkipped;
        }
        if(previous!=d.Status && !d.Status.Contains("초") && !d.Status.Contains("재확인 중")){AddHistory(d.Status);previous=d.Status;}
        if(!previewOnly)Files.Write(Path.Combine(Files.Data,"status.json"),new{at=DateTime.UtcNow.ToString("o"),monitorPid=Process.GetCurrentProcess().Id,status=d.Status,language=settings.Language,pollSeconds=settings.PollSeconds,enabled=lastN.Enabled,running=lastN.Running,chromeConnections=lastB.Connections,webCheckSkipped=lastB.WebCheckSkipped,blockedDomains=lastB.BlockedDomains});
      } catch(Exception e){if(!IsDisposed){lastCheckFailed=true;lastPolicyStatus="확인 중 오류 · 다음 주기에 재시도";status.Text=T(lastPolicyStatus);UpdateTray(null,lastPolicyStatus);AddHistory(e.GetType().Name+": "+e.Message);}}
      finally{busy=false;}
    }
    static string Flag(bool? value){return value==true?"켜짐":value==false?"꺼짐":"확인 불가";}
    void SaveDiagnostic(){try{string path=Path.Combine(Files.Data,"diagnostic.json");Files.Write(path,new{at=DateTime.UtcNow.ToString("o"),nvidia=lastN,browser=lastB,settings=settings,identity=Environment.UserName});AddHistory("진단 저장: diagnostic.json");OpenFolder(Files.Data);}catch(Exception e){MessageBox.Show(this,T(e.Message));}}
    public Task RefreshPreview(){return previewOnly?Check():Task.FromResult(0);}
    public void SavePreview(string path){using(var bitmap=new Bitmap(ClientSize.Width,ClientSize.Height)){Controls[0].DrawToBitmap(bitmap,new Rectangle(Point.Empty,ClientSize));bitmap.Save(path,System.Drawing.Imaging.ImageFormat.Png);}}
  }
}
