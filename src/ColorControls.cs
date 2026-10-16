using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace FlameCursor {
    public sealed class ColorChoice {
        public int Hue=0,Saturation=100,Brightness=100;
        public ColorChoice() {}
        public ColorChoice(int hue,int saturation,int brightness){Hue=hue;Saturation=saturation;Brightness=brightness;}
        public ColorChoice Copy(){return (ColorChoice)MemberwiseClone();}
        public void Validate(){Hue=Math.Max(0,Math.Min(360,Hue));Saturation=Math.Max(0,Math.Min(100,Saturation));Brightness=Math.Max(0,Math.Min(100,Brightness));}
    }
    public static class Colors {
        public static Color Hsv(double hue,double saturation,double brightness) {
            double h=((hue%360)+360)%360/60,s=Math.Max(0,Math.Min(1,saturation/100)),v=Math.Max(0,Math.Min(1,brightness/100));
            double c=v*s,x=c*(1-Math.Abs(h%2-1)),m=v-c,r=0,g=0,b=0;
            if(h<1){r=c;g=x;}else if(h<2){r=x;g=c;}else if(h<3){g=c;b=x;}
            else if(h<4){g=x;b=c;}else if(h<5){r=x;b=c;}else{r=c;b=x;}
            return Color.FromArgb((int)Math.Round((r+m)*255),(int)Math.Round((g+m)*255),(int)Math.Round((b+m)*255));
        }
    }
    sealed class ColorSlider : Control {
        public string Channel;
        public ColorChoice Context=new ColorChoice();
        public event EventHandler ValueChanged;
        int value;
        public int Maximum {get{return Channel=="Hue"?360:100;}}
        public int Value {get{return value;}set{int n=Math.Max(0,Math.Min(Maximum,value));if(this.value==n)return;this.value=n;Invalidate();if(ValueChanged!=null)ValueChanged(this,EventArgs.Empty);}}
        public ColorSlider(string channel) {
            Channel=channel;AccessibleName=channel;AccessibleRole=AccessibleRole.Slider;TabStop=true;Cursor=Cursors.Hand;
            SetStyle(ControlStyles.UserPaint|ControlStyles.AllPaintingInWmPaint|ControlStyles.OptimizedDoubleBuffer|ControlStyles.ResizeRedraw,true);
            Height=38;
        }
        void Seek(int x){Value=(int)Math.Round((x-9)*Maximum/(double)Math.Max(1,Width-18));}
        protected override void OnMouseDown(MouseEventArgs e){base.OnMouseDown(e);if(e.Button==MouseButtons.Left){Focus();Capture=true;Seek(e.X);}}
        protected override void OnMouseMove(MouseEventArgs e){base.OnMouseMove(e);if(Capture)Seek(e.X);}
        protected override void OnMouseUp(MouseEventArgs e){base.OnMouseUp(e);if(e.Button==MouseButtons.Left)Capture=false;}
        protected override bool IsInputKey(Keys key){return key==Keys.Left||key==Keys.Right||key==Keys.Up||key==Keys.Down||key==Keys.Home||key==Keys.End||base.IsInputKey(key);}
        protected override void OnKeyDown(KeyEventArgs e) {
            int step=e.Shift?10:1;
            if(e.KeyCode==Keys.Left||e.KeyCode==Keys.Down)Value-=step;else if(e.KeyCode==Keys.Right||e.KeyCode==Keys.Up)Value+=step;
            else if(e.KeyCode==Keys.Home)Value=0;else if(e.KeyCode==Keys.End)Value=Maximum;else{base.OnKeyDown(e);return;}e.Handled=true;
        }
        public Color At(double fraction) {
            if(Channel=="Hue")return Colors.Hsv(fraction*360,100,100);
            if(Channel=="Brightness")return Colors.Hsv(Context.Hue,Context.Saturation,fraction*100);
            return Colors.Hsv(Context.Hue,fraction*100,Context.Brightness);
        }
        protected override void OnPaint(PaintEventArgs e) {
            base.OnPaint(e);e.Graphics.SmoothingMode=SmoothingMode.AntiAlias;
            RectangleF box=new RectangleF(8,10,Math.Max(2,Width-16),18);
            using(var path=new GraphicsPath()) {
                path.AddArc(box.X,box.Y,18,18,90,180);path.AddArc(box.Right-18,box.Y,18,18,270,180);path.CloseFigure();
                using(var brush=new LinearGradientBrush(box,Color.Black,Color.White,0f)) {
                    var blend=new ColorBlend(13);for(int i=0;i<13;i++){blend.Positions[i]=i/12f;blend.Colors[i]=At(i/12.0);}brush.InterpolationColors=blend;e.Graphics.FillPath(brush,path);
                }
                using(var edge=new Pen(Color.FromArgb(118,139,132),1))e.Graphics.DrawPath(edge,path);
            }
            float thumb=box.X+(float)(Value/(double)Maximum)*box.Width;
            using(var dark=new Pen(Color.FromArgb(13,20,18),6))e.Graphics.DrawLine(dark,thumb,5,thumb,33);
            using(var light=new Pen(Focused?Color.FromArgb(170,244,202):Color.White,2))e.Graphics.DrawLine(light,thumb,5,thumb,33);
        }
    }
    sealed class ColorEditor : TableLayoutPanel {
        readonly Settings settings;
        readonly Action changed;
        readonly ComboBox selector=new ComboBox();
        readonly ColorSlider hue=new ColorSlider("Hue"),brightness=new ColorSlider("Brightness"),saturation=new ColorSlider("Saturation");
        readonly Label hueNumber=new Label(),brightnessNumber=new Label(),saturationNumber=new Label();
        bool refreshing;
        ColorChoice Selected {get{return selector.SelectedIndex==0?settings.Outline:selector.SelectedIndex==1?settings.Fill:settings.Flame;}}
        public ColorEditor(Settings draft,Action update) {
            settings=draft;changed=update;ColumnCount=1;RowCount=0;Margin=Padding.Empty;Padding=new Padding(8,12,8,0);
            ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));
            Add(new Label{Text="Choose what to color",ForeColor=Color.FromArgb(161,181,173),Dock=DockStyle.Fill},28);
            selector.DropDownStyle=ComboBoxStyle.DropDownList;selector.FlatStyle=FlatStyle.Flat;
            selector.BackColor=Color.FromArgb(29,38,38);selector.ForeColor=Color.White;selector.DrawMode=DrawMode.OwnerDrawFixed;selector.ItemHeight=30;
            selector.AccessibleName="Color target";selector.Items.AddRange(new object[]{"Arrow outline","Arrow fill","Flame"});
            selector.DrawItem+=delegate(object sender,DrawItemEventArgs e){if(e.Index<0)return;
                e.DrawBackground();ColorChoice choice=e.Index==0?settings.Outline:e.Index==1?settings.Fill:settings.Flame;
                using(var swatch=new SolidBrush(Colors.Hsv(choice.Hue,choice.Saturation,choice.Brightness)))e.Graphics.FillRectangle(swatch,e.Bounds.X+10,e.Bounds.Y+7,18,18);
                using(var pen=new Pen(Color.FromArgb(135,155,145)))e.Graphics.DrawRectangle(pen,e.Bounds.X+10,e.Bounds.Y+7,18,18);
                TextRenderer.DrawText(e.Graphics,(string)selector.Items[e.Index],Font,new Point(e.Bounds.X+39,e.Bounds.Y+7),Color.White);e.DrawFocusRectangle();
            };
            Add(selector,42);
            Channel("Hue",hue,hueNumber);Channel("Brightness",brightness,brightnessNumber);Channel("Saturation",saturation,saturationNumber);
            Add(new Label{Text="Flame shades move together, keeping their bright core.\nSmoke stays neutral gray.\n\nChanges appear in the preview. Apply to save them.",ForeColor=Color.FromArgb(161,181,173),Padding=new Padding(0,14,0,0)},100);
            RowStyles.Add(new RowStyle(SizeType.Percent,100));RowCount++;
            selector.SelectedIndexChanged+=delegate{RefreshValues();};
            hue.ValueChanged+=delegate{if(!refreshing){Selected.Hue=hue.Value;Updated();}};
            brightness.ValueChanged+=delegate{if(!refreshing){Selected.Brightness=brightness.Value;Updated();}};
            saturation.ValueChanged+=delegate{if(!refreshing){Selected.Saturation=saturation.Value;Updated();}};
            selector.SelectedIndex=2;
        }
        void Add(Control c,int height){int row=RowCount++;RowStyles.Add(new RowStyle(SizeType.Absolute,height));c.Dock=DockStyle.Fill;Controls.Add(c,0,row);}
        void Channel(string name,ColorSlider slider,Label value) {
            var row=new TableLayoutPanel{ColumnCount=2,RowCount=1,Margin=new Padding(0,10,0,0)};
            row.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));row.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,70));
            row.Controls.Add(new Label{Text=name,Dock=DockStyle.Fill},0,0);value.Dock=DockStyle.Fill;value.TextAlign=ContentAlignment.TopRight;value.ForeColor=Color.FromArgb(161,181,173);row.Controls.Add(value,1,0);
            Add(row,30);Add(slider,38);
        }
        void Updated(){RefreshValues();selector.Invalidate();changed();}
        public void RefreshValues() {
            refreshing=true;ColorChoice color=Selected;
            hue.Context=brightness.Context=saturation.Context=color;
            hue.Value=color.Hue;brightness.Value=color.Brightness;saturation.Value=color.Saturation;
            hueNumber.Text=color.Hue+"°";brightnessNumber.Text=color.Brightness+"%";saturationNumber.Text=color.Saturation+"%";
            hue.Invalidate();brightness.Invalidate();saturation.Invalidate();refreshing=false;selector.Invalidate();
        }
    }
}
