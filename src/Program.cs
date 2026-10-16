using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;

[assembly:System.Reflection.AssemblyVersion("1.2.2.0")]
[assembly:System.Reflection.AssemblyFileVersion("1.2.2.0")]

namespace FlameCursor {
    static class Program {
        [STAThread] static int Main(string[] args) {
            try {
                if(args.Length>0 && args[0]=="--physics"){LiveFlame.Run();return 0;}
                if(args.Length>0 && args[0]=="--stop-physics"){LiveFlame.Stop();return 0;}
                if(args.Length>0 && args[0]=="--self-test")return SelfTest.Run(args.Length>1?args[1]:Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"verification"));
                if(args.Length>1 && args[0]=="--native-status") {File.WriteAllText(args[1],Native.NormalInfo());return 0;}
                if(args.Length>1 && args[0]=="--render-ui") {
                    Application.EnableVisualStyles();Application.SetCompatibleTextRenderingDefault(false);
                    using(var form=new SettingsWindow(args.Length>2&&args[2]=="large"?new Settings{FlameSize=130,ArrowPulse=false,PhysicsSensitivity=250}:null)) {
                        form.Show();Application.DoEvents();form.PerformLayout();
                        using(var image=new Bitmap(form.Width,form.Height)) {
                            form.DrawToBitmap(image,new Rectangle(0,0,image.Width,image.Height));
                            image.Save(args[1],System.Drawing.Imaging.ImageFormat.Png);
                        }
                    }
                    return 0;
                }
                if(args.Length>0 && args[0]=="--restore-silent") {Integration.Restore();return 0;}
                if(args.Length>0 && args[0]=="--register") {Integration.Register();return 0;}
                if(args.Length>0 && args[0]=="--apply-saved") {
                    Settings s=Store.Load();if(!s.Enabled)return 0;
                    if(!File.Exists(s.AppliedFile)||s.AppliedReactive!=s.ReactiveFlame) {s.AppliedFile=Path.Combine(Store.Home,"cursor-"+Guid.NewGuid().ToString("N")+".ani");Ani.Write(s,s.AppliedFile,null,Renderer.Frames,s.ReactiveFlame);s.AppliedReactive=s.ReactiveFlame;Store.Save(s);}
                    Native.Apply(s.AppliedFile,new Geometry(s).Side);LiveFlame.Start(s);return 0;
                }
                if(args.Length>0 && args[0]=="--enable-default") {
                    Settings s=Store.Load();s.AppliedFile=Path.Combine(Store.Home,"cursor-"+Guid.NewGuid().ToString("N")+".ani");
                    Ani.Write(s,s.AppliedFile,null,Renderer.Frames,s.ReactiveFlame);LiveFlame.Stop();Native.Apply(s.AppliedFile,new Geometry(s).Side);
                    s.Enabled=true;s.AppliedReactive=s.ReactiveFlame;Store.Save(s);Integration.Startup(true);LiveFlame.Start(s);return 0;
                }
                bool created;
                using(Mutex mutex=new Mutex(true,@"Local\FlameCursorSettings-"+Environment.UserName,out created)) {
                    if(!created){MessageBox.Show("Cursor Settings is already open.","Cursor Settings");return 0;}
                    Application.EnableVisualStyles();Application.SetCompatibleTextRenderingDefault(false);
                    Application.Run(new SettingsWindow());
                }
                return 0;
            } catch(Exception e) {
                if(args.Length>0) {try{File.WriteAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"last-error.txt"),e.ToString());}catch{}return 1;}
                MessageBox.Show(e.Message,"Flame Cursor",MessageBoxButtons.OK,MessageBoxIcon.Error);return 1;
            }
        }
    }
    static class SelfTest {
        static void Check(bool condition,string message){if(!condition)throw new Exception(message);}
        public static int Run(string directory) {
            Directory.CreateDirectory(directory);var log=new System.Text.StringBuilder();var watch=Stopwatch.StartNew();
            try {
                Settings s=new Settings();Geometry g=new Geometry(s);
                Check(Renderer.Pulse(false,25,4,.2)==1,"Disabled pulse must stay steady");
                Check(Renderer.Pulse(true,13,2,.125)>1.1,"Enabled arrow pulse expands");
                Check(Renderer.Pulse(s.FlamePulse,s.FlameAmount,s.FlameSpeed,.0625)==1,"Flame is steady by default");
                s.FlamePulse=true;s.ArrowPulse=false;
                Check(Renderer.Pulse(s.ArrowPulse,25,2,.0625)==1 && Renderer.Pulse(s.FlamePulse,25,2,.0625)>1.1,"Independent pulse controls");
                s=new Settings();
                string config=Path.Combine(directory,"test-settings.xml");Store.Save(s,config);
                s.ArrowSize=147;s.FlameSize=64;s.FlamePulse=true;s.PhysicsSensitivity=250;Store.Save(s,config);
                Settings loaded=Store.Load(config);Check(loaded.ArrowSize==147&&loaded.FlameSize==64&&loaded.FlamePulse&&loaded.PhysicsSensitivity==250,"Settings roundtrip including sensitivity");
                File.WriteAllText(config,"broken xml");loaded=Store.Load(config);Check(loaded.ArrowSize==100,"Backup recovery");
                foreach(string item in new[]{config,config+".bak"})File.Delete(item);
                ColorTests(directory);
                ResizeTests(directory);
                var reset=new Settings{Overall=130,ArrowSize=145,FlameSize=50,ArrowPulse=false,FlamePulse=true,ArrowAmount=25,FlameAmount=25,ArrowSpeed=4,FlameSpeed=1,ReactiveFlame=false,Enabled=true};
                reset.Outline.Hue=145;reset.Fill.Brightness=80;reset.Flame.Hue=240;reset.PhysicsSensitivity=350;reset.ResetAppearance();
                Check(reset.PhysicsSensitivity==100,"Reset restores flame sensitivity");
                Check(reset.Overall==100&&reset.ArrowSize==100&&reset.FlameSize==100&&reset.ArrowPulse&&!reset.FlamePulse&&reset.ArrowAmount==13&&reset.FlameAmount==13&&reset.ArrowSpeed==2&&reset.FlameSpeed==2&&reset.ReactiveFlame&&reset.Outline.Brightness==100&&reset.Fill.Brightness==2&&reset.Flame.Hue==21&&reset.Enabled,"Reset restores every appearance option and preserves enabled state");
                FlameWindow.Verify(directory);
                s=new Settings();g=new Geometry(s);
                using(Bitmap b=Renderer.Render(s,.0625,g)) {
                    b.Save(Path.Combine(directory,"cursor-preview.png"),System.Drawing.Imaging.ImageFormat.Png);
                    Check(b.GetPixel(g.HotX,g.HotY).A>0,"Click tip has a visible pixel");
                }
                using(Bitmap b=Renderer.Render(s,.3125,g)) b.Save(Path.Combine(directory,"cursor-small-pulse.png"),System.Drawing.Imaging.ImageFormat.Png);
                var large=new Settings{Overall=130,ArrowSize=150,FlameSize=130,ArrowPulse=true,FlamePulse=true,ArrowAmount=25,FlameAmount=25};
                using(Bitmap b=Renderer.Render(large,.0625,new Geometry(large)))b.Save(Path.Combine(directory,"cursor-large.png"),System.Drawing.Imaging.ImageFormat.Png);
                string file=Path.Combine(directory,"verified-default.ani");Ani.Write(new Settings(),file,null,Renderer.Frames);
                IntPtr h=Native.Load(file,g.Side);try {
                    Native.IconInfo info;Check(Native.GetIconInfo(h,out info),"Native ANI icon info");
                    try {Check(!info.Icon && info.HotX==g.HotX && info.HotY==g.HotY,"Hotspot survives Windows ANI loading");
                        using(Bitmap b=Bitmap.FromHbitmap(info.Color))Check(b.Width==g.Side&&b.Height==g.Side,"Windows preserves requested native cursor size");
                    } finally {if(info.Mask!=IntPtr.Zero)Native.DeleteObject(info.Mask);if(info.Color!=IntPtr.Zero)Native.DeleteObject(info.Color);}
                    using(var a=new Bitmap(g.Side,g.Side))using(var b=new Bitmap(g.Side,g.Side)) {
                        foreach(var pair in new[]{Tuple.Create(a,0u),Tuple.Create(b,30u)}) using(Graphics graphics=Graphics.FromImage(pair.Item1)) {
                            IntPtr dc=graphics.GetHdc();try{Check(Native.DrawIconEx(dc,0,0,h,g.Side,g.Side,pair.Item2,IntPtr.Zero,3),"Native ANI frame rendering");}finally{graphics.ReleaseHdc(dc);}
                        }
                        bool differs=false;for(int y=0;y<g.Side&&!differs;y++)for(int x=0;x<g.Side;x++)if(a.GetPixel(x,y)!=b.GetPixel(x,y)){differs=true;break;}
                        Check(differs,"Windows recognizes distinct animated frames");
                    }
                }finally{Native.DestroyCursor(h);}
                using(var stream=File.OpenRead(file))using(var reader=new BinaryReader(stream)) {
                    Check(new string(reader.ReadChars(4))=="RIFF","RIFF tag");Check(reader.ReadInt32()==stream.Length-8,"RIFF length");
                    Check(new string(reader.ReadChars(4))=="ACON","ANI type");
                }
                log.AppendLine("PASS: independent layer pulses, steady default flame, HSB target switching/live updates, color persistence/deep-copy/legacy defaults, coordinated flame hues, neutral smoke, alpha rendering, native ANI loading/animation, exact hotspot/dimensions, RIFF lengths.");
                log.AppendLine("PASS: full reset defaults and reactive flame/native window tests; see physics-results.txt.");
                log.AppendLine("Normal NNW arrow: vertical left side, diagonal right side. 120 frames / 30fps / 4-second native loop.");
                log.AppendLine("No system cursor, registry, startup or user settings were modified by self-test.");
                log.AppendLine("Duration: "+watch.ElapsedMilliseconds+" ms. Native frame size: "+g.Side+"px. ANI bytes: "+new FileInfo(file).Length);
                File.WriteAllText(Path.Combine(directory,"results.txt"),log.ToString());return 0;
            }catch(Exception e){File.WriteAllText(Path.Combine(directory,"results.txt"),"FAIL: "+e);return 1;}
        }
        static void ColorTests(string directory) {
            Check(Colors.Hsv(0,100,100).ToArgb()==Color.Red.ToArgb(),"Hue red endpoint");
            Check(Colors.Hsv(120,100,100).ToArgb()==Color.Lime.ToArgb(),"Hue green");
            Check(Colors.Hsv(240,100,100).ToArgb()==Color.Blue.ToArgb(),"Hue blue");
            Check(Colors.Hsv(360,100,100)==Colors.Hsv(0,100,100),"Hue wraps");
            Check(Colors.Hsv(240,0,100).ToArgb()==Color.White.ToArgb(),"Zero saturation is neutral");
            Check(Colors.Hsv(240,100,0).ToArgb()==Color.Black.ToArgb(),"Zero brightness is black");
            Settings s=new Settings();int changes=0;
            using(var editor=new ColorEditor(s,delegate{changes++;})) {
                ComboBox selector=null;ColorSlider hue=null,brightness=null,saturation=null;
                foreach(Control control in editor.Controls) {
                    if(control is ComboBox)selector=(ComboBox)control;
                    var slider=control as ColorSlider;if(slider==null)continue;
                    if(slider.Channel=="Hue")hue=slider;else if(slider.Channel=="Brightness")brightness=slider;else saturation=slider;
                }
                selector.SelectedIndex=0;hue.Value=130;saturation.Value=82;brightness.Value=76;
                Check(s.Outline.Hue==130&&s.Outline.Saturation==82&&s.Outline.Brightness==76,"Outline slider binding");
                Check(s.Flame.Hue==21&&s.Fill.Brightness==2,"Outline controls do not alter other targets");
                selector.SelectedIndex=2;hue.Value=210;brightness.Value=94;saturation.Value=88;
                selector.SelectedIndex=0;Check(hue.Value==130&&brightness.Value==76&&saturation.Value==82,"Switching target restores its sliders");
                Check(changes>=6,"Color slider changes invalidate the live preview");
            }
            Settings copy=s.Copy();s.Flame.Hue=50;Check(copy.Flame.Hue==210,"Async rendering snapshot owns its palette");
            string path=Path.Combine(directory,"colors-test.xml");Store.Save(copy,path);Settings read=Store.Load(path);
            Check(read.Outline.Hue==130&&read.Flame.Hue==210&&read.Fill.Brightness==2,"All target colors persist");
            File.WriteAllText(path,"<Settings><Overall>117</Overall><Enabled>true</Enabled></Settings>");
            read=Store.Load(path);Check(read.Overall==117&&read.Enabled&&read.Outline.Brightness==100&&read.Flame.Hue==21,"Old settings retain existing controls and gain default colors");
            File.Delete(path);
            copy=new Settings();copy.Flame.Hue=210;copy.Outline=new ColorChoice(130,82,100);copy.Fill=new ColorChoice(275,70,65);
            var geo=new Geometry(copy);
            using(var blue=Renderer.Render(copy,.125,geo))using(var normal=Renderer.Render(new Settings(),.125,new Geometry(new Settings()))) {
                blue.Save(Path.Combine(directory,"colored-cursor.png"),System.Drawing.Imaging.ImageFormat.Png);
                bool changedFire=false,unchangedSmoke=false;
                for(int y=geo.HotY+(int)(70*geo.Unit);y<geo.Side;y++)for(int x=0;x<geo.Side;x++) {
                    Color a=blue.GetPixel(x,y),b=normal.GetPixel(x,y);
                    if(a.A>30&&a.B>a.R+25&&a.B>a.G)changedFire=true;
                    if(y>geo.HotY+(int)(125*geo.Unit)&&a.A>5&&a==b)unchangedSmoke=true;
                }
                Check(changedFire,"Flame hue visibly recolors rendered fire");Check(unchangedSmoke,"Smoke remains neutral when flame hue changes");
            }
        }
        static void ResizeTests(string directory) {
            FireDrawing.Release();int cases=0,collisions=0,lastTexture=0,lastCanvas=0;
            for(int mode=0;mode<2;mode++)for(int step=0;step<=160;step++) {
                int size=step<=80?50+step:210-step;
                var s=new Settings{FlameSize=size,ArrowPulse=mode==1,FlamePulse=mode==1,FlameAmount=25};
                var geo=new Geometry(s);int side=((int)Math.Ceiling(geo.Side*3.6)+31)/32*32;
                if(lastCanvas==side&&lastTexture!=geo.Side)collisions++;lastCanvas=side;lastTexture=geo.Side;
                var motion=new FlameMotion();motion.Reset(PointF.Empty,FireDrawing.Length(geo));
                using(var texture=Renderer.Render(s,.125,geo,false,true,true))using(var reused=new Bitmap(512,512))using(var fresh=new Bitmap(512,512)) {
                    using(var g=Graphics.FromImage(reused)){g.Clear(Color.Transparent);FireDrawing.Draw(g,texture,geo,new PointF(256,200),motion);}
                    FireDrawing.Release();
                    using(var g=Graphics.FromImage(fresh)){g.Clear(Color.Transparent);FireDrawing.Draw(g,texture,geo,new PointF(256,200),motion);}
                    bool same=BitmapHash(reused)==BitmapHash(fresh);
                    if(!same){reused.Save(Path.Combine(directory,"resize-artifact.png"),System.Drawing.Imaging.ImageFormat.Png);fresh.Save(Path.Combine(directory,"resize-expected.png"),System.Drawing.Imaging.ImageFormat.Png);}
                    Check(same,"Resizing flame must match a fresh render: "+size+"%, mode "+mode+", texture "+geo.Side+", canvas "+side);
                    if(size==130&&mode==0)fresh.Save(Path.Combine(directory,"flame130-fixed.png"),System.Drawing.Imaging.ImageFormat.Png);
                    cases++;
                }
            }
            Check(collisions>0,"Resize regression must cover reused output sizes with different source textures");FireDrawing.Release();
            File.WriteAllText(Path.Combine(directory,"resize-results.txt"),"PASS: "+cases+" up/down flame-size frames from50% to130%, both pulse modes, "+collisions+" same-canvas/different-texture transitions; cached renders exactly match clean renders.");
        }
        static string BitmapHash(Bitmap bitmap) {
            var data=bitmap.LockBits(new Rectangle(0,0,bitmap.Width,bitmap.Height),System.Drawing.Imaging.ImageLockMode.ReadOnly,System.Drawing.Imaging.PixelFormat.Format32bppPArgb);
            try{var bytes=new byte[bitmap.Width*bitmap.Height*4];for(int y=0;y<bitmap.Height;y++)Marshal.Copy(IntPtr.Add(data.Scan0,y*data.Stride),bytes,y*bitmap.Width*4,bitmap.Width*4);
                using(var sha=System.Security.Cryptography.SHA256.Create())return Convert.ToBase64String(sha.ComputeHash(bytes));}
            finally{bitmap.UnlockBits(data);}
        }
    }
}
