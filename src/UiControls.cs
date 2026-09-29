using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Collections.Generic;
using System.Windows.Forms;

namespace ReplayRescue {
  internal static class Theme {
    internal static readonly Color Background=Color.FromArgb(13,17,23),Card=Color.FromArgb(22,29,38),Input=Color.FromArgb(15,21,29),Border=Color.FromArgb(43,54,67),Text=Color.FromArgb(235,241,247),Muted=Color.FromArgb(145,162,181),Accent=Color.FromArgb(119,230,175),Amber=Color.FromArgb(239,191,105),Red=Color.FromArgb(243,128,133);
    internal static GraphicsPath Round(RectangleF r,float radius){var p=new GraphicsPath();float d=radius*2;p.AddArc(r.X,r.Y,d,d,180,90);p.AddArc(r.Right-d,r.Y,d,d,270,90);p.AddArc(r.Right-d,r.Bottom-d,d,d,0,90);p.AddArc(r.X,r.Bottom-d,d,d,90,90);p.CloseFigure();return p;}
    internal static void TextAt(Graphics g,string text,Font font,Color color,Rectangle r,TextFormatFlags extra=TextFormatFlags.Default){TextRenderer.DrawText(g,text,font,r,color,TextFormatFlags.NoPrefix|TextFormatFlags.EndEllipsis|extra);}
  }
  internal class Surface : Panel {
    internal int Radius=14;
    internal Color Outline=Theme.Border;
    public Surface(){BackColor=Theme.Card;DoubleBuffered=true;ResizeRedraw=true;}
    protected override void OnPaintBackground(PaintEventArgs e){e.Graphics.Clear(Parent==null?Theme.Background:Parent.BackColor);e.Graphics.SmoothingMode=SmoothingMode.AntiAlias;using(var path=Theme.Round(new RectangleF(.5f,.5f,Width-1,Height-1),Radius))using(var fill=new SolidBrush(BackColor))using(var pen=new Pen(Outline)){e.Graphics.FillPath(fill,path);e.Graphics.DrawPath(pen,path);}}
  }
  internal class SoftButton : Button {
    internal bool Primary;
    bool hovered;
    public SoftButton(){SetStyle(ControlStyles.UserPaint|ControlStyles.AllPaintingInWmPaint|ControlStyles.OptimizedDoubleBuffer|ControlStyles.ResizeRedraw,true);FlatStyle=FlatStyle.Flat;FlatAppearance.BorderSize=0;Cursor=Cursors.Hand;Height=36;BackColor=Theme.Card;ForeColor=Theme.Text;Font=new Font("맑은 고딕",9,FontStyle.Bold);}
    protected override void OnMouseEnter(EventArgs e){hovered=true;Invalidate();base.OnMouseEnter(e);}
    protected override void OnMouseLeave(EventArgs e){hovered=false;Invalidate();base.OnMouseLeave(e);}
    protected override void OnPaint(PaintEventArgs e){e.Graphics.Clear(Parent==null?Theme.Card:Parent.BackColor);e.Graphics.SmoothingMode=SmoothingMode.AntiAlias;Color fill=Primary?(hovered?Color.FromArgb(149,239,195):Theme.Accent):(hovered?Color.FromArgb(48,62,77):Color.FromArgb(32,43,56));using(var path=Theme.Round(new RectangleF(.5f,.5f,Width-1,Height-1),8))using(var b=new SolidBrush(fill))using(var p=new Pen(Primary?fill:Theme.Border)){e.Graphics.FillPath(b,path);e.Graphics.DrawPath(p,path);}Theme.TextAt(e.Graphics,Text,Font,Enabled?(Primary?Theme.Background:Theme.Text):Theme.Muted,ClientRectangle,TextFormatFlags.HorizontalCenter|TextFormatFlags.VerticalCenter);if(Focused && ShowFocusCues)ControlPaint.DrawFocusRectangle(e.Graphics,new Rectangle(4,4,Width-8,Height-8),Theme.Text,fill);}
  }
  internal class ToggleSwitch : CheckBox {
    public ToggleSwitch(){SetStyle(ControlStyles.UserPaint|ControlStyles.AllPaintingInWmPaint|ControlStyles.OptimizedDoubleBuffer|ControlStyles.ResizeRedraw,true);AutoSize=false;Size=new Size(46,28);Cursor=Cursors.Hand;AccessibleRole=AccessibleRole.CheckButton;}
    protected override void OnPaint(PaintEventArgs e){e.Graphics.Clear(Parent==null?Theme.Card:Parent.BackColor);e.Graphics.SmoothingMode=SmoothingMode.AntiAlias;var track=new RectangleF(2,4,Width-4,Height-8);using(var path=Theme.Round(track,track.Height/2))using(var b=new SolidBrush(Checked?Theme.Accent:Color.FromArgb(62,76,92))){e.Graphics.FillPath(b,path);}float d=track.Height-6;using(var b=new SolidBrush(Checked?Theme.Background:Theme.Text)){e.Graphics.FillEllipse(b,Checked?track.Right-d-3:track.Left+3,track.Top+3,d,d);}if(Focused && ShowFocusCues)ControlPaint.DrawFocusRectangle(e.Graphics,ClientRectangle);}
    protected override void OnCheckedChanged(EventArgs e){Invalidate();base.OnCheckedChanged(e);}
  }
  internal class LanguagePicker : SoftButton {
    readonly ContextMenuStrip choices=new ContextMenuStrip{BackColor=Theme.Card,ForeColor=Theme.Text,ShowImageMargin=false};
    int selected=-1;
    internal event EventHandler SelectedIndexChanged;
    internal int SelectedIndex {get{return selected;}set{if(value<0 || value>1 || value==selected)return;selected=value;Text=(value==0?"English":"Korean")+"  ▾";for(int i=0;i<choices.Items.Count;i++)((ToolStripMenuItem)choices.Items[i]).Checked=i==value;var changed=SelectedIndexChanged;if(changed!=null)changed(this,EventArgs.Empty);}}
    public LanguagePicker(){AccessibleName="Language / 언어";choices.Items.Add("English",null,delegate{SelectedIndex=0;});choices.Items.Add("Korean",null,delegate{SelectedIndex=1;});SelectedIndex=0;Click+=delegate{choices.Show(this,new Point(0,Height));};}
    protected override void Dispose(bool disposing){if(disposing)choices.Dispose();base.Dispose(disposing);}
  }
  internal class IntervalInput : Surface {
    readonly TextBox input;
    readonly SoftButton minus,plus;
    public IntervalInput(){Radius=8;BackColor=Theme.Input;Height=38;Width=144;AccessibleName="확인 간격 (초)";minus=new SoftButton{Text="−",Width=32,Dock=DockStyle.Left,TabStop=false};plus=new SoftButton{Text="+",Width=32,Dock=DockStyle.Right,TabStop=false};input=new TextBox{BorderStyle=BorderStyle.None,BackColor=Theme.Input,ForeColor=Theme.Text,TextAlign=HorizontalAlignment.Center,Font=new Font("Segoe UI",12,FontStyle.Bold),Text="2",AccessibleName="확인 간격, 1부터 300초",MaxLength=3};Controls.Add(input);Controls.Add(minus);Controls.Add(plus);minus.Click+=delegate{Step(-1);};plus.Click+=delegate{Step(1);};input.KeyPress+=delegate(object s,KeyPressEventArgs e){if(!char.IsControl(e.KeyChar) && !char.IsDigit(e.KeyChar))e.Handled=true;};input.KeyDown+=delegate(object s,KeyEventArgs e){if(e.KeyCode==Keys.Up || e.KeyCode==Keys.Down){Step(e.KeyCode==Keys.Up?1:-1);e.SuppressKeyPress=true;}};Padding=new Padding(4);Resize+=delegate{LayoutInput();};LayoutInput();}
    void LayoutInput(){if(input!=null)input.SetBounds(40,(Height-input.PreferredHeight)/2,Math.Max(20,Width-80),input.PreferredHeight);}
    void Step(int delta){int v;if(!int.TryParse(input.Text,out v))v=Settings.MinPollSeconds;Value=Math.Max(Settings.MinPollSeconds,Math.Min(Settings.MaxPollSeconds,v+delta));}
    internal void SetLanguage(Localizer localizer){AccessibleName=localizer.Translate("확인 간격 (초)");input.AccessibleName=localizer.Translate("확인 간격, 1부터 300초");}
    public int Value {set{input.Text=value.ToString();}get{int v;if(!int.TryParse(input.Text,out v) || v<Settings.MinPollSeconds || v>Settings.MaxPollSeconds)throw new ArgumentException("확인 간격은 1~300초 사이로 입력해 주세요.");return v;}}
  }
  internal class Signal : Control {
    string caption="",value="확인 중";
    Color tone=Theme.Muted;
    public Signal(){DoubleBuffered=true;BackColor=Theme.Card;Font=new Font("맑은 고딕",9);}
    internal void Set(string title,string text,Color color){caption=title;value=text;tone=color;AccessibleName=title+": "+text;Invalidate();}
    protected override void OnPaint(PaintEventArgs e){base.OnPaint(e);e.Graphics.SmoothingMode=SmoothingMode.AntiAlias;float scale=DeviceDpiScale(e.Graphics);using(var b=new SolidBrush(tone))e.Graphics.FillEllipse(b,0,Height/2-4*scale,8*scale,8*scale);int x=(int)(18*scale);int w=Math.Min((int)(80*scale),Width/2);Theme.TextAt(e.Graphics,caption,Font,Theme.Muted,new Rectangle(x,0,w,Height),TextFormatFlags.VerticalCenter);using(var f=new Font("맑은 고딕",9,FontStyle.Bold))Theme.TextAt(e.Graphics,value,f,Theme.Text,new Rectangle(x+w,0,Math.Max(0,Width-x-w),Height),TextFormatFlags.VerticalCenter);}
    static float DeviceDpiScale(Graphics g){return g.DpiX/96f;}
  }
  internal class StateGlyph : Control {
    internal Color Tone=Theme.Accent;
    public StateGlyph(){Size=new Size(46,46);DoubleBuffered=true;}
    protected override void OnPaint(PaintEventArgs e){e.Graphics.Clear(Parent==null?Theme.Card:Parent.BackColor);e.Graphics.SmoothingMode=SmoothingMode.AntiAlias;float s=Math.Min(Width,Height)/46f;using(var path=Theme.Round(new RectangleF(1,1,Width-2,Height-2),12*s))using(var b=new SolidBrush(Color.FromArgb(30,Tone))){e.Graphics.FillPath(b,path);}using(var p=new Pen(Tone,2*s))e.Graphics.DrawEllipse(p,12*s,12*s,22*s,22*s);using(var b=new SolidBrush(Tone))e.Graphics.FillEllipse(b,19*s,19*s,8*s,8*s);}
  }
  internal class RecentActivity : Control {
    readonly List<Tuple<string,string>> entries=new List<Tuple<string,string>>();
    internal Func<string,string> Localize=delegate(string text){return text;};
    public RecentActivity(){DoubleBuffered=true;BackColor=Theme.Card;Font=new Font("맑은 고딕",9);}
    internal void Add(string message){entries.Insert(0,Tuple.Create(DateTime.Now.ToString("HH:mm:ss"),message));if(entries.Count>3)entries.RemoveAt(3);RefreshLanguage();}
    internal void RefreshLanguage(){AccessibleName=Localize(entries.Count>0?"최근 활동: "+entries[0].Item2:"최근 활동");Invalidate();}
    protected override void OnPaint(PaintEventArgs e){base.OnPaint(e);float s=e.Graphics.DpiX/96f;int row=(int)(24*s),timeWidth=(int)(78*s);for(int i=0;i<entries.Count;i++){Theme.TextAt(e.Graphics,entries[i].Item1,Font,Theme.Muted,new Rectangle(0,row*i,timeWidth,row),TextFormatFlags.VerticalCenter);Theme.TextAt(e.Graphics,Localize(entries[i].Item2),Font,i==0?Theme.Text:Theme.Muted,new Rectangle(timeWidth,row*i,Math.Max(0,Width-timeWidth),row),TextFormatFlags.VerticalCenter);}}
  }
}
