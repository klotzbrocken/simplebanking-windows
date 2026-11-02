using System.Diagnostics;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
namespace SimpleBanking;

// Shared geometry from BalanceBar.defaultThemeCard and TransactionsPanelView.defaultThemeBalanceCard.
internal sealed class BalanceCard : Control
{
    readonly MainWindow owner;
    internal bool PrivacyHidden {get;private set;}
    internal void SetPrivacyHidden(bool hidden){if(PrivacyHidden==hidden)return;PrivacyHidden=hidden;RefreshCard();}
    readonly bool flyout;
    readonly Button unlock = new() { Name="UnlockButton", Text="Entsperren", FlatStyle=FlatStyle.Flat, BackColor=Color.White };
    readonly HeaderIcon subtitle;
    readonly BtxPhone phone;
    readonly System.Windows.Forms.Timer animation = new() { Interval=16 };
    readonly Stopwatch clock = new();
    Bitmap? surface;
    PointF origin;
    internal BalanceCard(MainWindow owner, bool flyout) {
        this.owner=owner;this.flyout=flyout;phone=new BtxPhone(()=>{if(owner.Theme.Id!="btx")return;owner.Preferences.BtxCrtEnabled=!owner.Preferences.BtxCrtEnabled;owner.Preferences.Save();owner.ApplyPreferences();}){Visible=false};Controls.Add(phone);Height=140;TabStop=false;
        SetStyle(ControlStyles.StandardClick|ControlStyles.StandardDoubleClick|ControlStyles.UserPaint|ControlStyles.AllPaintingInWmPaint|ControlStyles.OptimizedDoubleBuffer|ControlStyles.ResizeRedraw,true);
        subtitle=new HeaderIcon("subtitle","Anzeige unter dem Kontostand wechseln",()=>{
            int mode=flyout?owner.Preferences.FlyoutSubtitleStyle:owner.Preferences.PanelSubtitleStyle;
            mode=(mode+1)%(owner.CurrentRecurring.BankFee.HasValue?4:3);
            if(flyout)owner.Preferences.FlyoutSubtitleStyle=mode;else owner.Preferences.PanelSubtitleStyle=mode;
            owner.Preferences.Save();RefreshCard();
        });
        Controls.Add(subtitle);Controls.Add(unlock);unlock.Click+=(_,_)=>owner.RequestUnlock();
        owner.BalanceChanged+=RefreshCard;owner.NewBookings+=NewBookings;
        animation.Tick+=(_,_)=>{if(clock.Elapsed.TotalSeconds>=1.6 || !Visible){StopRipple();}else Invalidate();};
        VisibleChanged+=(_,_)=>{if(Visible && owner.Preferences.RippleAlwaysOn)StartRipple();else if(!Visible)StopRipple();};
    }
    void NewBookings(){if(Visible)StartRipple();}
    internal void StartRipple(PointF? point=null) {
        if(owner.IsLocked || !Visible || !owner.Theme.Ripple)return;
        float scale=DeviceDpi/96f;
        origin=point??new PointF((flyout?310:190)*scale,(flyout?130:65)*scale);
        clock.Restart();animation.Start();Invalidate();
    }
    internal bool RippleRunning => animation.Enabled;
    internal void StopRipple(){animation.Stop();clock.Reset();Invalidate();}
    internal void RefreshCard() {
        StopRipple();surface?.Dispose();surface=null;
        float scale=DeviceDpi/96f;
        unlock.SetBounds((int)(16*scale),(int)(98*scale),(int)(180*scale),(int)(34*scale));
        subtitle.SetBounds((int)(16*scale),(int)(102*scale),(int)(18*scale),(int)(18*scale));
        phone.Visible=owner.Theme.Lofi;phone.SetBounds(Width-(int)(30*scale),(int)(14*scale),(int)(24*scale),(int)(24*scale));
        unlock.Visible=owner.IsLocked;unlock.Text=owner.UnlockActionText;
        subtitle.Visible=!owner.IsLocked&&!(owner.BalancesHidden||PrivacyHidden)&&!owner.AllAccountsSelected;
        int mode=flyout?owner.Preferences.FlyoutSubtitleStyle:owner.Preferences.PanelSubtitleStyle;
        subtitle.SetSymbol(mode switch{1=>"metrics",2=>"daily",3=>"bankfees",_=>"subtitle"});
        AccessibleName=owner.IsLocked?"simplebanking gesperrt":"Kontostand";Invalidate();
    }
    protected override void OnSizeChanged(EventArgs e){base.OnSizeChanged(e);surface?.Dispose();surface=null;}
    protected override void OnMouseClick(MouseEventArgs e){base.OnMouseClick(e);StartRipple(e.Location);}
    protected override void OnPaint(PaintEventArgs e) {
        if(Width<=0 || Height<=0)return;
        surface??=CreateSurface();
        if(clock.IsRunning && clock.Elapsed.TotalSeconds is >0 and <1.5) {
            using var frame=Ripple.Render(surface,origin,(float)clock.Elapsed.TotalSeconds,DeviceDpi/96f);
            e.Graphics.DrawImageUnscaled(frame,0,0);
        } else e.Graphics.DrawImageUnscaled(surface,0,0);
    }
    internal Bitmap CreateSurface() {
        var bitmap=new Bitmap(Math.Max(1,Width),Math.Max(1,Height),PixelFormat.Format32bppArgb);
        using var g=Graphics.FromImage(bitmap);
        var presentation=BalancePresentation.Create(owner.CurrentBalance,owner.Preferences);var theme=owner.Theme;
        if(!flyout){var offset=owner.PointToClient(PointToScreen(Point.Empty));owner.PaintMood(g,new Rectangle(-offset.X,-offset.Y,owner.ClientSize.Width,owner.ClientSize.Height));}
        else using(var wash=new LinearGradientBrush(new Rectangle(0,0,bitmap.Width,bitmap.Height),presentation.Top,presentation.Bottom,160f)) {
            g.FillRectangle(wash,0,0,bitmap.Width,bitmap.Height);
        }
        float scale=DeviceDpi/96f;g.ScaleTransform(scale,scale);
        using var wallpaper=flyout?(theme.Image("wallpaperFlyout")??theme.Image("wallpaper")):null;
        if(wallpaper!=null)g.DrawImage(wallpaper,new RectangleF(0,0,Width/scale,Height/scale));
        float width=Width/scale;
        var account=owner.FlyoutAccounts().ElementAtOrDefault(owner.SelectedAccountIndex);
        bool aggregate=owner.AllAccountsSelected;
        using var logo=theme.BankLogos?(theme.Image("logo")??(!aggregate&&account!=null?BankArtwork.LoadLogo(account,40):null)):null;
        if(logo!=null)g.DrawImage(logo,16,16,20,20);
        else if(theme.Lofi)BtxMosaic.Draw(g,new Rectangle(16,16,20,20),"Sonstiges");else if(theme.BankLogos){using var pen=new Pen(presentation.Detail,1.5f);g.DrawRectangle(pen,17,18,18,14);g.DrawRectangle(pen,28,22,8,6);}
        string name=owner.IsLocked?"simplebanking":aggregate?"Alle Konten":account?.Name??"Kontostand";
        string time=!aggregate&&account?.BalanceFetchedAt is {} fetched?" · "+fetched.ToLocalTime().Hour+" Uhr":"";
        using var nameFont=theme.Font(14,true);
        using var detailFont=theme.Font(13);
        using var nameInk=new SolidBrush(theme.Ink);using var detailInk=new SolidBrush(presentation.Detail);
        using var format=new StringFormat {Trimming=StringTrimming.EllipsisCharacter,FormatFlags=StringFormatFlags.NoWrap};
        float timeWidth=g.MeasureString(time,detailFont).Width;
        float nameWidth=Math.Max(0,Math.Min(g.MeasureString(name,nameFont).Width,width-(theme.Lofi?88:60)-timeWidth));
        g.DrawString(theme.Text(name),nameFont,nameInk,new RectangleF(44,16,nameWidth,24),format);
        g.DrawString(time,detailFont,detailInk,new RectangleF(44+nameWidth,17,timeWidth,24),format);
        string amount=owner.IsLocked?(owner.NeedsSetup?"Willkommen":"Gesperrt"):(owner.BalancesHidden||PrivacyHidden)?"••••••":owner.CurrentBalance is {} value?UiFormat.Money(value,account?.Currency??"EUR"):"--,-- €";
        bool ring=(theme.HasRingImage||(theme.AccountRing&&owner.Preferences.MonthRingEnabled))&&!owner.IsLocked&&!(owner.BalancesHidden||PrivacyHidden)&&!aggregate&&account!=null&&!((account.BankName??account.Name).Contains("paypal",StringComparison.OrdinalIgnoreCase));
        float textWidth=width-32-(ring?84:0);
        float size=38;
        while(size>23){using var candidate=theme.Font(size,true);if(g.MeasureString(amount,candidate).Width<=textWidth)break;size--;}
        using var amountFont=theme.Font(size,true);using var amountInk=new SolidBrush(presentation.Ink);
        var amountState=g.Save();if(theme.Lofi){g.TranslateTransform(0,70);g.ScaleTransform(1,1.15f);g.TranslateTransform(0,-70);}
        g.DrawString(amount,amountFont,amountInk,new RectangleF(16,44,textWidth,52),format);g.Restore(amountState);
        if(!(owner.BalancesHidden||PrivacyHidden)) {
            int mode=flyout?owner.Preferences.FlyoutSubtitleStyle:owner.Preferences.PanelSubtitleStyle;
            if(mode==3&&!owner.CurrentRecurring.BankFee.HasValue)mode=0;
            string text=owner.CurrentRecurring.Subtitle(mode,owner.CurrentBalance,account?.Currency??"EUR",DateOnly.FromDateTime(DateTime.Today),aggregate);
            float left=aggregate?16:40;
            g.DrawString(theme.Text(text),detailFont,detailInk,new RectangleF(left,102,width-left-16-(ring?84:0),24),format);
        }
        if(ring){
            using var ringImage=theme.Image("ringImage");if(ringImage!=null){ArtworkDrawing.Draw(g,ringImage,new Rectangle((int)width-88,48,72,72));return bitmap;}
            var state=owner.RingState();var color=state.Dispo||state.Fraction<.34?Color.FromArgb(207,63,70):state.Fraction<.67?Color.FromArgb(220,135,45):Color.FromArgb(37,142,88);
            if(!theme.IsDefault)color=state.Dispo||state.Fraction<.34?theme.Negative:state.Fraction<.67?(theme.Lofi?ColorTranslator.FromHtml("#c08a00"):ThemePalette.Blend(theme.Positive,theme.Negative,.5)):theme.Positive;
            float x=width-88,y=48;g.SmoothingMode=SmoothingMode.AntiAlias;
            if(theme.Lofi){for(int i=0;i<10;i++){using var block=new SolidBrush(i<(int)Math.Round(state.Fraction*10,MidpointRounding.AwayFromZero)?color:ThemePalette.Blend(color,theme.Surface,.18));g.FillRectangle(block,x+6+i*6,y+57,5,8);}}
            else{using var track=new Pen(Color.FromArgb(45,color),7);g.DrawEllipse(track,x+4,y+4,64,64);if(state.Fraction>0){using var arc=new Pen(color,7){StartCap=LineCap.Round,EndCap=LineCap.Round};g.DrawArc(arc,x+4,y+4,64,64,-90,(float)state.Fraction*360);}}
            using var dayFont=theme.Font(theme.Lofi?22:20,true);using var monthFont=theme.Font(theme.Lofi?11:9);using var brush=new SolidBrush(color);using var centered=new StringFormat{Alignment=StringAlignment.Center};
            g.DrawString(DateTime.Today.Day.ToString("00"),dayFont,brush,new RectangleF(x,y+(theme.Lofi?2:18),72,30),centered);g.DrawString(DateTime.Today.ToString("MMM",System.Globalization.CultureInfo.GetCultureInfo("de-DE")).ToUpperInvariant(),monthFont,brush,new RectangleF(x,y+(theme.Lofi?33:43),72,20),centered);
        }
        return bitmap;
    }
    protected override void Dispose(bool disposing){if(disposing){owner.BalanceChanged-=RefreshCard;owner.NewBookings-=NewBookings;animation.Dispose();surface?.Dispose();}base.Dispose(disposing);}
}

