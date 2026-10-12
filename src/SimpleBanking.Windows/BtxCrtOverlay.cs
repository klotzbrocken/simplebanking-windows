using System.Drawing.Imaging;
using System.Runtime.InteropServices;
namespace SimpleBanking;

// CPU translation of BTXCRT.metal. A click-through layered window keeps native
// text fields and scroll views intact, just as the Mac overlay does.
internal sealed class BtxCrtOverlay : Form
{
    readonly Form target;
    readonly System.Windows.Forms.Timer timer=new(){Interval=42};
    internal BtxCrtOverlay(Form target){
        this.target=target;FormBorderStyle=FormBorderStyle.None;ShowInTaskbar=false;StartPosition=FormStartPosition.Manual;
        timer.Tick+=(_,_)=>Render();target.VisibleChanged+=Visibility;target.FormClosed+=TargetClosed;
        Show(target);timer.Start();Render();
    }
    protected override bool ShowWithoutActivation=>true;
    protected override CreateParams CreateParams {get{var p=base.CreateParams;p.ExStyle|=0x80000|0x20|0x08000000|0x80;return p;}}
    protected override void WndProc(ref Message m){if(m.Msg==0x84){m.Result=new IntPtr(-1);return;}if(m.Msg==0x21){m.Result=new IntPtr(3);return;}base.WndProc(ref m);}
    void TargetClosed(object? sender,FormClosedEventArgs e)=>Dispose();
    void Visibility(object? sender,EventArgs e){if(target.Visible&&target.WindowState!=FormWindowState.Minimized){Show();timer.Start();}else{timer.Stop();Hide();}}
    void Render(){
        if(target.IsDisposed||!target.Visible||target.WindowState==FormWindowState.Minimized){Hide();return;}
        var size=target.ClientSize;if(size.Width<=0||size.Height<=0||size.Width*size.Height>8_000_000)return;
        if(!Visible)Show();
        var position=target.PointToScreen(Point.Empty);Bounds=new Rectangle(position,size);
        using var bitmap=Frame(size,Environment.TickCount64/1000f,target.DeviceDpi/96f);
        IntPtr screen=GetDC(IntPtr.Zero),dc=CreateCompatibleDC(screen),image=bitmap.GetHbitmap(Color.FromArgb(0)),previous=SelectObject(dc,image);
        try{var destination=position;var source=Point.Empty;var blend=new Blend{Operation=0,Alpha=255,Format=1};UpdateLayeredWindow(Handle,screen,ref destination,ref size,dc,ref source,0,ref blend,2);}
        finally{SelectObject(dc,previous);DeleteObject(image);DeleteDC(dc);ReleaseDC(IntPtr.Zero,screen);}
    }
    internal static Bitmap Frame(Size size,float time,float scale=1){
        var bitmap=new Bitmap(size.Width,size.Height,PixelFormat.Format32bppPArgb);var pixels=new int[size.Width*size.Height];
        float flicker=1+.10f*MathF.Sin(time*100*MathF.PI);
        for(int y=0;y<size.Height;y++){
            float cy=(y+.5f)/size.Height*2-1,scan=.13f*(.5f+.5f*MathF.Sin((y+.5f)/scale*MathF.PI))*flicker;
            for(int x=0;x<size.Width;x++){
                float cx=x/(float)size.Width*2-1,black=Math.Clamp(scan+.16f*(cx*cx+cy*cy)*(1-scan),0,.85f),alpha=.05f+black*.95f,light=.05f*(1-black)*255;
                int stripe=(int)(x/scale)%3,r=(int)(light*(stripe==0?1:.1f)),g=(int)(light*(stripe==1?1:.1f)),b=(int)(light*(stripe==2?1:.1f));
                pixels[y*size.Width+x]=((int)(alpha*255)<<24)|(r<<16)|(g<<8)|b;
            }
        }
        var data=bitmap.LockBits(new Rectangle(Point.Empty,size),ImageLockMode.WriteOnly,PixelFormat.Format32bppPArgb);
        try{for(int y=0;y<size.Height;y++)Marshal.Copy(pixels,y*size.Width,data.Scan0+y*data.Stride,size.Width);}finally{bitmap.UnlockBits(data);}return bitmap;
    }
    protected override void Dispose(bool disposing){if(disposing){timer.Dispose();target.VisibleChanged-=Visibility;target.FormClosed-=TargetClosed;}base.Dispose(disposing);}
    [StructLayout(LayoutKind.Sequential,Pack=1)]struct Blend {public byte Operation,Flags,Alpha,Format;}
    [DllImport("user32.dll")]static extern IntPtr GetDC(IntPtr hwnd);
    [DllImport("user32.dll")]static extern int ReleaseDC(IntPtr hwnd,IntPtr dc);
    [DllImport("gdi32.dll")]static extern IntPtr CreateCompatibleDC(IntPtr dc);
    [DllImport("gdi32.dll")]static extern IntPtr SelectObject(IntPtr dc,IntPtr obj);
    [DllImport("gdi32.dll")]static extern bool DeleteObject(IntPtr obj);
    [DllImport("gdi32.dll")]static extern bool DeleteDC(IntPtr dc);
    [DllImport("user32.dll")]static extern bool UpdateLayeredWindow(IntPtr hwnd,IntPtr destinationDc,ref Point destination,ref Size size,IntPtr sourceDc,ref Point source,uint colorKey,ref Blend blend,uint flags);
}
