using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json.Nodes;
using Microsoft.Identity.Client;
namespace SimpleBanking;

public sealed record TodoLink(string Connection,string Key,string? TaskId=null,bool CreateAttempted=false,DateTime? LastAt=null,bool LastDone=false);
public sealed class TodoConnection
{
    public string ClientId {get;set;}="";
    public string AccountId {get;set;}="";
    public string AccountName {get;set;}="";
    public string ListId {get;set;}="";
    public string ListName {get;set;}="";
    public string Id {get;set;}=Guid.NewGuid().ToString("N");
    public byte[]? TokenCache {get;set;}
    public bool Enabled {get;set;}
    public DateTimeOffset? LastSync {get;set;}
}

internal static class TodoIdentity
{
    internal static string DefaultClientId {
        get => typeof(TodoIdentity).Assembly.GetCustomAttributes(typeof(System.Reflection.AssemblyMetadataAttribute),false).Cast<System.Reflection.AssemblyMetadataAttribute>().FirstOrDefault(a=>a.Key=="MicrosoftTodoClientId")?.Value??"";
    }
    internal static async Task<(string Token,TodoConnection Connection)> Authorize(TodoConnection state,bool interactive,CancellationToken token){
        if(!Guid.TryParse(state.ClientId,out _))throw new InvalidOperationException("Die Microsoft-App-Registrierung ist noch nicht eingerichtet.");
        var app=PublicClientApplicationBuilder.Create(state.ClientId).WithAuthority("https://login.microsoftonline.com/common").WithRedirectUri("http://localhost").Build();
        byte[]? cache=state.TokenCache;
        app.UserTokenCache.SetBeforeAccess(args=>{if(cache is {Length:>0})args.TokenCache.DeserializeMsalV3(cache);});
        app.UserTokenCache.SetAfterAccess(args=>{if(args.HasStateChanged)cache=args.TokenCache.SerializeMsalV3();});
        string[] scopes=["https://graph.microsoft.com/Tasks.ReadWrite"];
        AuthenticationResult result;
        if(interactive)result=await app.AcquireTokenInteractive(scopes).WithUseEmbeddedWebView(false).WithPrompt(Prompt.SelectAccount).ExecuteAsync(token);
        else {var account=(await app.GetAccountsAsync()).SingleOrDefault(a=>a.HomeAccountId.Identifier==state.AccountId);if(account==null)throw new InvalidOperationException("Bitte Microsoft To Do erneut verbinden.");result=await app.AcquireTokenSilent(scopes,account).ExecuteAsync(token);}
        if(state.AccountId.Length>0&&result.Account.HomeAccountId.Identifier!=state.AccountId)throw new InvalidOperationException("Bitte dasselbe Microsoft-Konto wählen oder zuerst die bestehende Verbindung trennen.");
        return (result.AccessToken,new TodoConnection{ClientId=state.ClientId,AccountId=result.Account.HomeAccountId.Identifier,AccountName=result.Account.Username,ListId=state.ListId,ListName=state.ListName,Id=state.Id,Enabled=state.Enabled,LastSync=state.LastSync,TokenCache=cache});
    }
}

