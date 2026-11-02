namespace SimpleBanking;
internal sealed class BtxPhone : Control
{
    readonly System.Windows.Forms.Timer blink=new(){Interval=600};bool on=true;
    internal BtxPhone(Action toggle){
        SetStyle(ControlStyles.SupportsTransparentBackColor|ControlStyles.UserPaint|ControlStyles.StandardClick|ControlStyles.StandardDoubleClick|ControlStyles.OptimizedDoubleBuffer,true);BackColor=Color.Transparent;Size=new(24,24);AccessibleName="BTX-Röhreneffekt umschalten";Cursor=Cursors.Hand;
        blink.Tick+=(_,_)=>{on=!on;Invalidate();};VisibleChanged+=(_,_)=>{if(Visible)blink.Start();else blink.Stop();};DoubleClick+=(_,_)=>toggle();
    }
    protected override void OnPaint(PaintEventArgs e){if(!on)return;using var font=new Font("Segoe UI Symbol",14*DeviceDpi/96f,GraphicsUnit.Pixel);TextRenderer.DrawText(e.Graphics,"☎",font,ClientRectangle,ThemeStyling.Current.Ink,TextFormatFlags.HorizontalCenter|TextFormatFlags.VerticalCenter);}
    protected override void Dispose(bool disposing){if(disposing)blink.Dispose();base.Dispose(disposing);}
}
