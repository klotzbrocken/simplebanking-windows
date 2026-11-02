using System.Drawing.Text;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
namespace SimpleBanking;
internal static class ThemeFonts
{
    // GDI+ needs the backing bytes for the lifetime of its private font collection.
    sealed class Loaded : IDisposable {
        internal readonly PrivateFontCollection Collection=new();
        IntPtr memory;
        IntPtr gdi;
        internal Loaded(byte[] bytes){memory=Marshal.AllocCoTaskMem(bytes.Length);try{Marshal.Copy(bytes,0,memory,bytes.Length);Collection.AddMemoryFont(memory,bytes.Length);uint count=0;gdi=AddFontMemResourceEx(memory,(uint)bytes.Length,IntPtr.Zero,ref count);if(Collection.Families.Length==0||gdi==IntPtr.Zero)throw new InvalidDataException("Ungültige Theme-Schrift.");}catch{Dispose();throw;}}
        public void Dispose(){Collection.Dispose();if(gdi!=IntPtr.Zero){RemoveFontMemResourceEx(gdi);gdi=IntPtr.Zero;}if(memory!=IntPtr.Zero){Marshal.FreeCoTaskMem(memory);memory=IntPtr.Zero;}}
    }
    static readonly Dictionary<string,Loaded> cache=new();
    [DllImport("gdi32.dll")]static extern IntPtr AddFontMemResourceEx(IntPtr font,uint length,IntPtr reserved,ref uint count);
    [DllImport("gdi32.dll")]static extern bool RemoveFontMemResourceEx(IntPtr font);
    static ThemeFonts(){AppDomain.CurrentDomain.ProcessExit+=(_,_)=>{foreach(var font in cache.Values)font.Dispose();};}
    internal static void Validate(byte[] bytes){if(bytes.Length<12||bytes.Length>4*1024*1024)throw new InvalidDataException("Ungültige Schriftgröße.");using var font=new Loaded(bytes);}
    internal static Font Create(byte[] bytes,float size,bool bold){
        string key=Convert.ToHexString(SHA256.HashData(bytes));
        if(!cache.TryGetValue(key,out var font)){font=new Loaded(bytes);cache.Add(key,font);}
        var family=font.Collection.Families[0];var style=bold&&family.IsStyleAvailable(FontStyle.Bold)?FontStyle.Bold:FontStyle.Regular;
        return new(family,size,style,GraphicsUnit.Pixel);
    }
}
