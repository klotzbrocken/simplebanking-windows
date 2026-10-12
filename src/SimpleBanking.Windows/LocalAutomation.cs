using System.IO.Pipes;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
namespace SimpleBanking;
internal static class LocalAutomation
{
    internal const string PipeName="simplebanking-windows-readonly-v1";
    internal static readonly string[] ToolNames=["get_accounts","get_transactions","get_spending_summary","get_monthly_overview","get_categories","get_merchants","get_fixed_costs","get_subscriptions"];
    static JsonObject Schema(string name) {
        var properties=new JsonObject{["account_id"]=new JsonObject{["type"]="string"},["currency"]=new JsonObject{["type"]="string"}};
        if(name is not ("get_fixed_costs" or "get_subscriptions")) {
            foreach(var field in new[]{"category","search","date_from","date_to"})properties[field]=new JsonObject{["type"]="string"};
            foreach(var field in new[]{"days","offset","limit"})properties[field]=new JsonObject{["type"]="integer"};
            foreach(var field in new[]{"min_amount","max_amount"})properties[field]=new JsonObject{["type"]="number"};
        }
        return new(){["type"]="object",["properties"]=properties,["additionalProperties"]=false};
    }
    static string Description(string name)=>name switch {
        "get_fixed_costs"=>"Read-only: unpaid fixed costs in each account's current salary cycle, using the same calculation and exclusions as the app. Includes calculation time and last balance retrieval time; no bank refresh.",
        "get_subscriptions"=>"Read-only: subscriptions from stored booked history, using the app detector and user assignments/exclusions. Includes calculation time and last balance retrieval time; no bank refresh.",
        _=>"Read-only banking query: "+name
    };
    internal static object Query(BankData data,string name,JsonObject args,AppPreferences? preferences=null){
        if(!ToolNames.Contains(name))throw new InvalidOperationException("Unbekannter Nur-Lese-Befehl");
        int days=Math.Clamp(args["days"]?.GetValue<int>()??30,1,3650);
        int offset=Math.Max(0,args["offset"]?.GetValue<int>()??0),limit=Math.Clamp(args["limit"]?.GetValue<int>()??5000,1,5000);
        string? account=args["account_id"]?.GetValue<string>();
        var today=DateOnly.FromDateTime(DateTime.Today);
        if(name is "get_fixed_costs" or "get_subscriptions")return AnalysisQuery(data,name,args,preferences??new(),today);
        var from=args["date_from"] is {} start?DateOnly.ParseExact(start.GetValue<string>(),"yyyy-MM-dd",System.Globalization.CultureInfo.InvariantCulture):today.AddDays(1-days);
        var to=args["date_to"] is {} finish?DateOnly.ParseExact(finish.GetValue<string>(),"yyyy-MM-dd",System.Globalization.CultureInfo.InvariantCulture):today;
        if(from>to)throw new InvalidDataException("Ungültiger Zeitraum");
        var rows=data.Entries.Where(e=>(account==null||e.AccountId==account)&&e.Date>=from&&e.Date<=to
            &&(args["currency"]==null||e.Currency==args["currency"]!.GetValue<string>())
            &&(args["category"]==null||e.Category==args["category"]!.GetValue<string>())
            &&(args["search"]==null||(e.Merchant+" "+e.Purpose).Contains(args["search"]!.GetValue<string>(),StringComparison.OrdinalIgnoreCase))
            &&(args["min_amount"]==null||e.Amount>=args["min_amount"]!.GetValue<decimal>())
            &&(args["max_amount"]==null||e.Amount<=args["max_amount"]!.GetValue<decimal>())).ToArray();
        return name switch{
            "get_accounts"=>data.Accounts.Where(a=>account==null||a.Id==account).Select(a=>new{id=a.Id,name=a.Name,iban=a.Iban,currency=a.Currency,balance=a.BalanceKnown?(decimal?)a.Balance:null}).ToArray(),
            "get_transactions"=>rows.OrderByDescending(e=>e.Date).ThenBy(e=>e.AccountId,StringComparer.Ordinal).ThenBy(e=>e.Id,StringComparer.Ordinal).Skip(offset).Take(limit).Select(e=>new{id=e.Id,account_id=e.AccountId,date=e.Date,amount=e.Amount,currency=e.Currency,merchant=e.Merchant,purpose=e.Purpose,category=e.Category,pending=e.Pending}).ToArray(),
            "get_spending_summary"=>rows.Where(e=>!e.Pending&&e.Amount<0).GroupBy(e=>(e.Currency,e.Category)).Select(g=>new{currency=g.Key.Currency,category=g.Key.Category,total=g.Sum(e=>e.Amount),count=g.Count()}).ToArray(),
            "get_monthly_overview"=>rows.Where(e=>!e.Pending).GroupBy(e=>(e.Date.Year,e.Date.Month,e.Currency)).OrderBy(g=>g.Key.Year).ThenBy(g=>g.Key.Month).Select(g=>new{year=g.Key.Year,month=g.Key.Month,currency=g.Key.Currency,income=g.Where(e=>e.Amount>0).Sum(e=>e.Amount),expenses=g.Where(e=>e.Amount<0).Sum(e=>e.Amount)}).ToArray(),
            "get_categories"=>rows.Where(e=>!e.Pending).GroupBy(e=>(e.Currency,e.Category)).OrderBy(g=>g.Key.Currency).ThenBy(g=>g.Key.Category).Select(g=>new{currency=g.Key.Currency,category=g.Key.Category,count=g.Count(),income=g.Where(e=>e.Amount>0).Sum(e=>e.Amount),expenses=g.Where(e=>e.Amount<0).Sum(e=>e.Amount)}).Skip(offset).Take(limit).ToArray(),
            "get_merchants"=>rows.Where(e=>!e.Pending).GroupBy(e=>(e.Currency,e.Merchant)).OrderBy(g=>g.Key.Currency).ThenBy(g=>g.Key.Merchant).Select(g=>new{currency=g.Key.Currency,merchant=g.Key.Merchant,count=g.Count(),income=g.Where(e=>e.Amount>0).Sum(e=>e.Amount),expenses=g.Where(e=>e.Amount<0).Sum(e=>e.Amount),last_date=g.Max(e=>e.Date)}).Skip(offset).Take(limit).ToArray(),
            _=>throw new InvalidOperationException("Unbekannter Nur-Lese-Befehl")
        };
    }
    static object AnalysisQuery(BankData data,string name,JsonObject args,AppPreferences preferences,DateOnly today) {
        if(args.Any(p=>p.Key is not ("account_id" or "currency")))throw new InvalidDataException("Auswertungen erlauben nur account_id und currency; Zeitraum und Korrekturen entsprechen der App.");
        var accounts=data.Accounts.Where(a=>(args["account_id"]==null||a.Id==args["account_id"]!.GetValue<string>())&&(args["currency"]==null||a.Currency==args["currency"]!.GetValue<string>()));
        return accounts.Select(a=>{
            var rows=data.Entries.Where(e=>e.AccountId==a.Id&&e.Currency==a.Currency).ToArray();
            var summary=RecurringSummary.Calculate(rows,a,preferences.FinanceFor(a.Id),today,data.RecurringAssignments,data.Rules);
            object items=name=="get_fixed_costs"
                ? (summary.CycleStart is {} start && summary.CycleEnd is {} end?RecurringCosts.OutstandingPayments(summary.Payments,start,end):Enumerable.Empty<RecurringPayment>()).Select(p=>new{merchant=p.Merchant,group_key=p.GroupKey,amount=p.AverageAmount,frequency=Subscriptions.Frequency(p.Frequency),last_date=p.LastDate,next_date=RecurringCosts.NextExpected(p)}).ToArray()
                : Subscriptions.Detect(rows,a.BankName??a.Name,data.Rules,data.RecurringAssignments).Select(s=>new{merchant=s.Name,group_key=s.Id,amount=s.Average,monthly_amount=s.Monthly,frequency=Subscriptions.Frequency(s.Cadence),last_date=s.LastDate,tab=s.Tab,cancellation_url=s.CancellationUrl}).ToArray();
            return new{account_id=a.Id,account_name=a.Name,currency=a.Currency,calculated_at=DateTimeOffset.UtcNow,bank_data_updated_at=a.BalanceFetchedAt,cycle_start=summary.CycleStart,cycle_end=summary.CycleEnd,outstanding=name=="get_fixed_costs"?summary.Outstanding:null,items};
        }).ToArray();
    }
    internal static async Task<string?> ReadRequest(TextReader reader,CancellationToken cancellation) {
        var result=new StringBuilder();var buffer=new char[1];
        while(await reader.ReadAsync(buffer.AsMemory(),cancellation)>0){
            if(buffer[0]=='\n')return result.ToString().TrimEnd('\r');
            if(result.Length>=65536)throw new InvalidDataException("Anfrage zu groß");
            result.Append(buffer[0]);
        }
        return result.Length==0?null:result.ToString();
    }
    static async Task<JsonNode?> Call(bool demo,string name,JsonObject args){
        if(demo)return JsonSerializer.SerializeToNode(Query(DemoData.Create(),name,args));
        using var timeout=new CancellationTokenSource(TimeSpan.FromSeconds(5));using var pipe=new NamedPipeClientStream(".",PipeName,PipeDirection.InOut,PipeOptions.Asynchronous|PipeOptions.CurrentUserOnly);await pipe.ConnectAsync(timeout.Token);using var writer=new StreamWriter(pipe,new UTF8Encoding(false),leaveOpen:true){AutoFlush=true};using var reader=new StreamReader(pipe,Encoding.UTF8,leaveOpen:true);await writer.WriteLineAsync(new JsonObject{["name"]=name,["arguments"]=args.DeepClone()}.ToJsonString());string? line=await reader.ReadLineAsync(timeout.Token);var response=JsonNode.Parse(line??"null");if(response?["error"] is {} error)throw new InvalidOperationException(error.GetValue<string>());return response?["result"]?.DeepClone();
    }
    internal static int RunCli(string[] args){try{int index=Array.IndexOf(args,"--cli");string name=args.ElementAtOrDefault(index+1)??"get_accounts";name=name switch{"fixed"=>"get_fixed_costs","subs"=>"get_subscriptions",_=>name};var options=args.ElementAtOrDefault(index+2) is {} json&&json.StartsWith('{')?JsonNode.Parse(json)!.AsObject():new JsonObject();Console.WriteLine(Call(args.Contains("--demo"),name,options).GetAwaiter().GetResult()?.ToJsonString());return 0;}catch(Exception ex){Console.Error.WriteLine("Abfrage nicht verfügbar: "+ex.GetType().Name+". App entsperren und CLI/MCP in Einstellungen → Extras einschalten.");return 1;}}
    internal static int RunMcp(bool demo){
        Console.InputEncoding=Encoding.UTF8;Console.OutputEncoding=new UTF8Encoding(false);string? line;bool initialized=false;
        while(true){try{line=ReadRequest(Console.In,CancellationToken.None).GetAwaiter().GetResult();}catch(InvalidDataException){return 1;}if(line==null)break;JsonNode? id=null;try{if(line.Length>65536)throw new InvalidDataException("Anfrage zu groß");var request=JsonNode.Parse(line)!.AsObject();id=request["id"]?.DeepClone();if(id==null)continue;string method=request["method"]!.GetValue<string>();object result;
            if(method=="initialize"){initialized=true;result=new{protocolVersion="2025-06-18",capabilities=new{tools=new{}},serverInfo=new{name="simplebanking-windows",version=typeof(LocalAutomation).Assembly.GetName().Version?.ToString()??"unknown"}};}
            else if(method=="ping")result=new{};
            else if(!initialized)throw new InvalidOperationException("Initialize erforderlich");
            else if(method=="tools/list")result=new{tools=ToolNames.Select(name=>new{name,description=Description(name),inputSchema=Schema(name),annotations=new{readOnlyHint=true,destructiveHint=false,openWorldHint=false}}).ToArray()};
            else if(method=="tools/call"){try{var parameters=request["params"]!;var value=Call(demo,parameters["name"]!.GetValue<string>(),parameters["arguments"]?.AsObject()??new()).GetAwaiter().GetResult();result=new{content=new[]{new{type="text",text=value?.ToJsonString()??"null"}},isError=false};}catch(Exception){result=new{content=new[]{new{type="text",text="Abfrage nicht verfügbar. App entsperren und CLI/MCP einschalten; nur Lesebefehle sind erlaubt."}},isError=true};}}
            else{Console.WriteLine(new JsonObject{["jsonrpc"]="2.0",["id"]=id,["error"]=new JsonObject{["code"]=-32601,["message"]="Method not found"}}.ToJsonString());continue;}
            Console.WriteLine(new JsonObject{["jsonrpc"]="2.0",["id"]=id,["result"]=JsonSerializer.SerializeToNode(result)}.ToJsonString());
        }catch(Exception){Console.WriteLine(new JsonObject{["jsonrpc"]="2.0",["id"]=id,["error"]=new JsonObject{["code"]=-32600,["message"]="Invalid request"}}.ToJsonString());}}
        return 0;
    }
}
internal sealed partial class MainWindow
{
    void InitializeAutomation(){
        var lifetime=new CancellationTokenSource();Disposed+=(_,_)=>lifetime.Cancel();
        _=Task.Run(async()=>{while(!lifetime.IsCancellationRequested){try{using var pipe=new NamedPipeServerStream(LocalAutomation.PipeName,PipeDirection.InOut,1,PipeTransmissionMode.Byte,PipeOptions.Asynchronous|PipeOptions.CurrentUserOnly);await pipe.WaitForConnectionAsync(lifetime.Token);using var timeout=CancellationTokenSource.CreateLinkedTokenSource(lifetime.Token);timeout.CancelAfter(5000);using var reader=new StreamReader(pipe,Encoding.UTF8,leaveOpen:true);using var writer=new StreamWriter(pipe,new UTF8Encoding(false),leaveOpen:true){AutoFlush=true};string? line=await LocalAutomation.ReadRequest(reader,timeout.Token);if(line==null)continue;var request=JsonNode.Parse(line)!.AsObject();var completion=new TaskCompletionSource<JsonObject>(TaskCreationOptions.RunContinuationsAsynchronously);
            BeginInvoke(new Action(()=>{try{if(!Preferences.AutomationEnabled||!Unlocked||BalancesHidden)throw new InvalidOperationException("Gesperrt oder nicht freigegeben");completion.TrySetResult(new JsonObject{["result"]=JsonSerializer.SerializeToNode(LocalAutomation.Query(data,request["name"]!.GetValue<string>(),request["arguments"]?.AsObject()??new(),Preferences))});}catch(Exception){completion.TrySetResult(new JsonObject{["error"]="Zugriff gesperrt oder ungültige Abfrage"});}}));
            var response=await completion.Task.WaitAsync(timeout.Token);await writer.WriteLineAsync(response.ToJsonString());
        }catch(OperationCanceledException){if(lifetime.IsCancellationRequested)break;}catch(Exception){if(lifetime.IsCancellationRequested)break;await Task.Delay(250,lifetime.Token);}}});
    }
}
