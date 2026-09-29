using System.Reflection;
using System.Runtime.InteropServices;
namespace SimpleBanking;
internal static class AppArtwork
{
    [DllImport("user32.dll")] static extern bool DestroyIcon(IntPtr handle);
    internal static Icon CreateBankTrayIcon(Account? account) {
        using var source=account==null?null:BankArtwork.LoadLogo(account,32);
        using var bitmap=new Bitmap(32,32);
        using(var graphics=Graphics.FromImage(bitmap)){
            graphics.Clear(Color.Transparent);
            if(source!=null)ArtworkDrawing.Draw(graphics,source,new Rectangle(0,0,32,32));
            else {using var pen=new Pen(Color.SteelBlue,2);graphics.DrawPolygon(pen,new Point[]{new(3,10),new(16,3),new(29,10)});foreach(int x in new[]{7,16,25})graphics.DrawLine(pen,x,13,x,25);graphics.DrawLine(pen,3,29,29,29);}
        }
        var handle=bitmap.GetHicon();try{return (Icon)Icon.FromHandle(handle).Clone();}finally{DestroyIcon(handle);}
    }
    internal static Icon CreateTrayIcon()
    {
        using var source = Assembly.GetExecutingAssembly().GetManifestResourceStream("SimpleBanking.AppMark.png");
        if (source == null) return (Icon)SystemIcons.Application.Clone();
        using var original = Image.FromStream(source);
        using var bitmap = new Bitmap(original, 32, 32);
        var handle = bitmap.GetHicon();
        try { return (Icon)Icon.FromHandle(handle).Clone(); }
        finally { DestroyIcon(handle); }
    }
}
