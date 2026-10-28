using System.Text.Json;
using System.Text.Json.Nodes;
namespace SimpleBanking;

internal sealed class BankApprovalRequiredException : Exception
{
    internal BankApprovalRequiredException() : base("Neue Bankfreigabe erforderlich. Bitte bewusst aktualisieren.") { }
}

// Consent data live in the encrypted vault. Sessions are short lived, consumed once,
// and never persisted (https://docs.yaxi.tech/sessions.html).
internal sealed class BankReadAccess
{
    internal static void RequireInteractive(bool allowed) { if(!allowed)throw new BankApprovalRequiredException(); }
    readonly Dictionary<(string Connection,string Service),(JsonNode Value,DateTimeOffset At)> sessions=[];
    internal void Clear()=>sessions.Clear();
    internal JsonNode? Take(string connection,string service){
        if(!sessions.Remove((connection,service),out var item)||DateTimeOffset.UtcNow-item.At>TimeSpan.FromMinutes(5))return null;
        return item.Value;
    }
    internal void Remember(string connection,string service,JsonObject result){
        sessions.Remove((connection,service));
        if(result["session"] is JsonArray {Count:>0} session)sessions[(connection,service)]=(session.DeepClone(),DateTimeOffset.UtcNow);
    }
    internal static DateOnly AutomaticFrom(DateOnly today)=>today.AddDays(-89);
    internal static BankConnection UpdatedConsent(BankConnection connection,JsonObject result)=>result["connectionData"] is JsonArray data
        ?connection with{ConnectionData=data.Deserialize<int[]>(),ConnectionDataUpdatedAt=DateTimeOffset.UtcNow}:connection;
    internal async Task<JsonObject> RunAsync(BankConnection connection,string service,JsonObject request,
        Func<Task<string>> issueTicket,Func<JsonObject,Task<JsonObject>> run,CancellationToken cancellation,bool allowInteractive=true){
        if(service is not ("Balances" or "Transactions"))throw new ArgumentException("Read service required.");
        JsonObject Create(bool storedConsent){
            var next=(JsonObject)request.DeepClone();next["useConnectionData"]=storedConsent;
            next["credentials"]=new JsonObject{["connectionId"]=connection.ProviderId,["connectionData"]=JsonSerializer.SerializeToNode(connection.ConnectionData),
                ["userId"]=storedConsent?null:connection.UserId.Length>0?connection.UserId:null,["password"]=storedConsent?null:connection.Password.Length>0?connection.Password:null};
            // Fast read uses no session, leaving a cached interactive session untouched.
            if(!storedConsent)next["session"]=Take(connection.Id,service);
            return next;
        }
        if(connection.ConnectionData is {Length:>0}){
            var fast=Create(true);fast["ticket"]=await issueTicket();cancellation.ThrowIfCancellationRequested();
            try{var result=await run(fast);Remember(connection.Id,service,result);return result;}
            catch(YaxiException e) when(e.ErrorType is "InterruptError" or "Unauthorized" or "InvalidCredentials"){}
        }
        cancellation.ThrowIfCancellationRequested();
        RequireInteractive(allowInteractive);
        var interactive=Create(false);interactive["ticket"]=await issueTicket();cancellation.ThrowIfCancellationRequested();
        var response=await run(interactive);Remember(connection.Id,service,response);return response;
    }
}
