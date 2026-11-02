using System.Reflection;
using System.Text.Json;
namespace SimpleBanking;
internal static class BankArtwork
{
    sealed record Brand(string Id, string[] Keywords, string Asset);
    static readonly Brand[] Brands = Load();
    static Brand[] Load() { using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("SimpleBanking.BankBrands.json"); return stream == null ? [] : JsonSerializer.Deserialize<Brand[]>(stream) ?? []; }
    static string? Asset(string? id) {
        if (string.IsNullOrWhiteSpace(id) || id.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 || id.Contains('/') || id.Contains('\\') || id.Contains("..")) return null;
        id = id.ToLowerInvariant();
        var name = Brands.FirstOrDefault(b => b.Id == id)?.Asset ?? id;
        return File.Exists(Path.Combine(AppContext.BaseDirectory,"Assets","BankLogos",name+".png")) ? name : null;
    }
    internal static string? Resolve(Account account) {
        var direct=Asset(account.LogoId); if(direct!=null)return direct;
        var brand=Brands.FirstOrDefault(b=>b.Keywords.Any(k=>BankingRules.WordMatch((account.BankName+" "+account.Name).ToLowerInvariant(),k.ToLowerInvariant())));
        if(brand!=null && Asset(brand.Id) is {} named)return named;
        var iban=BankingRules.NormalizeIban(account.Iban);
        if(iban.StartsWith("DE") && iban.Length>=12) {
            var blz=iban.Substring(4,8);
            string? id=blz switch {
                "50070010" or "10070000" or "50070024"=>"deutsche-bank",
                "50040000" or "50080000"=>"commerzbank", "50010517"=>"ing", "12030000"=>"dkb",
                "10011001"=>"n26", "10022400"=>"c24", "50010060" or "44010046"=>"postbank",
                "30020900"=>"targobank", "20041111" or "20041133"=>"comdirect", "10077777"=>"norisbank", "10010010"=>"revolut",
                _=>blz[3]=='5'?"sparkasse":blz[3] is '6' or '9'?"volksbank":blz.StartsWith("120")?"dkb":blz.StartsWith("200")?"comdirect":null
            };
            if(Asset(id) is {} byIban)return byIban;
        }
        foreach(var candidate in new[]{account.BankName,account.Name}) {
            var key=(candidate??"").ToLowerInvariant().Replace(" ","").Replace("-","");
            if(Asset(key) is {} catalog)return catalog;
        }
        return null;
    }
    internal static Image? LoadLogo(Account account, int size) {
        var id=Resolve(account); if(id==null)return null;
        using var source=Image.FromFile(Path.Combine(AppContext.BaseDirectory,"Assets","BankLogos",id+".png"));
        var result=new Bitmap(size,size);
        using var g=Graphics.FromImage(result);g.InterpolationMode=System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
        float scale=Math.Min((float)size/source.Width,(float)size/source.Height);
        float w=source.Width*scale,h=source.Height*scale;g.DrawImage(source,(size-w)/2,(size-h)/2,w,h);return result;
    }
}
