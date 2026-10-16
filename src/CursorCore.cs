using System;
using System.IO;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Xml.Serialization;
using Microsoft.Win32;

namespace FlameCursor {
    public static class ArrowShape {
        public const double RightX=34,RightY=37.2,RootY=24;
        public static readonly double FlameAngle=Math.Atan2(RightX,RightY)*.5;
        public static readonly float FlameX=(float)Math.Sin(FlameAngle),FlameY=(float)Math.Cos(FlameAngle);
        public static readonly double RootX=RootY*Math.Tan(FlameAngle);
    }
    public sealed class Settings {
        public int Overall = 100, ArrowSize = 100, FlameSize = 100;
        public bool ArrowPulse = true, FlamePulse = false, Enabled = false;
        public bool ReactiveFlame = true, AppliedReactive = false;
        public int PhysicsSensitivity = 100;
        public int ArrowAmount = 13, FlameAmount = 13, ArrowSpeed = 2, FlameSpeed = 2;
        public string AppliedFile = "";
        public ColorChoice Outline = new ColorChoice(0,0,100);
        public ColorChoice Fill = new ColorChoice(180,20,2);
        public ColorChoice Flame = new ColorChoice(21,93,100);
        public Settings Copy() {
            var copy=(Settings)MemberwiseClone();copy.Outline=Outline.Copy();copy.Fill=Fill.Copy();copy.Flame=Flame.Copy();return copy;
        }
        public void ResetAppearance() {
            var s=new Settings();Overall=s.Overall;ArrowSize=s.ArrowSize;FlameSize=s.FlameSize;
            ArrowPulse=s.ArrowPulse;FlamePulse=s.FlamePulse;ArrowAmount=s.ArrowAmount;FlameAmount=s.FlameAmount;
            ArrowSpeed=s.ArrowSpeed;FlameSpeed=s.FlameSpeed;ReactiveFlame=s.ReactiveFlame;
            PhysicsSensitivity=s.PhysicsSensitivity;
            Outline=s.Outline;Fill=s.Fill;Flame=s.Flame;AppliedFile="";AppliedReactive=false;
        }
        public void Validate() {
            Overall = Clamp(Overall, 60, 130); ArrowSize = Clamp(ArrowSize, 60, 150);
            FlameSize = Clamp(FlameSize, 50, 130); ArrowAmount = Clamp(ArrowAmount, 0, 25);
            FlameAmount = Clamp(FlameAmount, 0, 25); ArrowSpeed = Clamp(ArrowSpeed, 1, 4);
            FlameSpeed = Clamp(FlameSpeed, 1, 4);
            PhysicsSensitivity=Clamp(PhysicsSensitivity,25,400);
            if(Outline==null)Outline=new ColorChoice(0,0,100);
            if(Fill==null)Fill=new ColorChoice(180,20,2);
            if(Flame==null)Flame=new ColorChoice(21,93,100);
            Outline.Validate();Fill.Validate();Flame.Validate();
        }
        static int Clamp(int n, int lo, int hi) { return Math.Max(lo, Math.Min(hi, n)); }
    }
    public static class Store {
        public static readonly string Home = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "FlameCursor");
        public static string Config { get { return Path.Combine(Home, "settings.xml"); } }
        public static Settings Load() { return Load(Config); }
        public static Settings Load(string path) {
            foreach (string p in new string[] {path, path + ".bak"}) {
                try { if (File.Exists(p)) using (var stream = File.OpenRead(p)) {
                    var value = (Settings)new XmlSerializer(typeof(Settings)).Deserialize(stream); value.Validate(); return value;
                }} catch (IOException) {} catch (InvalidOperationException) {} catch (UnauthorizedAccessException) {}
            }
            return new Settings();
        }
        public static void Save(Settings value) { Save(value, Config); }
        public static void Save(Settings value, string path) {
            value.Validate(); Directory.CreateDirectory(Path.GetDirectoryName(path));
            string tmp = path + ".tmp";
            using (var stream = File.Create(tmp)) new XmlSerializer(typeof(Settings)).Serialize(stream, value);
            if (File.Exists(path)) File.Replace(tmp, path, path + ".bak"); else File.Move(tmp, path);
        }
    }
    public sealed class Geometry {
        public readonly int Side, HotX, HotY;
        public readonly double Unit, ArrowUnit, FireUnit;
        public Geometry(Settings s) {
            Unit = .60 * s.Overall / 100.0;
            ArrowUnit = Unit * s.ArrowSize / 100.0;
            FireUnit = Unit * s.FlameSize / 100.0;
            // Bounds account for either layer at its largest pulse, not the current frame.
            double fireMax = FireUnit * (s.FlamePulse ? 1 + s.FlameAmount / 100.0 : 1);
            double headMax = ArrowUnit * (s.ArrowPulse ? 1 + s.ArrowAmount / 100.0 : 1);
            double required = Math.Max(7 + headMax * 55, 7 + 24 * ArrowUnit + fireMax * 195) + 7;
            if(required > 250) {
                double fit = 240 / required;
                Unit *= fit; ArrowUnit *= fit; FireUnit *= fit; fireMax *= fit; headMax *= fit;
            }
            HotX = (int)Math.Ceiling(12 + fireMax * 24); HotY = 7;
            double end = Math.Max(HotY + headMax * 55, HotY + 24 * ArrowUnit + fireMax * 195);
            Side = Math.Min(256, Math.Max(64, ((int)Math.Ceiling(end + 7) + 3) / 4 * 4));
        }
    }
    public static class Renderer {
        public const int Frames = 120;
        public const double Duration = 4.0;
        public static double Pulse(bool enabled, int amount, int speed, double phase) {
            return enabled ? 1 + amount / 100.0 * Math.Sin(phase * Math.PI * 2 * speed) : 1;
        }
        static double Smooth(double a, double b, double x) {
            x = Math.Max(0, Math.Min(1, (x - a) / (b - a))); return x * x * (3 - 2 * x);
        }
        static double Hash(int x, int y) {
            unchecked { uint n = (uint)x * 374761393u + (uint)y * 668265263u; n = (n ^ (n >> 13)) * 1274126177u;
                return (n ^ (n >> 16)) / 4294967295.0; }
        }
        static double Noise(double x, double y) {
            int ix = (int)Math.Floor(x), iy = (int)Math.Floor(y);
            double fx = x - ix, fy = y - iy; fx = fx * fx * (3 - 2 * fx); fy = fy * fy * (3 - 2 * fy);
            double a = Hash(ix, iy), b = Hash(ix + 1, iy), c = Hash(ix, iy + 1), d = Hash(ix + 1, iy + 1);
            return (a + (b - a) * fx) * (1 - fy) + (c + (d - c) * fx) * fy;
        }
        static double Cloud(double x, double y) { return .57 * Noise(x,y) + .28 * Noise(x*2.03,y*2.03) + .15 * Noise(x*4.11,y*4.11); }
        public static Bitmap Render(Settings s, double phase, Geometry geo,bool arrow=true,bool fire=true,bool verticalFire=false) {
            Bitmap image = new Bitmap(geo.Side, geo.Side, PixelFormat.Format32bppArgb);
            byte[] data = new byte[geo.Side * geo.Side * 4];
            double firePulse = Pulse(s.FlamePulse,s.FlameAmount,s.FlameSpeed,phase);
            double fireUnit = geo.FireUnit * firePulse;
            double rootX = geo.HotX + ArrowShape.RootX * geo.ArrowUnit, rootY = geo.HotY + ArrowShape.RootY * geo.ArrowUnit;
            double t = phase * Duration, loop = phase * Math.PI * 2;
            Color edgeColor=Colors.Hsv(s.Flame.Hue,s.Flame.Saturation,s.Flame.Brightness);
            Color coreColor=Colors.Hsv(s.Flame.Hue+28,s.Flame.Saturation*.247,s.Flame.Brightness);
            double er=edgeColor.R/255.0,eg=edgeColor.G/255.0,eb=edgeColor.B/255.0;
            double cr=coreColor.R/255.0,cg=coreColor.G/255.0,cb=coreColor.B/255.0;
            // Tail flows southeast behind a conventional northwest pointer.
            if(fire)for (int y = geo.HotY; y < geo.Side; y++) for (int x = 0; x < geo.Side; x++) {
                double dx = (x + .5 - rootX) / fireUnit, dy = (y + .5 - rootY) / fireUnit;
                double across = verticalFire?dx:dx * ArrowShape.FlameY - dy * ArrowShape.FlameX, back = verticalFire?dy:dx * ArrowShape.FlameX + dy * ArrowShape.FlameY;
                if (back < 0 || back > 192 || Math.Abs(across) > 53) continue;
                double rise = back / 150.0;
                double drift = Math.Sin(rise * 5 - loop * 2) * (3 + rise * 8);
                // Crossfade two noise samples so the four-second ANI loops seamlessly.
                double n = Cloud(across*.065, -back*.045+t*1.6)*(1-phase) + Cloud(across*.065,-back*.045+(t-Duration)*1.6)*phase;
                double smokeWidth = 7 + rise * 20;
                double smoke = Math.Exp(-Math.Pow((across-drift)/smokeWidth,2)*2);
                smoke *= Smooth(.15,.45,rise)*(1-Smooth(.75,1.18,rise))*Smooth(.28,.7,n)*.62;
                double fr = back / 118.0, root = Smooth(0,.22,fr);
                double width = Math.Max(.1,(1-fr)*22*root);
                double turbulence = (n-.5)*(10+fr*22)*root;
                double flame = 1-Smooth(width*.3,width,Math.Abs(across-drift*.65*root+turbulence));
                flame *= Smooth(0,.18,fr)*(1-Smooth(.5,1,fr));
                double core = flame*flame*flame, fa = flame*.78, alpha = fa+smoke*(1-fa);
                if (alpha < .004) continue;
                double sr = .045+(.30-.045)*n, sg = .055+(.32-.055)*n, sb = .06+(.33-.06)*n;
                int p = (y*geo.Side+x)*4;
                data[p] = (byte)Math.Min(255,255*((eb+(cb-eb)*core)*fa+sb*smoke*(1-fa))/alpha);
                data[p+1] = (byte)Math.Min(255,255*((eg+(cg-eg)*core)*fa+sg*smoke*(1-fa))/alpha);
                data[p+2] = (byte)Math.Min(255,255*((er+(cr-er)*core)*fa+sr*smoke*(1-fa))/alpha);
                data[p+3] = (byte)Math.Min(255,255*alpha);
            }
            var bits = image.LockBits(new Rectangle(0,0,geo.Side,geo.Side),ImageLockMode.WriteOnly,PixelFormat.Format32bppArgb);
            try { Marshal.Copy(data,0,bits.Scan0,data.Length); } finally { image.UnlockBits(bits); }
            if(!arrow)return image;
            double pulse = Pulse(s.ArrowPulse,s.ArrowAmount,s.ArrowSpeed,phase);
            float k = (float)(geo.ArrowUnit*pulse), hx=geo.HotX, hy=geo.HotY;
            // Left edge is strictly vertical. The right edge slopes from the fixed click tip.
            PointF[] points = {new PointF(hx,hy),new PointF(hx,hy+52.8f*k),
                new PointF(hx+12*k,hy+37.2f*k),new PointF(hx+34*k,hy+37.2f*k)};
            float bright = s.ArrowPulse ? (float)(.65+.35*(.5+.5*Math.Sin(loop*s.ArrowSpeed))) : 1;
            using (Graphics g=Graphics.FromImage(image)) using (GraphicsPath path=new GraphicsPath()) {
                g.SmoothingMode=SmoothingMode.AntiAlias; path.AddPolygon(points);
                Color outline=Colors.Hsv(s.Outline.Hue,s.Outline.Saturation,s.Outline.Brightness*bright);
                Color inside=Colors.Hsv(s.Fill.Hue,s.Fill.Saturation,s.Fill.Brightness*bright);
                using (Pen glow=new Pen(Color.FromArgb(23,outline),Math.Max(2,9*k))) g.DrawPath(glow,path);
                using (SolidBrush fill=new SolidBrush(inside)) g.FillPath(fill,path);
                using (Pen edge=new Pen(outline,Math.Max(1.15f,2.6f*k))) {
                    edge.LineJoin=LineJoin.Round; g.DrawPath(edge,path);
                }
            }
            return image;
        }
    }
    public static class Ani {
        static void Tag(BinaryWriter w,string tag) { w.Write(System.Text.Encoding.ASCII.GetBytes(tag)); }
        static void Chunk(BinaryWriter w,string tag,byte[] bytes) { Tag(w,tag); w.Write(bytes.Length); w.Write(bytes); if ((bytes.Length&1)!=0) w.Write((byte)0); }
        public static byte[] CursorImage(Bitmap image,int hx,int hy) {
            int side=image.Width, maskStride=((side+31)/32)*4;
            byte[] bgra=new byte[side*side*4];
            var bits=image.LockBits(new Rectangle(0,0,side,side),ImageLockMode.ReadOnly,PixelFormat.Format32bppArgb);
            try { Marshal.Copy(bits.Scan0,bgra,0,bgra.Length); } finally { image.UnlockBits(bits); }
            using (MemoryStream mem=new MemoryStream()) using (BinaryWriter w=new BinaryWriter(mem)) {
                w.Write((ushort)0); w.Write((ushort)2); w.Write((ushort)1);
                w.Write((byte)(side==256?0:side)); w.Write((byte)(side==256?0:side)); w.Write((ushort)0);
                w.Write((ushort)hx); w.Write((ushort)hy); w.Write(40+bgra.Length+maskStride*side); w.Write(22);
                w.Write(40); w.Write(side); w.Write(side*2); w.Write((ushort)1); w.Write((ushort)32);
                w.Write(0); w.Write(bgra.Length+maskStride*side); w.Write(0); w.Write(0); w.Write(0); w.Write(0);
                for (int y=side-1;y>=0;y--) w.Write(bgra,y*side*4,side*4);
                for (int y=side-1;y>=0;y--) {
                    byte[] mask=new byte[maskStride];
                    for (int x=0;x<side;x++) if(bgra[(y*side+x)*4+3]==0) mask[x/8]|=(byte)(0x80>>(x%8));
                    w.Write(mask);
                }
                return mem.ToArray();
            }
        }
        public static Geometry Write(Settings settings,string file,Action<int> progress,int frames,bool arrowOnly=false) {
            settings.Validate(); var geo=new Geometry(settings);
            string tmp=file+".tmp"; Directory.CreateDirectory(Path.GetDirectoryName(file));
            try {
                using (FileStream stream=File.Create(tmp)) using (BinaryWriter w=new BinaryWriter(stream)) {
                    Tag(w,"RIFF"); w.Write(0); Tag(w,"ACON");
                    using (MemoryStream m=new MemoryStream()) using (BinaryWriter h=new BinaryWriter(m)) {
                        foreach(uint n in new uint[]{36,(uint)frames,(uint)frames,(uint)geo.Side,(uint)geo.Side,32,1,2,1}) h.Write(n);
                        Chunk(w,"anih",m.ToArray());
                    }
                    Tag(w,"LIST"); long listSize=stream.Position; w.Write(0); Tag(w,"fram");
                    for(int i=0;i<frames;i++) {
                        using(Bitmap b=Renderer.Render(settings,(double)i/frames,geo,true,!arrowOnly)) Chunk(w,"icon",CursorImage(b,geo.HotX,geo.HotY));
                        if(progress!=null) progress((i+1)*100/frames);
                    }
                    long end=stream.Position; stream.Position=listSize; w.Write((int)(end-listSize-4)); stream.Position=4; w.Write((int)(end-8));
                }
                if(File.Exists(file)) File.Delete(file); File.Move(tmp,file); return geo;
            } catch { if(File.Exists(tmp))File.Delete(tmp); throw; }
        }
    }
    public static class Native {
        [DllImport("user32.dll",CharSet=CharSet.Unicode,SetLastError=true)] public static extern IntPtr LoadImage(IntPtr instance,string name,uint kind,int width,int height,uint flags);
        [DllImport("user32.dll",SetLastError=true)] static extern bool SetSystemCursor(IntPtr cursor,uint id);
        [DllImport("user32.dll",SetLastError=true)] public static extern bool DestroyCursor(IntPtr cursor);
        [DllImport("user32.dll",SetLastError=true)] static extern bool SystemParametersInfo(uint action,uint param,IntPtr value,uint flags);
        [DllImport("user32.dll",SetLastError=true)] public static extern bool GetIconInfo(IntPtr icon,out IconInfo info);
        [DllImport("user32.dll")] static extern IntPtr LoadCursor(IntPtr instance,IntPtr name);
        [DllImport("user32.dll",SetLastError=true)] public static extern bool DrawIconEx(IntPtr dc,int x,int y,IntPtr cursor,int width,int height,uint frame,IntPtr brush,uint flags);
        [DllImport("gdi32.dll")] public static extern bool DeleteObject(IntPtr obj);
        [StructLayout(LayoutKind.Sequential)] public struct IconInfo { [MarshalAs(UnmanagedType.Bool)] public bool Icon; public uint HotX,HotY; public IntPtr Mask,Color; }
        public static IntPtr Load(string path,int side) {
            IntPtr h=LoadImage(IntPtr.Zero,path,2,side,side,0x10);
            if(h==IntPtr.Zero) throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error(),"Windows could not load the animated cursor."); return h;
        }
        public static void Apply(string path,int side) {
            IntPtr h=Load(path,side);
            // SetSystemCursor consumes the handle on success. Never pass a shared cursor.
            if(!SetSystemCursor(h,32512)) { int e=Marshal.GetLastWin32Error(); DestroyCursor(h); throw new System.ComponentModel.Win32Exception(e); }
        }
        public static void Restore() { if(!SystemParametersInfo(0x57,0,IntPtr.Zero,0)) throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error()); }
        public static string NormalInfo() {
            IconInfo info;if(!GetIconInfo(LoadCursor(IntPtr.Zero,new IntPtr(32512)),out info))throw new System.ComponentModel.Win32Exception();
            try {using(Bitmap b=Bitmap.FromHbitmap(info.Color))return b.Width+"x"+b.Height+" hotspot "+info.HotX+","+info.HotY;}
            finally{if(info.Color!=IntPtr.Zero)DeleteObject(info.Color);if(info.Mask!=IntPtr.Zero)DeleteObject(info.Mask);}
        }
    }
    public static class Integration {
        public const string MenuKey=@"Software\Classes\DesktopBackground\shell\FlameCursorSettings";
        public const string UninstallKey=@"Software\Microsoft\Windows\CurrentVersion\Uninstall\FlameCursor";
        public static string Exe { get { return Path.Combine(Store.Home,"FlameCursor.exe"); } }
        public static string Link { get { return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Programs),"Cursor Settings.lnk"); } }
        public static void Register() {
            using(var key=Registry.CurrentUser.CreateSubKey(MenuKey)) {
                key.SetValue("","Cursor Settings"); key.SetValue("Icon","\""+Exe+"\",0");
                using(var command=key.CreateSubKey("command")) command.SetValue("","\""+Exe+"\"");
            }
            using(var key=Registry.CurrentUser.CreateSubKey(UninstallKey)) {
                key.SetValue("DisplayName","Flame Cursor"); key.SetValue("DisplayVersion","1.2.2"); key.SetValue("Publisher","Flame Cursor");
                key.SetValue("InstallLocation",Store.Home); key.SetValue("DisplayIcon",Exe);
                key.SetValue("UninstallString","\""+Path.Combine(Store.Home,"Uninstall.exe")+"\" --uninstall");
                key.SetValue("NoModify",1,RegistryValueKind.DWord); key.SetValue("NoRepair",1,RegistryValueKind.DWord);
            }
            Type type=Type.GetTypeFromProgID("WScript.Shell");
            if(type!=null) {
                dynamic shell=Activator.CreateInstance(type); dynamic link=shell.CreateShortcut(Link);
                link.TargetPath=Exe; link.WorkingDirectory=Store.Home; link.Description="Flame Cursor Settings"; link.Save();
                Marshal.FinalReleaseComObject(link); Marshal.FinalReleaseComObject(shell);
            }
        }
        public static void Startup(bool enabled) {
            using(var key=Registry.CurrentUser.CreateSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run")) {
                if(enabled && File.Exists(Exe)) key.SetValue("FlameCursor","\""+Exe+"\" --apply-saved"); else key.DeleteValue("FlameCursor",false);
            }
        }
        public static void Restore() {
            LiveFlame.Stop();
            Native.Restore(); Startup(false);
            Settings s=Store.Load(); s.Enabled=false; Store.Save(s);
        }
        public static void Remove() {
            Restore();
            using(var key=Registry.CurrentUser.OpenSubKey(MenuKey)) {
                if(key!=null) using(var cmd=key.OpenSubKey("command")) {
                    if(cmd!=null && (string)cmd.GetValue("")=="\""+Exe+"\"") Registry.CurrentUser.DeleteSubKeyTree(MenuKey,false);
                }
            }
            Registry.CurrentUser.DeleteSubKeyTree(UninstallKey,false);
            if(File.Exists(Link))File.Delete(Link);
        }
    }
}
