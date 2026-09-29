using System.Text.Json;
using System.Text.RegularExpressions;
namespace SimpleBanking;

internal static class MerchantArtwork
{
    static readonly Dictionary<string,string> map = LoadMap();
    static readonly string[] needles = map.Keys.OrderByDescending(k => k.Length).ThenBy(k => k, StringComparer.Ordinal).ToArray();
    static readonly string[] intermediaries = ["klarna","paypal","wise","stripe","mollie","apple pay","google pay","giropay"];
    static Dictionary<string,string> LoadMap() {
        using var stream = typeof(MerchantArtwork).Assembly.GetManifestResourceStream("SimpleBanking.MerchantLogos.json")!;
        return JsonSerializer.Deserialize<Dictionary<string,string>>(stream)!;
    }
    internal static string? Resolve(string merchant, string purpose)
    {
        string key = merchant.ToLowerInvariant();
        string? Search(string text) => needles.FirstOrDefault(n => !intermediaries.Contains(n) && Regex.IsMatch(text, @"(?<!\p{L})" + Regex.Escape(n) + @"(?!\p{L})", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant));
        string resolved = intermediaries.Any(key.StartsWith) ? Search(purpose) ?? key : map.ContainsKey(key) ? key : Search(merchant) ?? Search(purpose) ?? key;
        return map.GetValueOrDefault(resolved);
    }
    internal static bool HasBundled(Entry entry)=>Resolve(entry.Merchant,entry.Purpose) is {} name&&File.Exists(Path.Combine(AppContext.BaseDirectory,"Assets","MerchantLogos",name+".png"));
    internal static Image? Load(Entry entry) {
        if((entry.CustomLogo ?? entry.MerchantLogo) is {Length:>0} bytes){try{using var stream=new MemoryStream(bytes);using var customSource=Image.FromStream(stream);return new Bitmap(customSource);}catch(ArgumentException){}}
        var name = Resolve(entry.Merchant,entry.Purpose); if (name == null) return RemoteMerchantLogos.Shared.Load(entry.Merchant);
        var path = Path.Combine(AppContext.BaseDirectory,"Assets","MerchantLogos",name+".png");
        if (!File.Exists(path)) return RemoteMerchantLogos.Shared.Load(entry.Merchant);
        using var source = Image.FromFile(path); return new Bitmap(source);
    }
}
