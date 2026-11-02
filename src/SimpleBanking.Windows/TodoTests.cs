using System.Net;
using System.Text;
using System.Text.Json.Nodes;
namespace SimpleBanking;

internal static class TodoTests
{
    static void Check(bool value){if(!value)throw new InvalidOperationException("Microsoft To Do regression.");}
    sealed class Handler(Func<HttpRequestMessage,JsonNode?,HttpResponseMessage> response):HttpMessageHandler{
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken cancellation)=>response(request,request.Content==null?null:JsonNode.Parse(await request.Content.ReadAsStringAsync(cancellation)));
    }
    static HttpResponseMessage Json(JsonNode? value,HttpStatusCode code=HttpStatusCode.OK)=>new(code){Content=new StringContent(value?.ToJsonString()??"",Encoding.UTF8,"application/json")};
    static BankData Fixture(){var data=new BankData{ToDo=new(){Id="connection",Enabled=true,ListId="list",AccountId="fixture",ClientId=Guid.NewGuid().ToString()}};data.Accounts.Add(new("account","Fixture","DE89370400440532013000","EUR",42));data.Entries.Add(new("entry","account",new(2026,10,1),-42,"EUR","Händler","Privater Zweck","Sonstiges",Note:"Private Notiz",ReminderAt:new DateTime(2026,10,20,9,30,0)));return data;}
    static void Run(BankData data,TodoGraph graph,Action? save=null,Action? guard=null)=>TodoSync.Run(data,graph,save??(()=>{}),guard??(()=>{}),CancellationToken.None).GetAwaiter().GetResult();
    internal static void Register(Action<string,Action> test){
        test("Editing an open detail preserves remotely changed reminders unless explicitly edited",()=>{
            var baseline=Fixture().Entries[0];var current=baseline with{ReminderAt=baseline.ReminderAt!.Value.AddDays(1),ReminderDone=true,ToDo=new("connection","key","task")};
            var note=baseline with{Note="Neue Notiz"};var merged=TodoSync.MergeDetail(current,note,baseline);Check(merged.ReminderAt==current.ReminderAt&&merged.ReminderDone&&merged.Note==note.Note&&merged.ToDo==current.ToDo);
            var remove=note with{ReminderAt=null};Check(TodoSync.MergeDetail(current,remove,note).ReminderAt==null);
        });
        test("To Do transmits only permitted fields and preserves local reminder time",()=>{
            var entry=Fixture().Entries[0];var payload=TodoGraph.Payload(entry,new("c","k"),true);string json=payload.ToJsonString();
            Check(!json.Contains("Privater")&&!json.Contains("Private Notiz")&&!json.Contains("IBAN")&&!json.Contains("42"));
            Check(TodoGraph.Reminder(payload)==entry.ReminderAt&&payload["title"]!.GetValue<string>().Contains("Händler"));
            Check(TodoGraph.Payload(entry,new("c","k"),false)["title"]==null);
        });
        test("To Do creates updates completes reopens and removes linked reminders",()=>{
            var data=Fixture();JsonNode? remote=null;int creates=0,deletes=0;
            using var handler=new Handler((request,body)=>{Check(request.Headers.Authorization?.Parameter=="fixture-token");if(request.Method==HttpMethod.Post){creates++;remote=body!.DeepClone();remote["id"]="task";return Json(remote);}if(request.Method==HttpMethod.Patch){foreach(var pair in body!.AsObject())remote![pair.Key]=pair.Value?.DeepClone();return Json(remote);}if(request.Method==HttpMethod.Delete){deletes++;remote=null;return Json(null,HttpStatusCode.NoContent);}return remote==null?Json(null,HttpStatusCode.NotFound):Json(remote);});using var graph=new TodoGraph("fixture-token",handler);
            Run(data,graph);Check(creates==1&&data.Entries[0].ToDo?.TaskId=="task");
            data.Entries[0]=data.Entries[0] with{ReminderAt=data.Entries[0].ReminderAt!.Value.AddHours(1)};Run(data,graph);Check(TodoGraph.Reminder(remote!)==data.Entries[0].ReminderAt);
            remote!["status"]="completed";Run(data,graph);Check(data.Entries[0].ReminderDone);
            data.Entries[0]=data.Entries[0] with{ReminderDone=false};Run(data,graph);Check(remote["status"]!.GetValue<string>()=="notStarted");
            data.Entries[0]=data.Entries[0] with{ReminderAt=null};Run(data,graph);Check(deletes==1&&data.Entries[0].ToDo==null&&creates==1);
        });
        test("To Do recovers a lost creation response without a duplicate POST",()=>{
            var data=Fixture();JsonNode? remote=null;int creates=0;
            using var handler=new Handler((request,body)=>{if(request.Method==HttpMethod.Post){creates++;remote=body!.DeepClone();remote["id"]="recovered";throw new HttpRequestException("Synthetic lost response");}if(request.RequestUri!.Query.Length>0)return Json(new JsonObject{["value"]=new JsonArray(remote!.DeepClone())});return Json(remote);});using var graph=new TodoGraph("fixture",handler);
            try{Run(data,graph);throw new Exception("Expected loss");}catch(HttpRequestException){}
            Check(data.Entries[0].ToDo is{CreateAttempted:true,TaskId:null});Run(data,graph);Check(creates==1&&data.Entries[0].ToDo!.TaskId=="recovered");
        });
        test("To Do never retries an uncertain missing creation automatically",()=>{
            var data=Fixture();data.Entries[0]=data.Entries[0] with{ToDo=new("connection","pending",CreateAttempted:true)};int writes=0;
            using var handler=new Handler((request,body)=>{if(request.Method!=HttpMethod.Get)writes++;return Json(new JsonObject{["value"]=new JsonArray()});});using var graph=new TodoGraph("fixture",handler);
            bool rejected=false;try{Run(data,graph);}catch(InvalidOperationException){rejected=true;}Check(rejected&&writes==0);
        });
        test("To Do rejects foreign pagination links before forwarding a token",()=>{
            int requests=0;using var handler=new Handler((request,body)=>{requests++;return Json(new JsonObject{["value"]=new JsonArray(),["@odata.nextLink"]="https://example.org/capture"});});using var graph=new TodoGraph("fixture",handler);
            bool rejected=false;try{graph.Collection(TodoGraph.Lists,CancellationToken.None).GetAwaiter().GetResult();}catch(InvalidDataException){rejected=true;}Check(rejected&&requests==1);
        });
        test("To Do preserves edits made during a request and discards locked results",()=>{
            var data=Fixture();var original=data.Entries[0];data.Entries[0]=original with{ToDo=new("connection","key","task",true,original.ReminderAt,false)};
            using var handler=new Handler((request,body)=>{data.Entries[0]=data.Entries[0] with{ReminderAt=original.ReminderAt!.Value.AddDays(1)};var payload=TodoGraph.Payload(original,new("c","k"),true);payload["status"]="completed";return Json(payload);});using var graph=new TodoGraph("fixture",handler);
            Run(data,graph);Check(!data.Entries[0].ReminderDone&&data.Entries[0].ReminderAt==original.ReminderAt!.Value.AddDays(1));
            bool locked=false;using var lockedHandler=new Handler((request,body)=>{locked=true;return Json(new JsonObject{["status"]="completed"});});using var lockedGraph=new TodoGraph("fixture",lockedHandler);
            bool canceled=false;try{Run(data,lockedGraph,guard:()=>{if(locked)throw new OperationCanceledException();});}catch(OperationCanceledException){canceled=true;}Check(canceled&&!data.Entries[0].ReminderDone);
        });
        test("To Do saves intent before networking and retains linkage across booking reconciliation",()=>{
            var data=Fixture();int requests=0;using var handler=new Handler((request,body)=>{requests++;return Json(new JsonObject());});using var graph=new TodoGraph("fixture",handler);
            try{Run(data,graph,()=>throw new IOException("Synthetic disk failure"));}catch(IOException){}Check(requests==0&&data.Entries[0].ToDo==null);
            var old=data.Entries[0] with{Pending=true,ToDo=new("connection","key","task",true,data.Entries[0].ReminderAt,false)};
            var next=old with{Id="booked",Pending=false,ToDo=null};var reconciled=BookingEnrichment.Reconcile([old],[next]).Single();Check(reconciled.ToDo==old.ToDo);
            data.Entries[0]=old;data.ToDo.TokenCache=Encoding.UTF8.GetBytes("synthetic-token-cache");var bytes=EncryptedStore.Encrypt(data,"test");Check(!Encoding.UTF8.GetString(bytes).Contains("synthetic-token-cache"));var restored=EncryptedStore.Decrypt(bytes,"test");Check(restored.Entries[0].ToDo==old.ToDo&&restored.ToDo.TokenCache!.SequenceEqual(data.ToDo.TokenCache));
        });
    }
}
