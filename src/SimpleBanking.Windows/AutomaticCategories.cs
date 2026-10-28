using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
namespace SimpleBanking;
internal static class AutomaticCategories
{
    sealed record CategoryRule(string Category,Regex[] Generic,Regex[] Merchants);
    static readonly CategoryRule[] rules=Load();
    static CategoryRule[] Load(){
        using var stream=typeof(AutomaticCategories).Assembly.GetManifestResourceStream("SimpleBanking.Categories.json")!;
        var categories=JsonNode.Parse(stream)!["categories"]!;
        return new[]{("versicherungen","Versicherungen"),("wohnen_kredit","Wohnen & Kredit"),("mobilitaet","Mobilität"),("abos_digital","Abos & Digital"),("shopping","Shopping"),("essen_alltag","Essen & Alltag")}.Select(pair=>{
            Regex[] Patterns(string type,bool whole)=>(categories[pair.Item1]?["keywords"]?[type]?.AsArray()??[]).Select(n=>AssignmentRules.Normalize(n!.GetValue<string>())).Where(s=>s.Length>0).Select(s=>new Regex(@"(?<!\p{L})"+Regex.Escape(s)+(whole?@"(?!\p{L})":""),RegexOptions.CultureInvariant,TimeSpan.FromMilliseconds(100))).ToArray();
            return new CategoryRule(pair.Item2,Patterns("generic",false),Patterns("merchants",true));
        }).ToArray();
    }
    internal static string Classify(Entry entry){
        if(entry.Amount>0)return "Einkommen";
        string text=AssignmentRules.Normalize(string.Join(' ',entry.Merchant,entry.BankCounterpartyName,entry.Purpose,entry.AdditionalInformation));
        return rules.FirstOrDefault(r=>r.Generic.Any(p=>p.IsMatch(text))||r.Merchants.Any(p=>p.IsMatch(text)))?.Category??"Sonstiges";
    }
}