internal sealed class TodoGraph:IDisposable
{
    readonly HttpClient client;
    internal TodoGraph(string token,HttpMessageHandler? handler=null){client=handler==null?new(new HttpClientHandler{AllowAutoRedirect=false}):new(handler,false);client.Timeout=TimeSpan.FromSeconds(30);client.DefaultRequestHeaders.Authorization=new AuthenticationHeaderValue("Bearer",token);}
    internal static string Lists="https://graph.microsoft.com/v1.0/me/todo/lists";
    internal static string Tasks(string list)=>Lists+"/"+Uri.EscapeDataString(list)+"/tasks";
    internal async Task<JsonNode?> Send(HttpMethod method,string url,JsonNode? body,CancellationToken token,bool missingOk=false){
        if(!Uri.TryCreate(url,UriKind.Absolute,out var uri)||uri.Scheme!="https"||uri.Host!="graph.microsoft.com"||uri.Port!=443||uri.UserInfo.Length>0||!uri.AbsolutePath.StartsWith("/v1.0/me/todo/lists",StringComparison.Ordinal))throw new InvalidDataException("Ungültige Microsoft-To-Do-Adresse.");
        using var request=new HttpRequestMessage(method,uri);if(body!=null)request.Content=new StringContent(body.ToJsonString(),Encoding.UTF8,"application/json");
        using var response=await client.SendAsync(request,token);
        if(missingOk&&response.StatusCode==HttpStatusCode.NotFound)return null;
        if(!response.IsSuccessStatusCode)throw new InvalidOperationException(response.StatusCode switch{HttpStatusCode.Unauthorized=>"Microsoft-Anmeldung abgelaufen. Bitte erneut verbinden.",HttpStatusCode.Forbidden=>"Microsoft hat den Zugriff abgelehnt. Bitte die To-Do-Berechtigung prüfen.",(HttpStatusCode)429=>"Microsoft To Do begrenzt die Anfragen. Der Abgleich wird später wiederholt.",_=>"Microsoft To Do ist momentan nicht erreichbar. Lokale Erinnerungen bleiben gespeichert."});
        if(response.StatusCode==HttpStatusCode.NoContent)return null;
        var text=await response.Content.ReadAsStringAsync(token);if(text.Length>4*1024*1024)throw new InvalidDataException("Microsoft-Antwort zu groß.");return text.Length==0?null:JsonNode.Parse(text);
    }
    internal async Task<List<JsonNode>> Collection(string url,CancellationToken token){
        var output=new List<JsonNode>();for(int page=0;page<100;page++){
            var json=await Send(HttpMethod.Get,url,null,token);output.AddRange(json?["value"]?.AsArray().Where(n=>n!=null).Select(n=>n!)??[]);
            if(json?["@odata.nextLink"]?.GetValue<string>() is not {Length:>0} next)return output;url=next;
        }throw new InvalidDataException("Die Microsoft-Liste ist zu groß für einen vollständigen Abgleich.");
    }
    internal static string Marker(string key)=>"simplebanking · "+key;
    internal static JsonObject Payload(Entry entry,TodoLink link,bool creating){
        JsonNode? date=entry.ReminderAt is {} at?new JsonObject{["dateTime"]=DateTime.SpecifyKind(at,DateTimeKind.Local).ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ss",CultureInfo.InvariantCulture),["timeZone"]="UTC"}:null;
        var result=new JsonObject{["status"]=entry.ReminderDone?"completed":"notStarted",["isReminderOn"]=entry.ReminderAt!=null&&!entry.ReminderDone,["reminderDateTime"]=date,["dueDateTime"]=date?.DeepClone()};
        if(creating){result["title"]="Buchung prüfen: "+entry.Merchant[..Math.Min(entry.Merchant.Length,180)];result["body"]=new JsonObject{["contentType"]="text",["content"]=Marker(link.Key)};}
        return result;
    }
    internal static DateTime? Reminder(JsonNode task){
        if(task["isReminderOn"]?.GetValue<bool>()==false&&task["status"]?.GetValue<string>()!="completed")return null;
        var value=task["reminderDateTime"];if(value?["dateTime"]?.GetValue<string>() is not {} text)return null;
        var date=DateTime.Parse(text,CultureInfo.InvariantCulture,DateTimeStyles.RoundtripKind);
        var zone=value["timeZone"]?.GetValue<string>()??"UTC";
        return TimeZoneInfo.ConvertTimeFromUtc(TimeZoneInfo.ConvertTimeToUtc(DateTime.SpecifyKind(date,DateTimeKind.Unspecified),TimeZoneInfo.FindSystemTimeZoneById(zone)),TimeZoneInfo.Local);
    }
    public void Dispose()=>client.Dispose();
}

internal static class TodoSync
{
    internal static Entry MergeDetail(Entry current,Entry changed,Entry baseline)=>changed with{ToDo=current.ToDo,ReminderAt=changed.ReminderAt==baseline.ReminderAt&&changed.ReminderDone==baseline.ReminderDone?current.ReminderAt:changed.ReminderAt,ReminderDone=changed.ReminderAt==baseline.ReminderAt&&changed.ReminderDone==baseline.ReminderDone?current.ReminderDone:changed.ReminderDone};
    internal static async Task Run(BankData data,TodoGraph graph,Action save,Action guard,CancellationToken token){
        var connection=data.ToDo;if(!connection.Enabled||connection.ListId.Length==0)return;
        string tasks=TodoGraph.Tasks(connection.ListId);int unresolved=0;
        foreach(var original in data.Entries.Where(e=>e.ReminderAt!=null||e.ToDo?.Connection==connection.Id).ToArray()){
            token.ThrowIfCancellationRequested();guard();
            Entry? Current()=>data.Entries.SingleOrDefault(e=>e.AccountId==original.AccountId&&e.Id==original.Id);
            void Commit(Func<Entry,Entry> change){guard();var current=Current();if(current==null)return;var changed=change(current);if(changed==current)return;int index=data.Entries.IndexOf(current);data.Entries[index]=changed;try{save();}catch{data.Entries[index]=current;throw;}}
            var entry=Current();if(entry==null)continue;
            var link=entry.ToDo?.Connection==connection.Id?entry.ToDo:null;
            if(link==null){if(entry.ReminderAt==null)continue;link=new(connection.Id,Guid.NewGuid().ToString("N"));Commit(e=>e with{ToDo=link});}
            if(link.TaskId==null){
                if(link.CreateAttempted){
                    var candidates=(await graph.Collection(tasks+"?$top=100",token)).Where(t=>t["body"]?["content"]?.GetValue<string>()?.Contains(TodoGraph.Marker(link.Key),StringComparison.Ordinal)==true).ToArray();guard();
                    if(candidates.Length!=1){unresolved++;continue;}
                    link=link with{TaskId=candidates[0]["id"]!.GetValue<string>()};Commit(e=>e with{ToDo=link});
                }else{
                    if(entry.ReminderAt==null){Commit(e=>e with{ToDo=null});continue;}
                    link=link with{CreateAttempted=true,LastAt=entry.ReminderAt,LastDone=entry.ReminderDone};Commit(e=>e with{ToDo=link});
                    var created=await graph.Send(HttpMethod.Post,tasks,TodoGraph.Payload(entry,link,true),token);guard();
                    link=link with{TaskId=created?["id"]?.GetValue<string>()??throw new InvalidDataException("Microsoft hat keine Aufgaben-ID zurückgegeben.")};Commit(e=>e with{ToDo=link});continue;
                }
            }
            string url=tasks+"/"+Uri.EscapeDataString(link.TaskId!);
            if(entry.ReminderAt==null&&link.LastAt!=null){await graph.Send(HttpMethod.Delete,url,null,token,true);Commit(e=>e with{ToDo=null});continue;}
            if(entry.ReminderAt!=link.LastAt||entry.ReminderDone!=link.LastDone){
                var existing=await graph.Send(HttpMethod.Get,url,null,token,true);guard();if(existing==null){Commit(e=>e with{ToDo=null});continue;}
                await graph.Send(HttpMethod.Patch,url,TodoGraph.Payload(entry,link,false),token);
                link=link with{LastAt=entry.ReminderAt,LastDone=entry.ReminderDone};Commit(e=>e with{ToDo=link});
            }else{
                var remote=await graph.Send(HttpMethod.Get,url,null,token,true);guard();
                DateTime? at=remote==null?null:TodoGraph.Reminder(remote);bool done=remote?["status"]?.GetValue<string>()=="completed";
                var next=remote==null?null:link with{LastAt=at,LastDone=done};
                Commit(e=>e.ReminderAt==entry.ReminderAt&&e.ReminderDone==entry.ReminderDone?e with{ReminderAt=at,ReminderDone=done,ToDo=next}:remote==null?e with{ToDo=null}:e);
            }
        }
        if(unresolved>0)throw new InvalidOperationException(unresolved+" To-Do-Erstellung(en) mit unklarem Ausgang. Bitte unter Verbindung prüfen nachsehen; es wird keine zweite Aufgabe erzeugt. Andere Erinnerungen wurden abgeglichen.");
    }
}
