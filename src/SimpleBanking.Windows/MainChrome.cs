using System.Drawing.Drawing2D;

namespace SimpleBanking;
internal sealed partial class MainWindow
{
    Image? themeWallpaper;
    bool wideWindow;
    internal void FitWindowToDisplay(){
        if(IsDisposed||WindowState!=FormWindowState.Normal)return;
        var area=Screen.FromControl(this).WorkingArea;
        var frame=new Size(Width-ClientSize.Width,Height-ClientSize.Height);
        MinimumSize=MaximumSize=Size.Empty;
        ClientSize=WindowPlacement.MainClient(wideWindow,DeviceDpi,frame,area);
        MinimumSize=MaximumSize=Size;
        Location=WindowPlacement.Clamp(Bounds,area);
    }
    protected override void OnShown(EventArgs e){base.OnShown(e);FitWindowToDisplay();}
    protected override void OnDpiChanged(DpiChangedEventArgs e){
        MinimumSize=MaximumSize=Size.Empty;base.OnDpiChanged(e);
        if(IsHandleCreated)BeginInvoke(new Action(()=>{if(!IsDisposed){FitWindowToDisplay();ApplyPreferences();}}));
    }
    internal void ToggleWindowWidth(){
        wideWindow=!wideWindow;
        int center=Left+Width/2;
        FitWindowToDisplay();
        var area=Screen.FromControl(this).WorkingArea;Left=Math.Max(area.Left,Math.Min(center-Width/2,area.Right-Width));
        themeWallpaper?.Dispose();themeWallpaper=Theme.Image(ClientSize.Width/(DeviceDpi/96f)>=594?"wallpaperWide":"wallpaper")??Theme.Image("wallpaper");RefreshEntries();Invalidate(true);
    }
    protected override void WndProc(ref Message m){
        if(m.Msg==0x112&&((long)m.WParam&0xfff0)==0xf030){ToggleWindowWidth();return;}
        base.WndProc(ref m);
        if((m.Msg==0x7e||m.Msg==0x1a)&&IsHandleCreated&&!IsDisposed)
            BeginInvoke(new Action(()=>{if(!IsDisposed)FitWindowToDisplay();}));
    }

    internal void PaintMood(Graphics graphics,Rectangle bounds){
        var p=BalancePresentation.Create(CurrentBalance,Preferences);
        using var brush=new LinearGradientBrush(bounds,p.Top,p.Bottom,90f);
        brush.InterpolationColors=new ColorBlend{Colors=[p.Top,p.Bottom,ThemePalette.Blend(p.Bottom,Theme.Surface,.4)],Positions=[0f,.48f,1f]};
        graphics.FillRectangle(brush,bounds);
        if(themeWallpaper!=null)graphics.DrawImage(themeWallpaper,bounds);
    }
    protected override void OnPaintBackground(PaintEventArgs e){if(Preferences==null){base.OnPaintBackground(e);return;}PaintMood(e.Graphics,ClientRectangle);}
}
