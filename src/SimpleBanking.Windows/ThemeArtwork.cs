using System.Text;
using System.Xml;
using System.Xml.Linq;
using System.Text.RegularExpressions;
namespace SimpleBanking;
internal static class ThemeArtwork
{
    internal static byte[] Normalize(string name,byte[] bytes){
        string extension=Path.GetExtension(name).ToLowerInvariant();
        using var output=new MemoryStream();
        if(extension==".svg"){
            using var reader=XmlReader.Create(new MemoryStream(bytes),new XmlReaderSettings{DtdProcessing=DtdProcessing.Prohibit,XmlResolver=null,MaxCharactersInDocument=4*1024*1024});
            var xml=XDocument.Load(reader);
            if(xml.Root?.Name.LocalName!="svg")throw new InvalidDataException("Ungültige SVG-Grafik.");
            var nodes=xml.Descendants().ToArray();if(nodes.Length>10000)throw new InvalidDataException("Die SVG-Grafik enthält zu viele Elemente.");
            bool External(string value)=>value.Contains('@')||value.Contains('\\')||Regex.Matches(value,@"url\((.*?)\)",RegexOptions.IgnoreCase).Any(m=>!m.Groups[1].Value.Trim(' ','\t','\r','\n','\'','"').StartsWith('#'));
            foreach(var node in nodes){
                if(node.Name.LocalName is "script" or "foreignObject" or "image" or "use")throw new InvalidDataException("SVG-Themes benötigen eigenständige Pfade ohne aktive oder nachgeladene Inhalte.");
                if(node.Name.LocalName=="style"&&External(node.Value))throw new InvalidDataException("Externe SVG-Stile sind nicht erlaubt.");
                foreach(var attr in node.Attributes())if(attr.Name.LocalName.StartsWith("on",StringComparison.OrdinalIgnoreCase)||attr.Name.LocalName is "href" or "src"||External(attr.Value))throw new InvalidDataException("Externe SVG-Verweise sind nicht erlaubt.");
            }
            var svg=Svg.SvgDocument.FromSvg<Svg.SvgDocument>(xml.ToString());
            var size=svg.GetDimensions();CheckSize(size.Width,size.Height);
            using var image=svg.Draw((int)Math.Ceiling(size.Width),(int)Math.Ceiling(size.Height));image.Save(output,System.Drawing.Imaging.ImageFormat.Png);
        }else if(extension==".pdf"){
            var size=PDFtoImage.Conversion.GetPageSize(bytes);CheckSize((float)size.Width,(float)size.Height);
            PDFtoImage.Conversion.SavePng(output,bytes,options:new PDFtoImage.RenderOptions(Dpi:96,Width:(int)Math.Ceiling(size.Width),Height:(int)Math.Ceiling(size.Height)));
        }else if(extension==".png"){
            if(bytes.Length<24||!bytes.AsSpan(0,8).SequenceEqual(new byte[]{137,80,78,71,13,10,26,10}))throw new InvalidDataException("Ungültige PNG-Grafik.");
            CheckSize(System.Buffers.Binary.BinaryPrimitives.ReadUInt32BigEndian(bytes.AsSpan(16,4)),System.Buffers.Binary.BinaryPrimitives.ReadUInt32BigEndian(bytes.AsSpan(20,4)));
            using var input=new MemoryStream(bytes);using var image=Image.FromStream(input,true,true);CheckSize(image.Width,image.Height);return bytes;
        }else throw new InvalidDataException("Unterstützte Theme-Grafiken: PNG, SVG und PDF.");
        if(output.Length>4*1024*1024)throw new InvalidDataException("Die gerenderte Theme-Grafik überschreitet 4 MB.");return output.ToArray();
    }
    static void CheckSize(float width,float height){if(!float.IsFinite(width)||!float.IsFinite(height)||width<=0||height<=0||width>4096||height>4096||width*height>8_000_000)throw new InvalidDataException("Theme-Grafik ist zu groß (maximal 4096 Pixel / 8 Megapixel).");}
}