// CPU translation of Ripple.metal: identical radial displacement, clamping and light refraction.
internal static class Ripple
{
    internal static Bitmap Render(Bitmap source,PointF origin,float time,float scale=1) {
        int w=source.Width,h=source.Height;
        var input=new int[w*h];var output=new int[w*h];
        var bits=source.LockBits(new Rectangle(0,0,w,h),ImageLockMode.ReadOnly,PixelFormat.Format32bppArgb);
        try{for(int y=0;y<h;y++)Marshal.Copy(bits.Scan0+y*bits.Stride,input,y*w,w);}finally{source.UnlockBits(bits);}
        for(int y=0;y<h;y++)for(int x=0;x<w;x++) {
            float dx=x-origin.X,dy=y-origin.Y,distance=MathF.Sqrt(dx*dx+dy*dy);
            float t=Math.Max(0,time-distance/(650*scale));
            float wave=12*scale*MathF.Sin(10*t)*MathF.Exp(-6*t);
            float sx=Math.Clamp(x+(distance>0?wave*dx/distance:0),0,w-1),sy=Math.Clamp(y+(distance>0?wave*dy/distance:0),0,h-1);
            int x0=(int)sx,y0=(int)sy,x1=Math.Min(w-1,x0+1),y1=Math.Min(h-1,y0+1);
            float fx=sx-x0,fy=sy-y0,light=255*.3f*wave/(12*scale);
            int result=unchecked((int)0xff000000);
            for(int shift=0;shift<=16;shift+=8) {
                float a=((input[y0*w+x0]>>shift)&255)*(1-fx)+((input[y0*w+x1]>>shift)&255)*fx;
                float b=((input[y1*w+x0]>>shift)&255)*(1-fx)+((input[y1*w+x1]>>shift)&255)*fx;
                result|=(int)Math.Clamp(a*(1-fy)+b*fy+light,0,255)<<shift;
            }
            output[y*w+x]=result;
        }
        var frame=new Bitmap(w,h,PixelFormat.Format32bppArgb);
        bits=frame.LockBits(new Rectangle(0,0,w,h),ImageLockMode.WriteOnly,PixelFormat.Format32bppArgb);
        try{for(int y=0;y<h;y++)Marshal.Copy(output,y*w,bits.Scan0+y*bits.Stride,w);}finally{frame.UnlockBits(bits);}
        return frame;
    }
}
