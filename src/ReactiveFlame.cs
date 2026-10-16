using System;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;

namespace FlameCursor {
    public sealed class FlameMotion {
        public PointF Tail,Velocity,FilteredMouse;
        PointF last;
        bool ready;
        public void Reset(PointF mouse,float length){last=mouse;ready=true;Tail=new PointF(ArrowShape.FlameX*length,ArrowShape.FlameY*length);Velocity=FilteredMouse=PointF.Empty;}
        public void Update(PointF mouse,double seconds,float length,int sensitivity=100) {
            if(!ready){Reset(mouse,length);return;}
            float dt=(float)Math.Max(.001,Math.Min(.08,seconds));
            float vx=(mouse.X-last.X)/dt,vy=(mouse.Y-last.Y)/dt;last=mouse;
            float speed=(float)Math.Sqrt(vx*vx+vy*vy);if(speed>5000){vx*=5000/speed;vy*=5000/speed;}
            float mix=1-(float)Math.Exp(-dt*18);
            FilteredMouse=new PointF(FilteredMouse.X+(vx-FilteredMouse.X)*mix,FilteredMouse.Y+(vy-FilteredMouse.Y)*mix);
            float force=.20f*sensitivity/100f;
            PointF target=new PointF(ArrowShape.FlameX*length-FilteredMouse.X*force,ArrowShape.FlameY*length-FilteredMouse.Y*force);
            float distance=(float)Math.Sqrt(target.X*target.X+target.Y*target.Y),limit=length*1.35f;
            if(distance>limit){target.X*=limit/distance;target.Y*=limit/distance;}
            // A damped spring stores momentum; substeps keep slow frames stable.
            int steps=(int)Math.Ceiling(dt*120);float step=dt/steps;
            for(int i=0;i<steps;i++) {
                Velocity=new PointF(Velocity.X+((target.X-Tail.X)*100-Velocity.X*19)*step,
                    Velocity.Y+((target.Y-Tail.Y)*100-Velocity.Y*19)*step);
                Tail=new PointF(Tail.X+Velocity.X*step,Tail.Y+Velocity.Y*step);
            }
        }
        public PointF At(float t,float length) {
            float u=1-t,a=3*u*u*t,b=3*u*t*t,c=t*t*t;
            return new PointF(a*ArrowShape.FlameX*length*.20f+b*Tail.X*.72f+c*Tail.X,
                a*ArrowShape.FlameY*length*.20f+b*Tail.Y*.72f+c*Tail.Y);
        }
    }
    public static class FireDrawing {
        [ThreadStatic]static WarpSurface surface;
        public static float Length(Geometry g){return g.Side-(float)(g.HotY+ArrowShape.RootY*g.ArrowUnit)-2;}
        public static void Draw(Graphics graphics,Bitmap texture,Geometry geo,PointF tip,FlameMotion motion) {
            int side=((int)Math.Ceiling(geo.Side*3.6)+31)/32*32;
            if(texture.Width!=geo.Side||texture.Height!=geo.Side)throw new ArgumentException("Flame texture dimensions do not match its geometry.");
            if(surface==null||surface.Side!=side||surface.TextureSide!=texture.Width){if(surface!=null)surface.Image.Dispose();surface=new WarpSurface(side,texture.Width);}
            surface.Begin(texture);
            float sourceX=(float)(geo.HotX+ArrowShape.RootX*geo.ArrowUnit),sourceY=(float)(geo.HotY+ArrowShape.RootY*geo.ArrowUnit);
            float length=Length(geo),origin=side/2;
            PointF root=new PointF(origin+(float)(ArrowShape.RootX*geo.ArrowUnit),origin+(float)(ArrowShape.RootY*geo.ArrowUnit));
            const int slices=18;
            for(int i=0;i<slices;i++) {
                float t0=i/(float)slices,t1=(i+1)/(float)slices;
                PointF a=motion.At(t0,length),b=motion.At(t1,length);
                PointF n0=Normal(motion,t0,length),n1=Normal(motion,t1,length);
                PointF l0=new PointF(root.X+a.X-n0.X*sourceX,root.Y+a.Y-n0.Y*sourceX);
                PointF r0=new PointF(root.X+a.X+n0.X*(texture.Width-sourceX),root.Y+a.Y+n0.Y*(texture.Width-sourceX));
                PointF l1=new PointF(root.X+b.X-n1.X*sourceX,root.Y+b.Y-n1.Y*sourceX);
                PointF r1=new PointF(root.X+b.X+n1.X*(texture.Width-sourceX),root.Y+b.Y+n1.Y*(texture.Width-sourceX));
                PointF uv0=new PointF(0,sourceY+t0*length),uv1=new PointF(texture.Width-1,sourceY+t0*length),uv2=new PointF(0,sourceY+t1*length),uv3=new PointF(texture.Width-1,sourceY+t1*length);
                surface.Triangle(l0,r0,l1,uv0,uv1,uv2);
                surface.Triangle(r0,r1,l1,uv1,uv3,uv2);
            }
            surface.Finish();graphics.DrawImage(surface.Image,tip.X-origin,tip.Y-origin,side,side);
        }
        static PointF Normal(FlameMotion motion,float t,float length) {
            PointF a=motion.At(Math.Max(0,t-.001f),length),b=motion.At(Math.Min(1,t+.001f),length);
            float dx=b.X-a.X,dy=b.Y-a.Y,d=(float)Math.Sqrt(dx*dx+dy*dy);
            if(d<.0001f)return new PointF(ArrowShape.FlameY,-ArrowShape.FlameX);
            float blend=t*.55f,nx=ArrowShape.FlameY*(1-blend)+dy/d*blend,ny=-ArrowShape.FlameX*(1-blend)-dx/d*blend;
            float magnitude=(float)Math.Sqrt(nx*nx+ny*ny);return magnitude<.001f?new PointF(ArrowShape.FlameY,-ArrowShape.FlameX):new PointF(nx/magnitude,ny/magnitude);
        }
        public static void Release(){if(surface!=null){surface.Image.Dispose();surface=null;}}
        sealed class WarpSurface {
            public readonly int Side,TextureSide;
            public readonly Bitmap Image;
            readonly byte[] output,input;
            public WarpSurface(int side,int source){Side=side;TextureSide=source;Image=new Bitmap(side,side,PixelFormat.Format32bppPArgb);output=new byte[side*side*4];input=new byte[source*source*4];}
            public void Begin(Bitmap texture){Array.Clear(output,0,output.Length);var data=texture.LockBits(new Rectangle(0,0,texture.Width,texture.Height),ImageLockMode.ReadOnly,PixelFormat.Format32bppPArgb);try{for(int y=0;y<TextureSide;y++)Marshal.Copy(IntPtr.Add(data.Scan0,y*data.Stride),input,y*TextureSide*4,TextureSide*4);}finally{texture.UnlockBits(data);}}
            public void Finish(){var data=Image.LockBits(new Rectangle(0,0,Side,Side),ImageLockMode.WriteOnly,PixelFormat.Format32bppPArgb);try{Marshal.Copy(output,0,data.Scan0,output.Length);}finally{Image.UnlockBits(data);}}
            public void Triangle(PointF a,PointF b,PointF c,PointF ua,PointF ub,PointF uc) {
                float denominator=(b.Y-c.Y)*(a.X-c.X)+(c.X-b.X)*(a.Y-c.Y);if(Math.Abs(denominator)<.0001f)return;
                int x0=Math.Max(0,(int)Math.Floor(Math.Min(a.X,Math.Min(b.X,c.X)))),x1=Math.Min(Side-1,(int)Math.Ceiling(Math.Max(a.X,Math.Max(b.X,c.X))));
                int y0=Math.Max(0,(int)Math.Floor(Math.Min(a.Y,Math.Min(b.Y,c.Y)))),y1=Math.Min(Side-1,(int)Math.Ceiling(Math.Max(a.Y,Math.Max(b.Y,c.Y))));
                for(int y=y0;y<=y1;y++)for(int x=x0;x<=x1;x++) {
                    float pa=((b.Y-c.Y)*(x+.5f-c.X)+(c.X-b.X)*(y+.5f-c.Y))/denominator;
                    float pb=((c.Y-a.Y)*(x+.5f-c.X)+(a.X-c.X)*(y+.5f-c.Y))/denominator,pc=1-pa-pb;
                    if(pa<-.0001f||pb<-.0001f||pc<-.0001f)continue;
                    float u=Math.Max(0,Math.Min(TextureSide-1,pa*ua.X+pb*ub.X+pc*uc.X));
                    float v=Math.Max(0,Math.Min(TextureSide-1,pa*ua.Y+pb*ub.Y+pc*uc.Y));
                    int ix=(int)u,iy=(int)v,rx=Math.Min(ix+1,TextureSide-1),by=Math.Min(iy+1,TextureSide-1);
                    float fx=u-ix,fy=v-iy,w0=(1-fx)*(1-fy),w1=fx*(1-fy),w2=(1-fx)*fy,w3=fx*fy;
                    int p0=(iy*TextureSide+ix)*4,p1=(iy*TextureSide+rx)*4,p2=(by*TextureSide+ix)*4,p3=(by*TextureSide+rx)*4,target=(y*Side+x)*4;
                    float alpha=input[p0+3]*w0+input[p1+3]*w1+input[p2+3]*w2+input[p3+3]*w3;
                    if(alpha<1)continue;
                    for(int channel=0;channel<4;channel++)output[target+channel]=(byte)Math.Min(255,input[p0+channel]*w0+input[p1+channel]*w1+input[p2+channel]*w2+input[p3+channel]*w3);
                }
            }
        }
    }
    public static class LiveFlame {
        static string Suffix {get{return "-"+Environment.UserName;}}
        public static string EventName {get{return @"Local\FlameCursorPhysicsStop"+Suffix;}}
        public static string MutexName {get{return @"Local\FlameCursorPhysics"+Suffix;}}
        public static void Stop() {
            try{using(var signal=EventWaitHandle.OpenExisting(EventName))signal.Set();}catch(WaitHandleCannotBeOpenedException){}
            try{using(var mutex=Mutex.OpenExisting(MutexName)) {
                bool held=false;try{held=mutex.WaitOne(3000);}catch(AbandonedMutexException){held=true;}
                if(!held)throw new IOException("The reactive flame is still stopping. Please try again.");
                mutex.ReleaseMutex();
            }}catch(WaitHandleCannotBeOpenedException){}
        }
        public static void Start(Settings s) {
            Stop();if(!s.Enabled||!s.ReactiveFlame)return;
            string exe=File.Exists(Integration.Exe)?Integration.Exe:System.Reflection.Assembly.GetExecutingAssembly().Location;
            Process.Start(new ProcessStartInfo(exe,"--physics"){UseShellExecute=false,CreateNoWindow=true,WindowStyle=ProcessWindowStyle.Hidden});
        }
        public static void Run() {
            bool created;using(var mutex=new Mutex(true,MutexName,out created)) {
                if(!created)return;
                using(var signal=new EventWaitHandle(false,EventResetMode.ManualReset,EventName)) {
                    signal.Reset();Settings s=Store.Load();if(!s.Enabled||!s.ReactiveFlame)return;
                    Application.EnableVisualStyles();Application.SetCompatibleTextRenderingDefault(false);
                    using(var window=new FlameWindow(s,signal))Application.Run(window);
                }
                mutex.ReleaseMutex();
            }
        }
    }
    sealed class NativeFrameCursor : IDisposable {
        readonly int side,hotspot;
        readonly byte[] pixels;
        IntPtr color,mask,bits;
        public NativeFrameCursor(int size,int hot) {
            side=size;hotspot=hot;pixels=new byte[side*side*4];
            var header=new OverlayNative.BitmapInfo{Size=40,Width=side,Height=-side,Planes=1,Bits=32};
            color=OverlayNative.CreateDIBSection(IntPtr.Zero,ref header,0,out bits,IntPtr.Zero,0);
            mask=OverlayNative.CreateBitmap(side,side,1,1,IntPtr.Zero);
            if(color==IntPtr.Zero||mask==IntPtr.Zero)throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error());
        }
        public IntPtr Create(Bitmap image) {
            var data=image.LockBits(new Rectangle(0,0,side,side),ImageLockMode.ReadOnly,PixelFormat.Format32bppPArgb);
            try{Marshal.Copy(data.Scan0,pixels,0,pixels.Length);Marshal.Copy(pixels,0,bits,pixels.Length);}finally{image.UnlockBits(data);}
            // These reusable bitmaps are never selected in a DC. Windows copies them.
            var info=new Native.IconInfo{Icon=false,HotX=(uint)hotspot,HotY=(uint)hotspot,Color=color,Mask=mask};
            IntPtr cursor=OverlayNative.CreateIconIndirect(ref info);
            if(cursor==IntPtr.Zero)throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error());return cursor;
        }
        public void Publish(Bitmap image) {
            IntPtr cursor=Create(image);
            if(!OverlayNative.SetSystemCursor(cursor,32512)){int error=Marshal.GetLastWin32Error();Native.DestroyCursor(cursor);throw new System.ComponentModel.Win32Exception(error);}
        }
        public void Dispose(){if(color!=IntPtr.Zero){Native.DeleteObject(color);color=IntPtr.Zero;}if(mask!=IntPtr.Zero){Native.DeleteObject(mask);mask=IntPtr.Zero;}}
    }
    sealed class FlameWindow : Form {
        readonly Settings settings;
        readonly Geometry geometry;
        readonly Bitmap[] frames=new Bitmap[Renderer.Frames];
        readonly Bitmap[] arrows=new Bitmap[Renderer.Frames];
        readonly System.Windows.Forms.Timer timer=new System.Windows.Forms.Timer();
        readonly Stopwatch clock=Stopwatch.StartNew();
        readonly FlameMotion motion=new FlameMotion();
        readonly EventWaitHandle signal;
        readonly Bitmap canvas;
        readonly int side,origin;
        readonly NativeFrameCursor nativeFrame;
        double last,nextConfig;
        bool showing,published;
        public FlameWindow(Settings value,EventWaitHandle stop) {
            settings=value;signal=stop;geometry=new Geometry(value);
            side=((int)Math.Ceiling(geometry.Side*3.6)+31)/32*32;origin=side/2;
            canvas=new Bitmap(side,side,PixelFormat.Format32bppPArgb);
            for(int i=0;i<frames.Length;i++) {
                frames[i]=Renderer.Render(value,i/(double)frames.Length,geometry,false,true,true);
                arrows[i]=Renderer.Render(value,i/(double)frames.Length,geometry,true,false);
            }
            FormBorderStyle=FormBorderStyle.None;ShowInTaskbar=false;StartPosition=FormStartPosition.Manual;
            AutoScaleMode=AutoScaleMode.None;Bounds=new Rectangle(-10000,-10000,side,side);TopMost=true;
            nativeFrame=new NativeFrameCursor(side,origin);
            timer.Interval=16;timer.Tick+=Tick;
        }
        protected override bool ShowWithoutActivation {get{return true;}}
        protected override CreateParams CreateParams {get{var p=base.CreateParams;p.ExStyle|=0x80000|0x20|0x80|0x8000000;return p;}}
        protected override void WndProc(ref Message m){if(m.Msg==0x84){m.Result=new IntPtr(-1);return;}if(m.Msg==0x21){m.Result=new IntPtr(3);return;}base.WndProc(ref m);}
        protected override void OnShown(EventArgs e){base.OnShown(e);last=clock.Elapsed.TotalSeconds;timer.Start();}
        void Tick(object sender,EventArgs args) {
            try {
                if(signal.WaitOne(0)){Close();return;}
                double now=clock.Elapsed.TotalSeconds,dt=now-last;last=now;
                if(now>nextConfig){nextConfig=now+.5;if(!Store.Load().Enabled){Close();return;}}
                var cursor=new OverlayNative.CursorInfo{Size=Marshal.SizeOf(typeof(OverlayNative.CursorInfo))};
                if(!OverlayNative.GetCursorInfo(ref cursor))return;
                float length=FireDrawing.Length(geometry);
                bool normal=(cursor.Flags&1)!=0&&cursor.Cursor==OverlayNative.LoadCursor(IntPtr.Zero,new IntPtr(32512));
                var position=new PointF(cursor.Position.X,cursor.Position.Y);
                if(!normal&&published){showing=false;motion.Reset(position,length);return;}
                if(!showing)motion.Reset(position,length);else motion.Update(position,dt,length,settings.PhysicsSensitivity);
                using(Graphics graphics=Graphics.FromImage(canvas)) {
                    graphics.Clear(Color.Transparent);
                    int frame=(int)(now/Renderer.Duration*frames.Length)%frames.Length;
                    FireDrawing.Draw(graphics,frames[frame],geometry,new PointF(origin,origin),motion);
                    graphics.DrawImageUnscaled(arrows[frame],origin-geometry.HotX,origin-geometry.HotY);
                }
                // One cursor bitmap contains both layers. Windows owns its POSITION;
                // only the shape is refreshed here, so speed cannot separate the base.
                nativeFrame.Publish(canvas);published=true;showing=normal;
            }catch(Exception e){try{File.WriteAllText(Path.Combine(Store.Home,"physics-error.txt"),e.ToString());}catch{}Close();}
        }
        protected override void Dispose(bool disposing) {
            if(disposing){timer.Stop();timer.Dispose();foreach(var frame in frames)if(frame!=null)frame.Dispose();foreach(var arrow in arrows)if(arrow!=null)arrow.Dispose();canvas.Dispose();nativeFrame.Dispose();FireDrawing.Release();}
            base.Dispose(disposing);
        }
        public static void Verify(string directory) {
            IntPtr foreground=OverlayNative.GetForegroundWindow();
            using(var stop=new EventWaitHandle(false,EventResetMode.ManualReset))using(var window=new FlameWindow(new Settings(),stop)) {
                IntPtr handle=window.Handle;long style=OverlayNative.GetWindowLong(handle,-20);
                long required=0x80000|0x20|0x80|0x8000000;
                if((style&required)!=required)throw new Exception("Overlay must be layered, click-through, hidden from taskbar and nonactivating.");
                if(OverlayNative.SendMessage(handle,0x84,IntPtr.Zero,IntPtr.Zero).ToInt32()!=-1)throw new Exception("Overlay hit-test did not pass through.");
                if(OverlayNative.SendMessage(handle,0x21,IntPtr.Zero,IntPtr.Zero).ToInt32()!=3)throw new Exception("Overlay can steal activation.");
                float length=FireDrawing.Length(window.geometry);window.motion.Reset(PointF.Empty,length);
                for(int i=1;i<=120;i++)window.motion.Update(new PointF(0,i*20),1.0/60,length);
                if(window.motion.Tail.Y>=0)throw new Exception("Moving downward must pull the flame upward.");
                using(var graphics=Graphics.FromImage(window.canvas)) {
                    graphics.Clear(Color.Transparent);
                    FireDrawing.Draw(graphics,window.frames[30],window.geometry,new PointF(window.origin,window.origin),window.motion);
                    using(var arrow=Renderer.Render(window.settings,.25,window.geometry,true,false))graphics.DrawImage(arrow,window.origin-window.geometry.HotX,window.origin-window.geometry.HotY);
                }
                window.canvas.Save(Path.Combine(directory,"reactive-flame-downward-motion.png"),ImageFormat.Png);
                IntPtr combined=window.nativeFrame.Create(window.canvas);
                try {
                    Native.IconInfo info;if(!Native.GetIconInfo(combined,out info))throw new Exception("Combined native cursor did not load.");
                    try {
                        if(info.HotX!=window.origin||info.HotY!=window.origin)throw new Exception("Combined cursor click point moved.");
                        using(var bitmap=Bitmap.FromHbitmap(info.Color))if(bitmap.Width!=window.side||bitmap.Height!=window.side)throw new Exception("Windows resized the combined cursor.");
                    }finally{Native.DeleteObject(info.Color);Native.DeleteObject(info.Mask);}
                }finally{Native.DestroyCursor(combined);}
                if(OverlayNative.GetForegroundWindow()!=foreground)throw new Exception("Cursor engine changed foreground focus.");
                for(int i=0;i<240;i++)window.motion.Update(new PointF(0,2400),1.0/60,length);
                if(Math.Abs(window.motion.Tail.Y-ArrowShape.FlameY*length)>1||Math.Abs(window.motion.Tail.X-ArrowShape.FlameX*length)>1)throw new Exception("Flame must settle after movement stops.");
                if(Math.Abs(Math.Atan2(ArrowShape.FlameX,ArrowShape.FlameY)-ArrowShape.FlameAngle)>.000001||Math.Abs(Math.Atan2(ArrowShape.RootX,ArrowShape.RootY)-ArrowShape.FlameAngle)>.000001)throw new Exception("Flame origin and initial direction must bisect the arrow tip.");
                var soft=new FlameMotion();var strong=new FlameMotion();soft.Reset(PointF.Empty,length);strong.Reset(PointF.Empty,length);
                for(int i=1;i<=120;i++){soft.Update(new PointF(0,i*4),1.0/60,length,25);strong.Update(new PointF(0,i*4),1.0/60,length,400);}
                if(soft.Tail.Y<=0||strong.Tail.Y>=0)throw new Exception("Higher sensitivity must bend upward during smaller movements.");
                if(window.motion.At(0,length)!=PointF.Empty)throw new Exception("The flame base must stay fixed.");
                var watch=Stopwatch.StartNew();
                for(int i=0;i<60;i++)using(var graphics=Graphics.FromImage(window.canvas)){graphics.Clear(Color.Transparent);FireDrawing.Draw(graphics,window.frames[i],window.geometry,new PointF(window.origin,window.origin),window.motion);}
                File.WriteAllText(Path.Combine(directory,"physics-results.txt"),"PASS: downward motion bends upward, anchored root, spring settles, exact 21.2133-degree arrow bisector; sensitivity 400% bends upward at 240px/sec where 25% remains downward. Arrow+flame in a single native cursor bitmap with exact hotspot and preserved dimensions. No screen-positioned flame window. Hidden engine does not activate or intercept input.\nCached live drawing average: "+(watch.Elapsed.TotalMilliseconds/60).ToString("F2")+" ms/frame. Native canvas "+window.side+"px.");
            }
        }
    }
    static class OverlayNative {
        [StructLayout(LayoutKind.Sequential)] public struct Point{public int X,Y;}
        [StructLayout(LayoutKind.Sequential)] public struct Size{public int Width,Height;}
        [StructLayout(LayoutKind.Sequential)] public struct CursorInfo{public int Size;public uint Flags;public IntPtr Cursor;public Point Position;}
        [StructLayout(LayoutKind.Sequential,Pack=1)] public struct Blend{public byte Operation,Flags,Alpha,Format;}
        [StructLayout(LayoutKind.Sequential)] public struct BitmapInfo{public uint Size;public int Width,Height;public ushort Planes,Bits;public uint Compression,ImageSize;public int X,Y;public uint Used,Important;}
        [DllImport("user32.dll")] public static extern bool GetCursorInfo(ref CursorInfo info);
        [DllImport("user32.dll")] public static extern IntPtr LoadCursor(IntPtr instance,IntPtr name);
        [DllImport("user32.dll")] public static extern IntPtr GetForegroundWindow();
        [DllImport("user32.dll",EntryPoint="GetWindowLongW")] public static extern int GetWindowLong(IntPtr window,int index);
        [DllImport("user32.dll")] public static extern IntPtr SendMessage(IntPtr window,uint message,IntPtr wparam,IntPtr lparam);
        [DllImport("gdi32.dll")] public static extern IntPtr CreateDIBSection(IntPtr dc,ref BitmapInfo info,uint usage,out IntPtr bits,IntPtr section,uint offset);
        [DllImport("gdi32.dll",SetLastError=true)] public static extern IntPtr CreateBitmap(int width,int height,uint planes,uint bits,IntPtr pixels);
        [DllImport("user32.dll",SetLastError=true)] public static extern IntPtr CreateIconIndirect(ref Native.IconInfo info);
        [DllImport("user32.dll",SetLastError=true)] public static extern bool SetSystemCursor(IntPtr cursor,uint id);
    }
}
