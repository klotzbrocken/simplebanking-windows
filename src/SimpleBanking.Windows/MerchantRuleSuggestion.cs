using System.Text.RegularExpressions;
namespace SimpleBanking;
internal sealed record MerchantRuleSuggestion(string Scope,string Pattern)
{
    internal static MerchantRuleSuggestion From(Entry entry){
        var match=Regex.Match(entry.Purpose,@"(?:ihr\s+einkauf\s+bei|purchase\s+at)\s+(.+)$",RegexOptions.IgnoreCase|RegexOptions.CultureInvariant,TimeSpan.FromMilliseconds(100));
        if(match.Success)return new("Verwendungszweck",match.Groups[1].Value.Trim());
        string party=entry.BankCounterpartyName??entry.OriginalMerchant??entry.Merchant;
        if(!string.IsNullOrWhiteSpace(party)&&party!="Unbekannt")return new("Empfänger",party.Trim());
        return new("Verwendungszweck",entry.Purpose.Trim());
    }
}
