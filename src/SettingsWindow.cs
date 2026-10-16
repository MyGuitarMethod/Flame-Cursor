using System;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace FlameCursor {
    sealed class Preview : Panel {
        public Settings Value;
        public bool Light;
        public readonly Stopwatch Clock=Stopwatch.StartNew();
        readonly FlameMotion motion=new FlameMotion();
        double last;
        int previousSide;
        double previousArrowUnit,previousFireUnit;
        public Preview() { DoubleBuffered=true; ResizeRedraw=true; BackColor=Color.FromArgb(24,30,32); }
        protected override void OnPaint(PaintEventArgs e) {
            base.OnPaint(e);
            e.Graphics.Clear(Light?Color.FromArgb(232,235,232):Color.FromArgb(24,30,32));
            using(Pen grid=new Pen(Light?Color.FromArgb(218,222,219):Color.FromArgb(30,39,40))) {
                for(int x=0;x<Width;x+=24)e.Graphics.DrawLine(grid,x,0,x,Height);
                for(int y=0;y<Height;y+=24)e.Graphics.DrawLine(grid,0,y,Width,y);
            }
            if(Value==null)return;
            var geo=new Geometry(Value);
            double phase=(Clock.Elapsed.TotalSeconds%Renderer.Duration)/Renderer.Duration;
            using(Bitmap image=Renderer.Render(Value,phase,geo,true,!Value.ReactiveFlame)) {
                float zoom=Math.Min(2f,Math.Min((Width-40f)/geo.Side,(Height-80f)/geo.Side));
                float x=(Width-image.Width*zoom)/2, y=(Height-image.Height*zoom)/2-10;
                e.Graphics.InterpolationMode=InterpolationMode.HighQualityBicubic;
                if(Value.ReactiveFlame) {
                    double now=Clock.Elapsed.TotalSeconds;
                    if(previousSide!=geo.Side||previousArrowUnit!=geo.ArrowUnit||previousFireUnit!=geo.FireUnit)motion.Reset(Cursor.Position,FireDrawing.Length(geo));
                    else motion.Update(Cursor.Position,now-last,FireDrawing.Length(geo),Value.PhysicsSensitivity);
                    previousSide=geo.Side;previousArrowUnit=geo.ArrowUnit;previousFireUnit=geo.FireUnit;last=now;
                    var saved=e.Graphics.Save();e.Graphics.TranslateTransform(Width*.43f,Height*.37f);e.Graphics.ScaleTransform(zoom,zoom);
                    using(Bitmap texture=Renderer.Render(Value,phase,geo,false,true,true))FireDrawing.Draw(e.Graphics,texture,geo,PointF.Empty,motion);
                    e.Graphics.DrawImage(image,-geo.HotX,-geo.HotY,image.Width,image.Height);e.Graphics.Restore(saved);
                }else e.Graphics.DrawImage(image,x,y,image.Width*zoom,image.Height*zoom);
                using(Font font=new Font("Segoe UI",10)) using(Brush brush=new SolidBrush(Light?Color.FromArgb(65,75,73):Color.FromArgb(169,187,180))) {
                    e.Graphics.DrawString("Live preview · enlarged for editing",font,brush,new RectangleF(10,Height-44,Width-20,30),new StringFormat{Alignment=StringAlignment.Center});
                }
            }
        }
    }
    public sealed class SettingsWindow : Form {
        static readonly Color Background=Color.FromArgb(17,23,24), Surface=Color.FromArgb(29,38,38), Muted=Color.FromArgb(161,181,173);
        readonly Preview preview=new Preview();
        readonly Settings draft;
        readonly Timer timer=new Timer();
        readonly Label status=new Label();
        readonly ProgressBar progress=new ProgressBar();
        readonly Label enabled=new Label();
        readonly Button apply=new Button();
        readonly Button restore=new Button();
        readonly TableLayoutPanel controls=new TableLayoutPanel();
        readonly ColorEditor colors;
        readonly FlowLayoutPanel sectionTabs=new FlowLayoutPanel();
        bool busy, closing;
        public SettingsWindow(Settings previewSettings=null) {
            draft=previewSettings??Store.Load(); Text="Cursor Settings"; Name="FlameCursorSettings";
            Icon=SystemIcons.Application; BackColor=Background; ForeColor=Color.White;
            Font=new Font("Segoe UI",10); AutoScaleMode=AutoScaleMode.Dpi;
            ClientSize=new Size(900,730); MinimumSize=new Size(850,750); StartPosition=FormStartPosition.CenterScreen;
            var root=new TableLayoutPanel {Dock=DockStyle.Fill,Padding=new Padding(26),ColumnCount=1,RowCount=4};
            root.RowStyles.Add(new RowStyle(SizeType.Absolute,78)); root.RowStyles.Add(new RowStyle(SizeType.Percent,100));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute,57)); root.RowStyles.Add(new RowStyle(SizeType.Absolute,66)); Controls.Add(root);
            var header=new TableLayoutPanel {Dock=DockStyle.Fill,ColumnCount=2,RowCount=1};
            header.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,65)); header.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,35));
            var heading=new Label {Text="Cursor Settings",Font=new Font("Segoe UI",23,FontStyle.Regular),AutoSize=true,Dock=DockStyle.Fill}; header.Controls.Add(heading,0,0);
            enabled.Text=draft.Enabled?"Enabled":"Disabled";enabled.ForeColor=Muted;enabled.Dock=DockStyle.Fill;enabled.TextAlign=ContentAlignment.MiddleRight;
            enabled.AccessibleName="Cursor status";
            header.Controls.Add(enabled,1,0); root.Controls.Add(header,0,0);
            var main=new TableLayoutPanel {Dock=DockStyle.Fill,ColumnCount=2,RowCount=1};
            main.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,43)); main.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,57)); root.Controls.Add(main,0,1);
            var left=new TableLayoutPanel {Dock=DockStyle.Fill,ColumnCount=1,RowCount=3,Margin=new Padding(0,0,22,0)};
            left.RowStyles.Add(new RowStyle(SizeType.Percent,100)); left.RowStyles.Add(new RowStyle(SizeType.Absolute,42)); left.RowStyles.Add(new RowStyle(SizeType.Absolute,72));
            preview.Value=draft; preview.Dock=DockStyle.Fill; left.Controls.Add(preview,0,0);
            var backgrounds=new FlowLayoutPanel {Dock=DockStyle.Fill,Padding=new Padding(0,4,0,0)};
            var dark=Button("Dark preview",120); var light=Button("Light preview",120);
            dark.Click+=delegate {preview.Light=false;preview.Invalidate();};light.Click+=delegate {preview.Light=true;preview.Invalidate();};
            backgrounds.Controls.Add(dark); backgrounds.Controls.Add(light); left.Controls.Add(backgrounds,0,1);
            left.Controls.Add(new Label {Text="The arrow tip is the click point.\nText and resize pointers stay familiar.\nApply saves your settings for next time.",Dock=DockStyle.Fill,ForeColor=Muted,Padding=new Padding(0,6,0,0)},0,2);
            main.Controls.Add(left,0,0);
            controls.Dock=DockStyle.Fill; controls.ColumnCount=1; controls.RowCount=0;controls.AutoScroll=true;
            controls.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));
            controls.Margin=new Padding(0);
            var right=new TableLayoutPanel{Dock=DockStyle.Fill,ColumnCount=1,RowCount=2,Margin=Padding.Empty};
            right.RowStyles.Add(new RowStyle(SizeType.Absolute,44));right.RowStyles.Add(new RowStyle(SizeType.Percent,100));
            main.Controls.Add(right,1,0);
            sectionTabs.Dock=DockStyle.Fill;sectionTabs.WrapContents=false;
            var motionTab=Button("Size & pulse",160);var colorTab=Button("Colors",120);
            sectionTabs.Controls.Add(motionTab);sectionTabs.Controls.Add(colorTab);right.Controls.Add(sectionTabs,0,0);
            var content=new Panel{Dock=DockStyle.Fill,Margin=Padding.Empty};right.Controls.Add(content,0,1);
            colors=new ColorEditor(draft,delegate{preview.Invalidate();}){Dock=DockStyle.Fill};
            content.Controls.Add(controls);content.Controls.Add(colors);controls.Visible=false;colors.Visible=true;
            colorTab.BackColor=Color.FromArgb(44,71,55);
            if(previewSettings!=null){controls.Visible=true;colors.Visible=false;motionTab.BackColor=Color.FromArgb(44,71,55);colorTab.BackColor=Surface;}
            motionTab.Click+=delegate{controls.Show();colors.Hide();motionTab.BackColor=Color.FromArgb(44,71,55);colorTab.BackColor=Surface;};
            colorTab.Click+=delegate{colors.Show();controls.Hide();colorTab.BackColor=Color.FromArgb(44,71,55);motionTab.BackColor=Surface;};
            Slider("Overall size",60,130,draft.Overall,delegate(int n){draft.Overall=n;});
            Section("ARROW");
            Slider("Arrow size",60,150,draft.ArrowSize,delegate(int n){draft.ArrowSize=n;});
            CheckBox arrowPulse=Toggle("Pulse arrow",draft.ArrowPulse,delegate(bool b){draft.ArrowPulse=b;});
            TrackBar arrowAmount=Slider("Pulse strength",0,25,draft.ArrowAmount,delegate(int n){draft.ArrowAmount=n;});
            ComboBox arrowSpeed=Speed(draft.ArrowSpeed,delegate(int n){draft.ArrowSpeed=n;});
            arrowAmount.Enabled=arrowSpeed.Enabled=draft.ArrowPulse;
            arrowPulse.CheckedChanged+=delegate {arrowAmount.Enabled=arrowSpeed.Enabled=arrowPulse.Checked;};
            Section("FLAME & SMOKE");
            CheckBox physics=Toggle("Flame physics",draft.ReactiveFlame,delegate(bool b){draft.ReactiveFlame=b;});
            TrackBar sensitivity=Slider("Flame sensitivity",25,400,draft.PhysicsSensitivity,delegate(int n){draft.PhysicsSensitivity=n;});
            sensitivity.Enabled=draft.ReactiveFlame;physics.CheckedChanged+=delegate{sensitivity.Enabled=physics.Checked;};
            Slider("Flame size",50,130,draft.FlameSize,delegate(int n){draft.FlameSize=n;});
            CheckBox flamePulse=Toggle("Pulse flame",draft.FlamePulse,delegate(bool b){draft.FlamePulse=b;});
            TrackBar flameAmount=Slider("Pulse strength",0,25,draft.FlameAmount,delegate(int n){draft.FlameAmount=n;});
            ComboBox flameSpeed=Speed(draft.FlameSpeed,delegate(int n){draft.FlameSpeed=n;});
            flameAmount.Enabled=flameSpeed.Enabled=draft.FlamePulse;
            flamePulse.CheckedChanged+=delegate {flameAmount.Enabled=flameSpeed.Enabled=flamePulse.Checked;};
            int spacer=controls.RowCount++;controls.RowStyles.Add(new RowStyle(SizeType.Percent,100));
            controls.Controls.Add(new Panel{Dock=DockStyle.Fill},0,spacer);
            controls.AutoScrollMinSize=new Size(0,508);
            var statusRow=new TableLayoutPanel {Dock=DockStyle.Fill,ColumnCount=1,RowCount=2};
            statusRow.RowStyles.Add(new RowStyle(SizeType.Absolute,30));statusRow.RowStyles.Add(new RowStyle(SizeType.Absolute,10));
            status.Dock=DockStyle.Fill;status.ForeColor=Muted;status.Text=draft.Enabled?"Enabled. Adjust the preview, then Apply.":"Disabled. Your Windows pointer is unchanged.";
            progress.Dock=DockStyle.Fill;progress.Visible=false; statusRow.Controls.Add(status,0,0);statusRow.Controls.Add(progress,0,1);root.Controls.Add(statusRow,0,2);
            var footer=new FlowLayoutPanel {Dock=DockStyle.Fill,Padding=new Padding(0,8,0,0),WrapContents=false};
            apply.Text="Apply & enable";Style(apply,150);apply.Click+=async delegate {await ApplyAsync();}; footer.Controls.Add(apply);
            restore.Text="Disable";Style(restore,115);restore.Click+=delegate {Disable();}; footer.Controls.Add(restore);
            var guide=Button("How to use",120);guide.Click+=delegate {
                string path=Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"READ ME.txt");
                if(File.Exists(path))Process.Start(new ProcessStartInfo(path){UseShellExecute=true});
                else MessageBox.Show("Enable or Apply to use the cursor. Adjust arrow and flame separately. Disable restores your Windows pointer.","How to use");
            };footer.Controls.Add(guide);
            var defaults=Button("Reset defaults",145);defaults.Click+=async delegate {
                if(busy)return;bool active=Store.Load().Enabled;
                draft.ResetAppearance();ReloadValues();colors.RefreshValues();preview.Invalidate();
                if(active){if(await ApplyAsync())status.Text="All defaults restored and applied.";}
                else{draft.Enabled=false;Store.Save(draft);status.Text="All defaults restored. Cursor remains disabled.";}
            };footer.Controls.Add(defaults);root.Controls.Add(footer,0,3);
            timer.Interval=33;timer.Tick+=delegate {if(!busy)preview.Invalidate();};timer.Start();
            FormClosing+=delegate(object sender,FormClosingEventArgs e){if(busy){e.Cancel=true;return;}closing=true;timer.Stop();timer.Dispose();};
        }
        void ReloadValues() {
            foreach(Control row in controls.Controls) {
                TrackBar track=null;Label value=null;
                foreach(Control c in row.Controls){if(c is TrackBar)track=(TrackBar)c;if(c is Label && c.Name=="value")value=(Label)c;}
                if(track!=null){string prop=(string)track.Tag;track.Value=(int)typeof(Settings).GetField(prop).GetValue(draft);if(value!=null)value.Text=track.Value+"%";}
                foreach(Control c in row.Controls) {
                    var check=c as CheckBox;if(check!=null)check.Checked=check.Text=="Pulse arrow"?draft.ArrowPulse:check.Text=="Flame physics"?draft.ReactiveFlame:draft.FlamePulse;
                    var combo=c as ComboBox;if(combo!=null)combo.SelectedIndex=1;
                }
            }
        }
        static void Style(Button b,int width) {b.Size=new Size(width,37);b.UseMnemonic=false;b.FlatStyle=FlatStyle.Flat;b.FlatAppearance.BorderColor=Color.FromArgb(83,119,101);b.BackColor=Surface;b.ForeColor=Color.White;b.Cursor=Cursors.Hand;b.Margin=new Padding(0,0,10,0);}
        static Button Button(string text,int width) {var b=new Button{Text=text};Style(b,width);return b;}
        void Add(Control c,int height) {int row=controls.RowCount++;controls.RowStyles.Add(new RowStyle(SizeType.Absolute,height));c.Dock=DockStyle.Fill;controls.Controls.Add(c,0,row);}
        void Section(string title) {Add(new Label{Text=title,UseMnemonic=false,ForeColor=Color.FromArgb(166,213,187),Font=new Font("Segoe UI",10,FontStyle.Bold),Padding=new Padding(0,8,0,0)},35);}
        TrackBar Slider(string title,int low,int high,int val,Action<int> changed) {
            var row=new TableLayoutPanel {ColumnCount=3,RowCount=1,Margin=new Padding(0)};
            row.RowStyles.Add(new RowStyle(SizeType.Percent,100));
            row.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,115));row.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));row.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,70));
            row.Controls.Add(new Label{Text=title,Dock=DockStyle.Fill,TextAlign=ContentAlignment.MiddleLeft},0,0);
            var track=new TrackBar{Minimum=low,Maximum=high,Value=val,TickStyle=TickStyle.None,Dock=DockStyle.Fill,AutoSize=false,Margin=new Padding(3,10,3,0),AccessibleName=title};
            // Tags only support preview reset; event handlers keep the draft authoritative.
            string field=title=="Overall size"?"Overall":title=="Arrow size"?"ArrowSize":title=="Flame size"?"FlameSize":title=="Flame sensitivity"?"PhysicsSensitivity":controls.RowCount<6?"ArrowAmount":"FlameAmount";
            track.Tag=field;
            var label=new Label{Name="value",Text=val+"%",Margin=Padding.Empty,AutoEllipsis=true,TextAlign=ContentAlignment.MiddleRight,Dock=DockStyle.Fill,ForeColor=Muted};
            track.ValueChanged+=delegate{changed(track.Value);label.Text=track.Value+"%";preview.Invalidate();};
            row.Controls.Add(track,1,0);row.Controls.Add(label,2,0);Add(row,40);return track;
        }
        CheckBox Toggle(string text,bool value,Action<bool> changed) {
            var row=new Panel();var check=new CheckBox{Text=text,Checked=value,Dock=DockStyle.Fill,AccessibleName=text};
            check.CheckedChanged+=delegate{changed(check.Checked);preview.Invalidate();};row.Controls.Add(check);Add(row,30);return check;
        }
        ComboBox Speed(int value,Action<int> changed) {
            var row=new TableLayoutPanel{ColumnCount=2,RowCount=1,Margin=new Padding(0)};
            row.RowStyles.Add(new RowStyle(SizeType.Percent,100));
            row.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,115));row.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));
            row.Controls.Add(new Label{Text="Pulse speed",Dock=DockStyle.Fill,TextAlign=ContentAlignment.MiddleLeft},0,0);
            var combo=new ComboBox{DropDownStyle=ComboBoxStyle.DropDownList,Dock=DockStyle.Fill,FlatStyle=FlatStyle.Flat,BackColor=Surface,ForeColor=Color.White,AccessibleName="Pulse speed"};
            combo.Items.AddRange(new object[]{"Slow","Natural","Fast","Very fast"});combo.SelectedIndex=value-1;combo.SelectedIndexChanged+=delegate{changed(combo.SelectedIndex+1);preview.Invalidate();};
            row.Controls.Add(combo,1,0);Add(row,34);return combo;
        }
        async Task<bool> ApplyAsync() {
            if(busy)return false;SetBusy(true);Settings copy=draft.Copy();string file=Path.Combine(Store.Home,"cursor-"+Guid.NewGuid().ToString("N")+".ani");
            try {
                status.Text="Preparing your cursor…"; progress.Value=0;
                Geometry geo=await Task.Run(delegate{return Ani.Write(copy,file,delegate(int n){if(!closing)BeginInvoke((Action)delegate{progress.Value=n;status.Text="Preparing your cursor… "+n+"%";});},Renderer.Frames,copy.ReactiveFlame);});
                LiveFlame.Stop();Native.Apply(file,geo.Side);copy.Enabled=true;copy.AppliedFile=file;copy.AppliedReactive=copy.ReactiveFlame;Store.Save(copy);Integration.Startup(true);LiveFlame.Start(copy);
                draft.Enabled=true;draft.AppliedFile=file;draft.AppliedReactive=copy.AppliedReactive;enabled.Text="Enabled";status.Text="Enabled and saved. You can close this window.";
                CleanOldCursors(file);
                return true;
            } catch(Exception e) {enabled.Text=Store.Load().Enabled?"Enabled":"Disabled";status.Text="Could not apply: "+e.Message;return false;}
            finally{SetBusy(false);}
        }
        void Disable() {
            if(busy)return;
            try{Integration.Restore();draft.Enabled=false;enabled.Text="Disabled";status.Text="Disabled. Your normal Windows pointer is restored.";}
            catch(Exception e){status.Text="Could not restore: "+e.Message;enabled.Text=Store.Load().Enabled?"Enabled":"Disabled";}
        }
        void SetBusy(bool value) {busy=value;controls.Enabled=colors.Enabled=sectionTabs.Enabled=!value;apply.Enabled=restore.Enabled=enabled.Enabled=!value;progress.Visible=value;}
        static void CleanOldCursors(string keep) {
            foreach(string path in Directory.GetFiles(Store.Home,"cursor-*.ani")) if(path!=keep)try{File.Delete(path);}catch(IOException){}catch(UnauthorizedAccessException){}
        }
    }
}
