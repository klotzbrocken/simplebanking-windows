using System.Text.Json.Nodes;
namespace SimpleBanking;

// Keep the bank's classification separate from user-authored remittance text.
internal static class BankBookingCode
{
    internal static bool StandingOrder(string code)=>Parts(code).Any(p=>p.StartsWith("ISO:")&&p.EndsWith("/STDO")||p is "GVC:52" or "GVC:052" or "GVC:152");
    internal static bool Fee(string code)=>Parts(code).Any(p=>p is "SWIFT:CHG" or "GVC:808" or "GVC:809" or "GVC:810" or "GVC:888");
    static IEnumerable<string> Parts(string code)=>code.ToUpperInvariant().Split(';',StringSplitOptions.RemoveEmptyEntries|StringSplitOptions.TrimEntries);
    internal static string Compact(JsonNode? codes){
        if(codes is JsonValue scalar&&scalar.TryGetValue<string>(out var value))return value;
        if(codes is not JsonArray array)return "";
        var result=new List<string>();
        foreach(var item in array.OfType<JsonObject>()){
            var variant=item.FirstOrDefault(p=>p.Key is "iso" or "national" or "swift" or "bai" or "other");
            var fields=variant.Value as JsonObject??item;
            string Read(string key)=>fields[key] is JsonValue v&&v.TryGetValue<string>(out var s)?s:"";
            string type=variant.Key??Read("type").ToLowerInvariant(),code=variant.Value is JsonValue val&&val.TryGetValue<string>(out var scalarCode)?scalarCode:Read("code");
            if(type=="iso"&&Read("domain").Length>0)result.Add("ISO:"+Read("domain")+"/"+Read("family")+"/"+Read("subFamily"));
            else if(type=="national"&&code.Length>0)result.Add((Read("country").Equals("DE",StringComparison.OrdinalIgnoreCase)?"GVC:":"NAT-"+Read("country").ToUpperInvariant()+":")+code);
            else if(type is "swift" or "bai"&&code.Length>0)result.Add(type.ToUpperInvariant()+":"+code);
            else if(type=="other"&&code.Length>0)result.Add("OTHER"+(Read("issuer").Length>0?"-"+Read("issuer"):"")+":"+code);
        }
        return string.Join(';',result);
    }
}
