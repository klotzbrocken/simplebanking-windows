using System.IO.Compression;
using System.Text;
namespace SimpleBanking;
internal sealed record ThemePackage(Dictionary<string,string> Values,Dictionary<string,byte[]> Images)
{
    internal static readonly string[] ImageKeys=["logo","logoDark","wallpaper","wallpaperDark","wallpaperFlyout","wallpaperFlyoutDark","wallpaperWide","wallpaperWideDark","ringImage","ringImageDark"];
    static byte[] ReadEntry(Stream stream,int limit){using var output=new MemoryStream();var buffer=new byte[8192];int count;while((count=stream.Read(buffer))>0){if(output.Length+count>limit)throw new InvalidDataException("Theme-Datei überschreitet die Größenbegrenzung.");output.Write(buffer,0,count);}return output.ToArray();}
    internal static ThemePackage Read(string path){
        if(new FileInfo(path).Length>40*1024*1024)throw new InvalidDataException("Theme-Pakete dürfen höchstens 40 MB groß sein.");
        if(Path.GetExtension(path).Equals(".cfg",StringComparison.OrdinalIgnoreCase)){using var file=File.OpenRead(path);return Validate(ThemeFiles.Read(Encoding.UTF8.GetString(ReadEntry(file,65536))),new(StringComparer.OrdinalIgnoreCase));}
        using var archive=ZipFile.OpenRead(path);if(archive.Entries.Count>128)throw new InvalidDataException("Zu viele Dateien im Theme-Paket.");
        var files=new Dictionary<string,byte[]>(StringComparer.OrdinalIgnoreCase);long total=0;
        foreach(var entry in archive.Entries){
            var parts=entry.FullName.Replace('\\','/').Split('/');
            if(parts.Any(p=>p=="..")||entry.FullName.Contains(':')||entry.FullName.StartsWith('/')||((entry.ExternalAttributes>>16)&0xf000)==0xa000)throw new InvalidDataException("Ungültiger Dateipfad im Theme-Paket.");
            if(parts.Any(p=>p.StartsWith('.')||p=="__MACOSX")||entry.Name.Length==0)continue;
            total+=entry.Length;if(total>24*1024*1024)throw new InvalidDataException("Entpackte Theme-Dateien dürfen höchstens 24 MB groß sein.");
            string name=parts[^1],ext=Path.GetExtension(name).ToLowerInvariant();if(ext is not (".cfg" or ".png"))continue;
            if(files.ContainsKey(name))throw new InvalidDataException("Doppelter Dateiname im Paket: "+name);
            using var input=entry.Open();files[name]=ReadEntry(input,ext==".cfg"?65536:4*1024*1024);
        }
        var configs=files.Where(p=>Path.GetExtension(p.Key).Equals(".cfg",StringComparison.OrdinalIgnoreCase)).ToArray();
        if(configs.Length!=1)throw new InvalidDataException("Das Paket muss genau eine CFG-Datei enthalten.");
        var values=ThemeFiles.Read(Encoding.UTF8.GetString(configs[0].Value));files.Remove(configs[0].Key);return Validate(values,files);
    }
    static ThemePackage Validate(Dictionary<string,string> values,Dictionary<string,byte[]> files){
        var images=new Dictionary<string,byte[]>(StringComparer.OrdinalIgnoreCase);
        foreach(string key in ImageKeys){
            if(!values.TryGetValue(key,out var name)||string.IsNullOrWhiteSpace(name))continue;
            if(name!=Path.GetFileName(name)||name.Contains('\\')||name.Contains('/')||name.Contains(':'))throw new InvalidDataException("Theme-Grafiken benötigen einfache Dateinamen.");
            if(!Path.GetExtension(name).Equals(".png",StringComparison.OrdinalIgnoreCase))throw new InvalidDataException("Die Grafik „"+name+"“ wird noch nicht unterstützt. Bitte eine PNG-Version verwenden.");
            if(!files.TryGetValue(name,out var bytes))throw new InvalidDataException("Die Grafik „"+name+"“ fehlt. Bitte CFG und PNG gemeinsam als ZIP importieren.");
            if(bytes.Length<33||!bytes.AsSpan(0,8).SequenceEqual(new byte[]{137,80,78,71,13,10,26,10}))throw new InvalidDataException("Keine gültige PNG-Grafik: "+name);
            uint width=System.Buffers.Binary.BinaryPrimitives.ReadUInt32BigEndian(bytes.AsSpan(16,4)),height=System.Buffers.Binary.BinaryPrimitives.ReadUInt32BigEndian(bytes.AsSpan(20,4));
            if(width==0||height==0||width>4096||height>4096||(long)width*height>8_000_000)throw new InvalidDataException("Theme-Grafik ist zu groß (maximal 4096 Pixel / 8 Megapixel).");
            using var stream=new MemoryStream(bytes);using var image=Image.FromStream(stream,true,true);
            if(image.Width>4096||image.Height>4096||(long)image.Width*image.Height>8_000_000)throw new InvalidDataException("Theme-Grafik ist zu groß (maximal 4096 Pixel / 8 Megapixel).");
            images[name]=bytes;
        }
        return new(values,images);
    }
    internal void Write(string path){
        string target=Path.GetFullPath(path),temporary=target+"."+Guid.NewGuid().ToString("N")+".tmp";
        try {
            using(var file=new FileStream(temporary,FileMode.CreateNew)) {
                using var archive=new ZipArchive(file,ZipArchiveMode.Create);
                using(var writer=new StreamWriter(archive.CreateEntry("theme.cfg").Open()))writer.Write(ThemeFiles.Write(Values));
                foreach(var pair in Images){using var stream=archive.CreateEntry(pair.Key).Open();stream.Write(pair.Value);}
            }
            File.Move(temporary,target,true);
        } finally {if(File.Exists(temporary))File.Delete(temporary);}
    }
}
