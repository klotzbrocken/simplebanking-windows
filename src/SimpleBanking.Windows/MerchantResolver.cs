using System.Text.RegularExpressions;
namespace SimpleBanking;
internal sealed record MerchantResolution(string Name,string Source,double Confidence);
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
    internal static string Resolve(Entry entry,Account? account=null,IEnumerable<Account>? ownAccounts=null)=>ResolveDetailed(entry,account,ownAccounts).Name;
    internal static MerchantResolution ResolveDetailed(Entry entry,Account? account=null,IEnumerable<Account>? ownAccounts=null){
        string raw=entry.OriginalMerchant??entry.Merchant;string party=entry.BankCounterpartyName??raw;string lower=party.ToLowerInvariant();var texts=new[]{entry.Purpose,entry.AdditionalInformation};
        string Canonical(string? value)=>RecurringCosts.CanonicalMerchant(Clean(value)??"Unbekannt");
        if(entry.Amount<0&&(lower.Contains("ga nr")||lower.StartsWith("f0")||texts.Any(t=>t.Contains("geldautomat",StringComparison.OrdinalIgnoreCase)||t.Contains("bargeldausz",StringComparison.OrdinalIgnoreCase))||entry.Amount<0&&entry.AdditionalInformation.Contains("barauszahl",StringComparison.OrdinalIgnoreCase)))return new("Bargeldabhebung","cash_pattern",.98);
        if(entry.Amount<0){foreach(string provider in new[]{"paypal","klarna"})if(lower.Contains(provider)){foreach(string text in texts)if(Extract(text,provider) is {} name)return new(Canonical(name),provider+"_purpose",.95);return new(provider=="paypal"?"PayPal (Intermediaer)":"Klarna (Intermediaer)",provider+"_intermediary",.45);}
            if(processors.Any(lower.Contains)){foreach(string text in texts)if(Extract(text,"card") is {} name)return new(Canonical(name),"card_processor_purpose",.75);return new("Kartenzahlung (Intermediaer)","card_processor_intermediary",.35);}}
        foreach(string text in texts)if(Capture(text,@"ABWA\+[A-Z0-9]+\.([^/\n]{2,80})/") is {} abwa)return new(Canonical(abwa),"abwa_format",.88);
        bool own=OwnPartyRegistry.Contains(ownAccounts??(account==null?[]:new[]{account}),party,entry.CounterpartyIban);
        if(!own&&Clean(party) is {} counterparty&&counterparty!="Unbekannt")return new(Canonical(counterparty),entry.Amount<0?"empfaenger":"absender",entry.Amount<0?.9:.75);
        foreach(string text in texts){var card=Capture(text,@"\d{1,2}:\d{2}\s*(.+?)\\")??Capture(text,@"\d{1,2}:\d{2}\s*(.{3,60})$");if(card!=null)return new(Canonical(StripBranch(card)),"kartenzahlung",.8);var generic=Capture(text,@"(?:purchase\s+at|einkauf\s+bei|zahlung\s+an)\s+(.+)$");if(generic!=null)return new(Canonical(generic),"purpose_inferred",.6);}
        return new("Unbekannt","unknown",.1);
    }
    internal static string StripBranch(string name){while(Regex.IsMatch(name,@"\s+\d{2,}$")){string next=Regex.Replace(name,@"\s+\d{2,}$","");if(!next.Any(char.IsLetter))break;name=next;}return name;}
}
