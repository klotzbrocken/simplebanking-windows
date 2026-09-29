using System.Text.Json.Nodes;
namespace SimpleBanking;
internal static class OwnPartyRegistry
{
    // Matches the source's two-word normalization, using only this vault's accounts.
    static string Name(string? value)=>string.Join(" ",(value??"").ToLowerInvariant().Split((char[]?)null,StringSplitOptions.RemoveEmptyEntries).Take(2));
    internal static bool Contains(IEnumerable<Account> accounts,string? name,string? iban){
        string normalized=BankingRules.NormalizeIban(iban??"");string person=Name(name);
        return accounts.Any(a=>(normalized.Length>0&&BankingRules.NormalizeIban(a.Iban)==normalized)||(person.Length>0&&Name(a.OwnerName)==person));
    }
    internal static Account Learn(Account account,JsonArray transactions){
        if(!string.IsNullOrWhiteSpace(account.OwnerName)||string.IsNullOrWhiteSpace(account.Iban))return account;
        var names=new Dictionary<string,string>();
        foreach(var transaction in transactions){
            if(!decimal.TryParse(transaction?["amount"]?["amount"]?.GetValue<string>(),System.Globalization.NumberStyles.Number,System.Globalization.CultureInfo.InvariantCulture,out var amount)||amount==0)continue;
            var own=transaction?[amount<0?"debtor":"creditor"];
            if(BankingRules.NormalizeIban(own?["iban"]?.GetValue<string>()??"")!=BankingRules.NormalizeIban(account.Iban))continue;
            string? name=own?["name"]?.GetValue<string>();if(!string.IsNullOrWhiteSpace(name))names[Name(name)]=name.Trim();
        }
        return names.Count==1?account with{OwnerName=names.Values.Single()}:account;
    }
}
