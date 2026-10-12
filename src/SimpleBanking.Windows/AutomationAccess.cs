using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;
namespace SimpleBanking;
public sealed record AutomationClient(string Id,string Name,string TokenHash,string[] Scopes,string[] Accounts,DateTimeOffset CreatedAt,DateTimeOffset? ExpiresAt,bool Revoked=false);
public sealed record AutomationDraft(string Id,string ClientId,string ClientName,string RequestId,TransferDraft Draft,DateTimeOffset CreatedAt,string State="Prüfung ausstehend");
internal static class AutomationAccess
{
    internal static readonly string[] Scopes=["accounts","transactions","analysis","drafts"];
    internal static (AutomationClient Client,string Token) Create(string name,string[] scopes,string[] accounts,int days){
        if(string.IsNullOrWhiteSpace(name)||name.Trim().Length>80||scopes.Length==0||scopes.Any(s=>!Scopes.Contains(s))||days is <1 or >365)throw new InvalidDataException("Clientname, Berechtigungen oder Gültigkeit ungültig.");
        string token=Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
        return(new(Guid.NewGuid().ToString("N"),name.Trim(),Hash(token),scopes.Distinct().ToArray(),accounts.Distinct().ToArray(),DateTimeOffset.UtcNow,DateTimeOffset.UtcNow.AddDays(days)),token);
    }
    static string Hash(string token)=>Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
    internal static AutomationClient Authenticate(IEnumerable<AutomationClient> clients,string? token){
        if(token==null||token.Length!=64)throw new InvalidOperationException("Client-Token fehlt oder ist ungültig.");
        byte[] hash=Encoding.ASCII.GetBytes(Hash(token));
        return clients.FirstOrDefault(c=>!c.Revoked&&(c.ExpiresAt==null||c.ExpiresAt>DateTimeOffset.UtcNow)&&CryptographicOperations.FixedTimeEquals(Encoding.ASCII.GetBytes(c.TokenHash),hash))??throw new InvalidOperationException("Client-Token ungültig, abgelaufen oder widerrufen.");
    }
    internal static string Scope(string tool)=>tool switch{"get_accounts"=>"accounts","get_transactions"=>"transactions","prepare_transfer"=>"drafts",_ when LocalAutomation.ToolNames.Contains(tool)=>"analysis",_=>throw new InvalidOperationException("Unbekannter Befehl.")};
    internal static string[] Tools(AutomationClient client)=>LocalAutomation.ToolNames.Append("prepare_transfer").Where(t=>client.Scopes.Contains(Scope(t))).ToArray();
    internal static BankData Restrict(BankData data,AutomationClient client,string tool,JsonObject args){
        if(!client.Scopes.Contains(Scope(tool)))throw new InvalidOperationException("Dieser Bereich ist nicht freigegeben.");
        var accounts=data.Accounts.Where(a=>client.Accounts.Contains(a.Id)).ToList();string? requested=args["account_id"]?.GetValue<string>();
        if(requested!=null&&!accounts.Any(a=>a.Id==requested))throw new InvalidOperationException("Konto nicht freigegeben.");
        return new(){Accounts=accounts,Entries=data.Entries.Where(e=>accounts.Any(a=>a.Id==e.AccountId)).ToList(),Rules=data.Rules,RecurringAssignments=data.RecurringAssignments};
    }
    internal static AutomationDraft Prepare(BankData data,AutomationClient client,JsonObject args){
        Restrict(data,client,"prepare_transfer",args);
        if(args.Any(p=>p.Key is not ("account_id" or "recipient" or "iban" or "amount" or "purpose" or "date" or "request_id")))throw new InvalidDataException("Unbekanntes Entwurfsfeld.");
        string request=args["request_id"]?.GetValue<string>()??"";
        if(request.Length is <1 or >100)throw new InvalidDataException("Eine eindeutige request_id mit maximal 100 Zeichen ist erforderlich.");
        var draft=new TransferDraft(args["account_id"]?.GetValue<string>()??"",args["recipient"]?.GetValue<string>()??"",BankingRules.NormalizeIban(args["iban"]?.GetValue<string>()??""),args["amount"]?.GetValue<decimal>()??0,args["purpose"]?.GetValue<string>()??"",args["date"] is {} date?DateOnly.ParseExact(date.GetValue<string>(),"yyyy-MM-dd",System.Globalization.CultureInfo.InvariantCulture):null);
        string? error=BankingRules.Validate(draft);if(error!=null)throw new InvalidDataException(error);
        if(!data.Accounts.Any(a=>a.Id==draft.AccountId&&a.Currency=="EUR"))throw new InvalidDataException("Ein EUR-Quellkonto ist erforderlich.");
        var existing=data.AutomationDrafts.FirstOrDefault(d=>d.ClientId==client.Id&&d.RequestId==request);if(existing!=null){if(existing.Draft!=draft)throw new InvalidDataException("request_id wurde bereits für einen anderen Entwurf verwendet.");return existing;}
        if(data.AutomationDrafts.Count(d=>d.State=="Prüfung ausstehend")>=100)throw new InvalidDataException("Bitte zunächst die ausstehenden Entwürfe prüfen.");
        return new(Guid.NewGuid().ToString("N"),client.Id,client.Name,request,draft,DateTimeOffset.UtcNow);
    }
}
