using System.Text.RegularExpressions;
namespace SimpleBanking;
internal static class MerchantResolver
{
    static readonly string[] processors=["landesbank hessen-thuringen","landesbank hessen-thueringen","adyen","nexi","wirecard","payone","sumup","fiserv","worldline"];
    static readonly Dictionary<string,string> legal=new(){["AG"]="AG",["GMBH"]="GmbH",["SE"]="SE",["KG"]="KG",["UG"]="UG",["OHG"]="OHG",["NV"]="N.V.",["AB"]="AB",["SARL"]="S.A.R.L.",["SCA"]="S.C.A.",["SA"]="S.A."};
    static string? Capture(string text,string pattern){var match=Regex.Match(text,pattern,RegexOptions.IgnoreCase|RegexOptions.CultureInvariant,TimeSpan.FromMilliseconds(100));return match.Success?Clean(match.Groups[1].Value):null;}
    internal static string? Clean(string? value){
        if(string.IsNullOrWhiteSpace(value))return null;string result=Regex.Replace(value,@"\s+"," ").Trim(" \t\r\n,.;:!?/\\|-_()[]{}".ToCharArray());string original=result;
        foreach(string pattern in new[]{@"^crv\*\s*",@"^pp\.\d+\.pp/\.\s*",@"^paypal\*\s*",@"^klarna\*\s*"})result=Regex.Replace(result,pattern,"",RegexOptions.IgnoreCase);
        var purchase=Regex.Match(result,@"(?:ihr\s+einkauf\s+bei|purchase\s+at)\s+(.+)$",RegexOptions.IgnoreCase);if(purchase.Success)result=purchase.Groups[1].Value;
        foreach(var marker in new[]{" zahl.system"," debitmastercard"," debitk."," pp."," /pp"}){int index=result.IndexOf(marker,StringComparison.OrdinalIgnoreCase);if(index>=0){result=result[..index].Trim();break;}}
        if(original.Contains("crv*",StringComparison.OrdinalIgnoreCase))result=Regex.Replace(result,@"\s+\d{4,}.*$","");
        if(result.Replace(".","").Trim().Length==0)return null;
        return string.Join(" ",result.Split(' ',StringSplitOptions.RemoveEmptyEntries).Select(word=>legal.GetValueOrDefault(word.ToUpperInvariant().Replace(".",""))??(word.Any(char.IsDigit)||word!=word.ToUpperInvariant()&&word!=word.ToLowerInvariant()?word:char.ToUpperInvariant(word[0])+word[1..].ToLowerInvariant())));
    }
    static string? Extract(string text,string kind){
        if(kind=="card"&&(text.Contains("zahl.system",StringComparison.OrdinalIgnoreCase)||text.Contains("debitmastercard",StringComparison.OrdinalIgnoreCase)))return null;
        foreach(var pattern in new[]{@"ihr\s+einkauf\s+bei\s+(.+)$",@"purchase\s+at\s+(.+)$"})if(Capture(text,pattern) is {} purchase)return purchase;
        if(kind=="paypal"&&Capture(text,@"/\.\s*([^,;/]{2,120})\s*,\s*ihr\s+einkauf\s+bei") is {} preceding)return preceding;
        return kind=="card"?null:Capture(text,Regex.Escape(kind)+@"\s*[\*:\-]?\s*([^,;/]{2,100})");
    }
    internal static string Resolve(Entry entry,Account? account=null,IEnumerable<Account>? ownAccounts=null){
        string raw=entry.OriginalMerchant??entry.Merchant;string party=entry.BankCounterpartyName??raw;string lower=party.ToLowerInvariant();var texts=new[]{entry.Purpose,entry.AdditionalInformation};
        string Canonical(string? value)=>RecurringCosts.CanonicalMerchant(Clean(value)??"Unbekannt");
        if(entry.Amount<0&&(lower.Contains("ga nr")||lower.StartsWith("f0")||texts.Any(t=>t.Contains("geldautomat",StringComparison.OrdinalIgnoreCase)||t.Contains("bargeldausz",StringComparison.OrdinalIgnoreCase))||entry.Amount<0&&entry.AdditionalInformation.Contains("barauszahl",StringComparison.OrdinalIgnoreCase)))return "Bargeldabhebung";
        if(entry.Amount<0){foreach(string provider in new[]{"paypal","klarna"})if(lower.Contains(provider)){foreach(string text in texts)if(Extract(text,provider) is {} name)return Canonical(name);return provider=="paypal"?"PayPal (Intermediaer)":"Klarna (Intermediaer)";}
            if(processors.Any(lower.Contains)){foreach(string text in texts)if(Extract(text,"card") is {} name)return Canonical(name);return "Kartenzahlung (Intermediaer)";}}
        foreach(string text in texts)if(Capture(text,@"ABWA\+[A-Z0-9]+\.([^/\n]{2,80})/") is {} abwa)return Canonical(abwa);
        bool own=OwnPartyRegistry.Contains(ownAccounts??(account==null?[]:new[]{account}),party,entry.CounterpartyIban);
        if(!own&&Clean(party) is {} counterparty&&counterparty!="Unbekannt")return Canonical(counterparty);
        foreach(string text in texts){var card=Capture(text,@"\d{1,2}:\d{2}\s*(.+?)\\")??Capture(text,@"\d{1,2}:\d{2}\s*(.{3,60})$");if(card!=null)return Canonical(StripBranch(card));var generic=Capture(text,@"(?:purchase\s+at|einkauf\s+bei|zahlung\s+an)\s+(.+)$");if(generic!=null)return Canonical(generic);}
        return "Unbekannt";
    }
    internal static string StripBranch(string name){while(Regex.IsMatch(name,@"\s+\d{2,}$")){string next=Regex.Replace(name,@"\s+\d{2,}$","");if(!next.Any(char.IsLetter))break;name=next;}return name;}
}
