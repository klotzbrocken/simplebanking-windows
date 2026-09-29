using System.Drawing.Drawing2D;

namespace SimpleBanking;
internal sealed partial class MainWindow
{
    Image? themeWallpaper;
    internal void ToggleWindowWidth(){
        int target=(int)((ClientSize.Width/(DeviceDpi/96f)<594?840:360)*(DeviceDpi/96f));
        int center=Left+Width/2;
        MinimumSize=Size.Empty;MaximumSize=Size.Empty;
        ClientSize=new Size(target,ClientSize.Height);MinimumSize=MaximumSize=Size;
        var area=Screen.FromControl(this).WorkingArea;Left=Math.Max(area.Left,Math.Min(center-Width/2,area.Right-Width));
        RefreshEntries();Invalidate(true);
    }
    protected override void WndProc(ref Message m){
        if(m.Msg==0x112&&((long)m.WParam&0xfff0)==0xf030){ToggleWindowWidth();return;}
        base.WndProc(ref m);
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
