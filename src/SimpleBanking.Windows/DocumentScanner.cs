using System.Globalization;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using Tesseract;
using ZXing;
namespace SimpleBanking;
internal static class DocumentScanner
{
    internal static TransferDraft Parse(string text,string accountId){
        var qr=BankingRules.ParseGiroCode(text,accountId);if(qr!=null)return qr;
        string iban="";foreach(Match m in Regex.Matches(text.ToUpperInvariant(),@"\b[A-Z]{2}[ \t]*\d{2}(?:[ \t]*[A-Z0-9]){11,30}")){string candidate=BankingRules.NormalizeIban(m.Value);for(int n=candidate.Length;n>=15;n--)if(BankingRules.ValidIban(candidate[..n])){iban=candidate[..n];break;}if(iban.Length>0)break;}
        var lines=text.Replace('\r','\n').Split('\n',StringSplitOptions.RemoveEmptyEntries|StringSplitOptions.TrimEntries);
        string Label(string[] markers){foreach(var line in lines){var parts=Regex.Split(line,@"\s*[:=]\s*",RegexOptions.None,TimeSpan.FromMilliseconds(100));if(parts.Length>=2&&markers.Any(m=>parts[0].Contains(m,StringComparison.OrdinalIgnoreCase)))return string.Join(": ",parts.Skip(1));}return "";}
        string name=Label(["Empfänger","Empfaenger","Kontoinhaber","Begünstigter","Beneficiary","Payee","Recipient"]),purpose=Label(["Verwendungszweck","Rechnungsnummer","Rechnung Nr","Referenz","Reference","Invoice"]);
        decimal Amount(string line){var matches=Regex.Matches(line,@"(?<!\d)(?:\d{1,3}(?:[. ]\d{3})+|\d+)[,.]\d{2}(?!\d)");foreach(Match m in matches.Reverse()){string raw=m.Value.Replace(" ","");if(raw.Contains(','))raw=raw.Replace(".","").Replace(',','.');if(decimal.TryParse(raw,NumberStyles.AllowDecimalPoint,CultureInfo.InvariantCulture,out var n)&&n<=100000)return n;}return 0;}
        decimal amount=lines.Where(l=>new[]{"rechnungsbetrag","gesamtbetrag","zahlbetrag","endbetrag","gesamtsumme","zu zahlen","total","summe"}.Any(k=>l.Contains(k,StringComparison.OrdinalIgnoreCase))).Select(Amount).FirstOrDefault(n=>n>0);
        if(amount==0)amount=lines.Where(l=>l.Contains('€')||l.Contains("EUR",StringComparison.OrdinalIgnoreCase)).Select(Amount).DefaultIfEmpty().Max();
        return new(accountId,name,iban,amount,purpose.Length>140?purpose[..140]:purpose);
    }
    internal static string? ReadQr(Bitmap source){
        using var bitmap=new Bitmap(source.Width,source.Height,System.Drawing.Imaging.PixelFormat.Format32bppArgb);using(var graphics=Graphics.FromImage(bitmap))graphics.DrawImageUnscaled(source,0,0);
        var data=bitmap.LockBits(new Rectangle(0,0,bitmap.Width,bitmap.Height),System.Drawing.Imaging.ImageLockMode.ReadOnly,System.Drawing.Imaging.PixelFormat.Format32bppArgb);
        try{var bytes=new byte[bitmap.Width*bitmap.Height*4];for(int y=0;y<bitmap.Height;y++)Marshal.Copy(data.Scan0+y*data.Stride,bytes,y*bitmap.Width*4,bitmap.Width*4);var reader=new BarcodeReaderGeneric{Options=new ZXing.Common.DecodingOptions{TryHarder=true,PossibleFormats=[BarcodeFormat.QR_CODE]}};return reader.Decode(new RGBLuminanceSource(bytes,bitmap.Width,bitmap.Height,RGBLuminanceSource.BitmapFormat.BGRA32))?.Text;}finally{bitmap.UnlockBits(data);}
    }
    internal static string Ocr(byte[] bytes){using var engine=new TesseractEngine(Path.Combine(AppContext.BaseDirectory,"Assets","tessdata"),"deu+eng",EngineMode.Default);using var image=Pix.LoadFromMemory(bytes);using var page=engine.Process(image);return page.GetText();}
    internal static string Read(string path,CancellationToken cancellation=default,IProgress<string>? progress=null){
        if(new FileInfo(path).Length>30*1024*1024)throw new InvalidDataException("Dokument größer als 30 MB.");
        using var engine=new TesseractEngine(Path.Combine(AppContext.BaseDirectory,"Assets","tessdata"),"deu+eng",EngineMode.Default);
        if(Path.GetExtension(path).Equals(".pdf",StringComparison.OrdinalIgnoreCase)){
            byte[] pdf=File.ReadAllBytes(path);int count=PDFtoImage.Conversion.GetPageCount(pdf);
            if(count>20)throw new InvalidDataException("Bitte höchstens 20 Rechnungsseiten pro Dokument verwenden.");
            var extracted=new List<string>();
            for(int i=0;i<count;i++){
                cancellation.ThrowIfCancellationRequested();progress?.Report($"Seite {i+1} von {count} wird erkannt …");
                var size=PDFtoImage.Conversion.GetPageSize(pdf,i);double factor=Math.Min(200d/72,3200/Math.Max(size.Width,size.Height));
                if(size.Width<=0||size.Height<=0||!double.IsFinite(factor))throw new InvalidDataException("Ungültige PDF-Seitengröße.");
                using var png=new MemoryStream();PDFtoImage.Conversion.SavePng(png,pdf,i,options:new PDFtoImage.RenderOptions(Dpi:200,Width:Math.Max(1,(int)(size.Width*factor)),Height:Math.Max(1,(int)(size.Height*factor))));png.Position=0;
                using var bitmap=new Bitmap(png);var text=Recognize(bitmap,engine,cancellation);if(text.StartsWith("BCD\n"))return text;extracted.Add(text);
            }
            return string.Join("\n",extracted);
        }
        using(var bitmap=new Bitmap(path)){if(bitmap.Width>10000||bitmap.Height>10000||bitmap.Width*(long)bitmap.Height>40_000_000)throw new InvalidDataException("Bildabmessungen zu groß.");progress?.Report("Bild wird erkannt …");return Recognize(bitmap,engine,cancellation);}
    }
    static string Recognize(Bitmap source,TesseractEngine engine,CancellationToken cancellation){
        float ratio=Math.Min(1,3200f/Math.Max(source.Width,source.Height));using var bitmap=new Bitmap(source,new Size(Math.Max(1,(int)(source.Width*ratio)),Math.Max(1,(int)(source.Height*ratio))));
        string best="";float score=-1;
        for(int rotation=0;rotation<4;rotation++){
            cancellation.ThrowIfCancellationRequested();if(rotation>0)bitmap.RotateFlip(RotateFlipType.Rotate90FlipNone);
            string? qr=ReadQr(bitmap);if(qr!=null&&BankingRules.ParseGiroCode(qr,"scan")!=null)return qr.Replace("\r\n","\n");
            using var png=new MemoryStream();bitmap.Save(png,System.Drawing.Imaging.ImageFormat.Png);using var pix=Pix.LoadFromMemory(png.ToArray());
            using var deskewed=pix.Deskew();using var page=engine.Process(deskewed,PageSegMode.Auto);string text=page.GetText();float confidence=page.GetMeanConfidence();
            if(text.Count(char.IsLetterOrDigit)<8)confidence=0;
            if(confidence>score){score=confidence;best=text;}if(confidence>.88f)break;
        }
        return best;
    }
}
