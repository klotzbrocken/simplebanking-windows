using System.Text.RegularExpressions;
namespace SimpleBanking;

internal static class MerchantLogoStore
{
    internal static string? Key(Entry entry) {
        string name=Regex.Replace(entry.Merchant.Trim(),@"\s+"," ").ToUpperInvariant();
        if(string.IsNullOrWhiteSpace(entry.AccountId)||name.Length==0||name=="UNBEKANNT"||name.Contains("INTERMEDIAER")||name.Contains("INTERMEDIÄR"))return null;
        return entry.AccountId.Length+":"+entry.AccountId+name;
    }
    internal static Entry Apply(Entry entry,Dictionary<string,byte[]> logos) => entry with {MerchantLogo=Key(entry) is {} key?logos.GetValueOrDefault(key):null};
    internal static byte[] Normalize(Image source) {
        using var resized=new Bitmap(128,128);
        using(var graphics=Graphics.FromImage(resized)){graphics.Clear(Color.Transparent);ArtworkDrawing.Draw(graphics,source,new Rectangle(0,0,128,128));}
        using var output=new MemoryStream();resized.Save(output,System.Drawing.Imaging.ImageFormat.Png);return output.ToArray();
    }
}

internal sealed partial class MainWindow
{
    Entry SetMerchantLogo(Entry entry,byte[]? logo) {
        if(!Unlocked)throw new InvalidOperationException("Bitte zuerst entsperren.");
        int index=data.Entries.FindIndex(e=>e.Id==entry.Id&&e.AccountId==entry.AccountId);
        if(index<0)throw new InvalidOperationException("Die Buchung ist nicht mehr vorhanden.");
        string key=MerchantLogoStore.Key(data.Entries[index])??throw new InvalidOperationException("Für diesen Empfänger ist kein gemeinsames Logo möglich.");
        var previousLogos=new Dictionary<string,byte[]>(data.MerchantLogos);var previousEntries=data.Entries.ToList();
        try {
            if(logo==null)data.MerchantLogos.Remove(key);else data.MerchantLogos[key]=logo;
            // An explicit shared selection replaces this booking's individual override only.
            data.Entries[index]=data.Entries[index] with{CustomLogo=null};
            ApplyMerchantRules();Save();Populate();return data.Entries[index];
        }catch{data.MerchantLogos=previousLogos;data.Entries=previousEntries;Populate();throw;}
    }
}
