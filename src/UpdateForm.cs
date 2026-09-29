using System;
using System.Drawing;
using System.IO;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace ReplayRescue {
  internal sealed class UpdateForm : Form {
    readonly Localizer text;
    readonly Label state,latest;
    readonly SoftButton check,install;
    readonly ProgressBar progress;
    readonly MainForm owner;
    readonly ToolTip tips=new ToolTip();
    ReleaseInfo release;
    bool working,changingAuto;
    string T(string value){return text.Translate(value);}
    internal UpdateForm(MainForm parent,bool automatic,string language,bool preview=false){
      owner=parent;text=new Localizer(language);Text=T("Replay Rescue 업데이트");
      Font=new Font("맑은 고딕",9);BackColor=Theme.Background;ForeColor=Theme.Text;
      ClientSize=new Size(570,384);FormBorderStyle=FormBorderStyle.FixedDialog;MaximizeBox=false;MinimizeBox=false;StartPosition=FormStartPosition.CenterParent;
      AutoScaleMode=AutoScaleMode.Dpi;AutoScaleDimensions=new SizeF(96,96);Icon=AppIcon.Create(Theme.Accent);
      Controls.Add(new Label{Text=T("앱 업데이트"),Bounds=new Rectangle(26,22,510,34),Font=new Font("Segoe UI",18,FontStyle.Bold)});
      Controls.Add(new Label{Text=T("현재 버전: ")+Updater.CurrentVersion,Bounds=new Rectangle(28,65,510,26)});
      latest=new Label{Text=T("최신 버전을 확인합니다."),Bounds=new Rectangle(28,94,510,26),ForeColor=Theme.Muted};Controls.Add(latest);
      state=new Label{Bounds=new Rectangle(28,132,510,54),ForeColor=Theme.Accent};Controls.Add(state);
      progress=new ProgressBar{Bounds=new Rectangle(28,194,510,8),Minimum=0,Maximum=100,Visible=false};Controls.Add(progress);
      var auto=new CheckBox{Name="AutomaticUpdateChecks",Text=T("새 버전 자동 확인 (시작 후 · 하루 간격)"),Checked=automatic,Bounds=new Rectangle(28,220,510,28)};
      auto.CheckedChanged+=delegate{if(changingAuto)return;try{owner.SetUpdateChecks(auto.Checked);}catch(Exception error){MessageBox.Show(this,T(error.Message),T("설정 저장 오류"));changingAuto=true;auto.Checked=!auto.Checked;changingAuto=false;}};Controls.Add(auto);
      Controls.Add(new Label{Text=T("설치하면 앱이 재시작됩니다. 저장된 설정은 유지됩니다.\nChrome 확장 파일도 갱신되며, Chrome에서 확장 새로고침이 필요할 수 있습니다."),Bounds=new Rectangle(28,258,510,44),ForeColor=Theme.Muted,Font=new Font("맑은 고딕",8.5f)});
      check=new SoftButton{Text=T("업데이트 확인"),Bounds=new Rectangle(28,324,160,36)};
      install=new SoftButton{Name="InstallUpdate",Text=T("설치 후 재시작"),Bounds=new Rectangle(318,324,220,36),Enabled=false};
      check.Click+=async delegate{await Check();};install.Click+=async delegate{await Install();};Controls.Add(check);Controls.Add(install);
      if(!preview)Shown+=async delegate{await Check();};
      else{latest.Text=T("최신 버전: ")+Updater.CurrentVersion;state.Text=T("이미 최신 버전입니다.");}
      FormClosing+=delegate(object sender,FormClosingEventArgs e){if(working)e.Cancel=true;};
    }
    async Task Check(){
      if(working)return;working=true;check.Enabled=false;install.Enabled=false;state.Text=T("업데이트 확인 중…");state.ForeColor=Theme.Accent;
      try{release=await owner.CheckForUpdate(false);latest.Text=T("최신 버전: ")+release.Version;state.Text=T(release.Version>Updater.CurrentVersion?"새 버전을 설치할 수 있습니다.":"이미 최신 버전입니다.");install.Enabled=release.Version>Updater.CurrentVersion;install.Primary=install.Enabled;install.Invalidate();}
      catch(Exception error){state.ForeColor=Theme.Amber;state.Text=T("확인에 실패했습니다. 인터넷 연결을 확인하고 다시 시도하세요.");tips.SetToolTip(state,T(error.Message));}
      finally{working=false;check.Enabled=true;}
    }
    async Task Install(){
      if(working || release==null)return;working=true;check.Enabled=false;install.Enabled=false;progress.Visible=true;progress.Value=0;state.ForeColor=Theme.Accent;state.Text=T("업데이트 다운로드 중…");
      try{
        var reporter=new Progress<int>(value=>{if(!IsDisposed)progress.Value=value;});
        bool restartInTray=!owner.Visible;
        string plan=await Task.Run(()=>Updater.Prepare(release,restartInTray,text.Language,value=>((IProgress<int>)reporter).Report(value)));
        state.Text=T("파일 검증 완료 · 재시작 준비 중…");
        await owner.InstallPreparedUpdate(plan);
        working=false;Close();
      }catch(Exception error){state.ForeColor=Theme.Amber;state.Text=T("업데이트를 설치하지 못했습니다. 기존 앱은 유지됩니다.");MessageBox.Show(this,T(error.Message),T("업데이트 오류"),MessageBoxButtons.OK,MessageBoxIcon.Error);}
      finally{working=false;if(!IsDisposed){check.Enabled=true;install.Enabled=release.Version>Updater.CurrentVersion;}}
    }
    protected override void Dispose(bool disposing){if(disposing){tips.Dispose();if(Icon!=null)Icon.Dispose();}base.Dispose(disposing);}
  }
}
