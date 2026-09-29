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
    internal static string Read(string path){
        if(new FileInfo(path).Length>30*1024*1024)throw new InvalidDataException("Dokument größer als 30 MB.");
        if(Path.GetExtension(path).Equals(".pdf",StringComparison.OrdinalIgnoreCase)){
            using var document=UglyToad.PdfPig.PdfDocument.Open(path);var pages=document.GetPages().Take(3).ToArray();string text=string.Join("\n",pages.Select(p=>string.Join("\n",p.GetWords().GroupBy(w=>Math.Round(w.BoundingBox.Bottom/5)).OrderByDescending(g=>g.Key).Select(g=>string.Join(" ",g.OrderBy(w=>w.BoundingBox.Left).Select(w=>w.Text))))));
            if(text.Count(char.IsLetterOrDigit)>=20)return text;
            var extracted=new List<string>();foreach(var page in pages)foreach(var image in page.GetImages().Where(i=>i.WidthInSamples<=10000&&i.HeightInSamples<=10000).Take(3))if(image.TryGetPng(out var png)){using var stream=new MemoryStream(png);using var bitmap=new Bitmap(stream);string? qr=ReadQr(bitmap);if(qr?.StartsWith("BCD")==true)return qr;extracted.Add(Ocr(png));}return string.Join("\n",extracted);
        }
        using(var bitmap=new Bitmap(path)){if(bitmap.Width>10000||bitmap.Height>10000)throw new InvalidDataException("Bildabmessungen zu groß.");string? qr=ReadQr(bitmap);if(qr?.StartsWith("BCD")==true)return qr;using var stream=new MemoryStream();bitmap.Save(stream,System.Drawing.Imaging.ImageFormat.Png);return Ocr(stream.ToArray());}
    }
}
