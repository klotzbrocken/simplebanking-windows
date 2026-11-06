using System.IO.Pipes;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
namespace SimpleBanking;
internal static class LocalAutomation
{
    internal static string? ClientToken(string? windows,string? mac) {
        if(!string.IsNullOrEmpty(windows)&&!string.IsNullOrEmpty(mac)&&windows!=mac)throw new InvalidOperationException("SIMPLEBANKING_TOKEN und SIMPLEBANKING_MCP_TOKEN widersprechen sich.");
        return string.IsNullOrEmpty(windows)?mac:windows;
    }
    internal static string ClientConfiguration(string executable,string token)=>JsonSerializer.Serialize(new{mcpServers=new{simplebanking=new{command=executable,args=new[]{"--mcp"},env=new Dictionary<string,string>{{"SIMPLEBANKING_TOKEN",token}}}}},new JsonSerializerOptions{WriteIndented=true});
    internal const string PipeName="simplebanking-windows-readonly-v1";
    internal static readonly string[] ToolNames=["get_accounts","get_transactions","get_spending_summary","get_monthly_overview","get_categories","get_merchants","get_fixed_costs","get_subscriptions"];
    static JsonObject Schema(string name) {
        if(name=="prepare_transfer")return new(){["type"]="object",["properties"]=new JsonObject{["account_id"]=new JsonObject{["type"]="string"},["recipient"]=new JsonObject{["type"]="string"},["iban"]=new JsonObject{["type"]="string"},["amount"]=new JsonObject{["type"]="number"},["purpose"]=new JsonObject{["type"]="string"},["date"]=new JsonObject{["type"]="string"},["request_id"]=new JsonObject{["type"]="string"}},["required"]=new JsonArray("account_id","recipient","iban","amount","purpose","request_id"),["additionalProperties"]=false};
        var properties=new JsonObject{["account_id"]=new JsonObject{["type"]="string"},["currency"]=new JsonObject{["type"]="string"}};
        if(name is not ("get_fixed_costs" or "get_subscriptions")) {
            foreach(var field in new[]{"category","search","date_from","date_to"})properties[field]=new JsonObject{["type"]="string"};
            foreach(var field in new[]{"days","offset","limit"})properties[field]=new JsonObject{["type"]="integer"};
            foreach(var field in new[]{"min_amount","max_amount"})properties[field]=new JsonObject{["type"]="number"};
        }
        return new(){["type"]="object",["properties"]=properties,["additionalProperties"]=false};
    }
    static string Description(string name)=>name switch {
        "prepare_transfer"=>"Create a pending EUR transfer proposal. Requires drafts permission and an allowed source account. User must approve the draft inside the app, then separately review/send it via simplesend. Never executes a payment. Reuse request_id only for the identical proposal.",
        "get_fixed_costs"=>"Read-only: unpaid fixed costs in each account's current salary cycle, using the same calculation and exclusions as the app. Includes calculation time and last balance retrieval time; no bank refresh.",
        "get_subscriptions"=>"Read-only: subscriptions from stored booked history, using the app detector and user assignments/exclusions. Includes calculation time and last balance retrieval time; no bank refresh.",
        _=>"Read-only banking query: "+name
    };
    internal static object ToolDefinitions(IEnumerable<string> names)=>new{tools=names.Select(name=>new{name,description=Description(name),inputSchema=Schema(name),annotations=new{readOnlyHint=name!="prepare_transfer",destructiveHint=false,openWorldHint=false,idempotentHint=true}}).ToArray()};
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
        var rows=data.Entries.Where(e=>(account==null||e.AccountId==account)&&e.Date>=from&&e.Date<=to&&(name=="get_transactions"||e.Date<=today)
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
        if(demo){if(name=="list_tools")return JsonSerializer.SerializeToNode(ToolDefinitions(ToolNames));return JsonSerializer.SerializeToNode(Query(DemoData.Create(),name,args));}
        using var timeout=new CancellationTokenSource(TimeSpan.FromSeconds(5));using var pipe=new NamedPipeClientStream(".",PipeName,PipeDirection.InOut,PipeOptions.Asynchronous|PipeOptions.CurrentUserOnly);await pipe.ConnectAsync(timeout.Token);using var writer=new StreamWriter(pipe,new UTF8Encoding(false),leaveOpen:true){AutoFlush=true};using var reader=new StreamReader(pipe,Encoding.UTF8,leaveOpen:true);await writer.WriteLineAsync(new JsonObject{["name"]=name,["arguments"]=args.DeepClone(),["token"]=ClientToken(Environment.GetEnvironmentVariable("SIMPLEBANKING_TOKEN"),Environment.GetEnvironmentVariable("SIMPLEBANKING_MCP_TOKEN"))}.ToJsonString());string? line=await reader.ReadLineAsync(timeout.Token);var response=JsonNode.Parse(line??"null");if(response?["error"] is {} error)throw new InvalidOperationException(error.GetValue<string>());return response?["result"]?.DeepClone();
    }
    internal static int RunCli(string[] args){try{int index=Array.IndexOf(args,"--cli");string name=args.ElementAtOrDefault(index+1)??"get_accounts";name=name switch{"fixed"=>"get_fixed_costs","subs"=>"get_subscriptions",_=>name};var options=args.ElementAtOrDefault(index+2) is {} json&&json.StartsWith('{')?JsonNode.Parse(json)!.AsObject():new JsonObject();Console.WriteLine(Call(args.Contains("--demo"),name,options).GetAwaiter().GetResult()?.ToJsonString());return 0;}catch(Exception ex){Console.Error.WriteLine("Abfrage nicht verfügbar: "+ex.GetType().Name+". App entsperren, CLI/MCP einschalten und SIMPLEBANKING_TOKEN für einen freigegebenen Client setzen.");return 1;}}
    internal static int RunMcp(bool demo){
        Console.InputEncoding=Encoding.UTF8;Console.OutputEncoding=new UTF8Encoding(false);
        return RunMcp(Console.In,Console.Out,(name,args)=>Call(demo,name,args).GetAwaiter().GetResult());
    }
    internal static int RunMcp(TextReader input,TextWriter output,Func<string,JsonObject,JsonNode?> call){
        bool initialized=false;
        void Error(JsonNode? id,int code,string message)=>output.WriteLine(new JsonObject{["jsonrpc"]="2.0",["id"]=id?.DeepClone(),["error"]=new JsonObject{["code"]=code,["message"]=message}}.ToJsonString());
        while(true){
            string? line;
            try{line=ReadRequest(input,CancellationToken.None).GetAwaiter().GetResult();}catch(InvalidDataException){return 1;}
            if(line==null)return 0;
            JsonNode? id=null;JsonObject request;
            try{request=JsonNode.Parse(line) as JsonObject??throw new InvalidDataException();}
            catch(JsonException){Error(null,-32700,"Parse error");continue;}
            catch(InvalidDataException){Error(null,-32600,"Invalid request");continue;}
            try{
                id=request["id"]?.DeepClone();
                if(request["jsonrpc"]?.GetValue<string>()!="2.0"||request["method"] is not JsonValue methodNode||!methodNode.TryGetValue<string>(out var method)||
                    (id!=null&&(id is not JsonValue identifier||(!identifier.TryGetValue<string>(out _)&&!identifier.TryGetValue<long>(out _))))) {
                    Error(null,-32600,"Invalid request");continue;
                }
                // Notifications never receive a response, including unknown notifications.
                if(id==null)continue;
                object result;
                if(method=="initialize"){
                    if(initialized){Error(id,-32600,"Already initialized");continue;}
                    if(request["params"] is not JsonObject init||init["protocolVersion"] is not JsonValue version||!version.TryGetValue<string>(out var requested)||
                        init["capabilities"] is not JsonObject||init["clientInfo"] is not JsonObject){Error(id,-32602,"Invalid initialization parameters");continue;}
                    initialized=true;
                    string protocol=requested is "2024-11-05" or "2025-03-26" or "2025-06-18"?requested:"2025-06-18";
                    result=new{protocolVersion=protocol,capabilities=new{tools=new{}},serverInfo=new{name="simplebanking-windows",version=typeof(LocalAutomation).Assembly.GetName().Version?.ToString()??"unknown"}};
                }
                else if(method=="ping")result=new{};
                else if(!initialized){Error(id,-32002,"Initialize required");continue;}
                else if(method=="tools/list"){
                    try{result=call("list_tools",new())??throw new InvalidOperationException();}
                    catch{Error(id,-32000,"App entsperren, CLI/MCP aktivieren und Client-Token/Berechtigungen prüfen.");continue;}
                }
                else if(method=="tools/call"){
                    if(request["params"] is not JsonObject parameters||parameters["name"] is not JsonValue tool||!tool.TryGetValue<string>(out var name)||
                        (parameters["arguments"]!=null&&parameters["arguments"] is not JsonObject)){Error(id,-32602,"Invalid tool parameters");continue;}
                    try{var value=call(name,parameters["arguments"]?.AsObject()??new());result=new{content=new[]{new{type="text",text=value?.ToJsonString()??"null"}},isError=false};}
                    catch{result=new{content=new[]{new{type="text",text="Abfrage nicht verfügbar. App entsperren, CLI/MCP aktivieren und Client-Token/Berechtigungen prüfen."}},isError=true};}
                }
                else{Error(id,-32601,"Method not found");continue;}
                output.WriteLine(new JsonObject{["jsonrpc"]="2.0",["id"]=id,["result"]=JsonSerializer.SerializeToNode(result)}.ToJsonString());output.Flush();
            }catch(Exception){Error(id,-32600,"Invalid request");}
        }
    }
}
internal sealed partial class MainWindow
{
    internal object DispatchAutomation(JsonObject request){if(!Preferences.AutomationEnabled||!Unlocked||BalancesHidden)throw new InvalidOperationException("Gesperrt oder nicht freigegeben");var client=AutomationAccess.Authenticate(data.AutomationClients,request["token"]?.GetValue<string>());string name=request["name"]!.GetValue<string>();var args=request["arguments"]?.AsObject()??new();object result;if(name=="list_tools")result=LocalAutomation.ToolDefinitions(AutomationAccess.Tools(client));else if(name=="prepare_transfer"){var draft=AutomationAccess.Prepare(data,client,args);if(!data.AutomationDrafts.Any(d=>d.Id==draft.Id)){data.AutomationDrafts.Add(draft);try{Save();}catch{data.AutomationDrafts.Remove(draft);throw;}status.Text="Neuer MCP-Entwurf von "+client.Name+" · Einstellungen → Extras → Entwürfe prüfen";}result=new{id=draft.Id,state=draft.State,payment_executed=false};}else result=LocalAutomation.Query(AutomationAccess.Restrict(data,client,name,args),name,args,Preferences);return result;}
    void InitializeAutomation(){
        var lifetime=new CancellationTokenSource();Disposed+=(_,_)=>lifetime.Cancel();
        _=Task.Run(async()=>{while(!lifetime.IsCancellationRequested){try{using var pipe=new NamedPipeServerStream(LocalAutomation.PipeName,PipeDirection.InOut,1,PipeTransmissionMode.Byte,PipeOptions.Asynchronous|PipeOptions.CurrentUserOnly);await pipe.WaitForConnectionAsync(lifetime.Token);using var timeout=CancellationTokenSource.CreateLinkedTokenSource(lifetime.Token);timeout.CancelAfter(5000);using var reader=new StreamReader(pipe,Encoding.UTF8,leaveOpen:true);using var writer=new StreamWriter(pipe,new UTF8Encoding(false),leaveOpen:true){AutoFlush=true};string? line=await LocalAutomation.ReadRequest(reader,timeout.Token);if(line==null)continue;var request=JsonNode.Parse(line)!.AsObject();var completion=new TaskCompletionSource<JsonObject>(TaskCreationOptions.RunContinuationsAsynchronously);
            BeginInvoke(new Action(()=>{try{if(!Preferences.AutomationEnabled||!Unlocked||BalancesHidden)throw new InvalidOperationException("Gesperrt oder nicht freigegeben");timeout.Token.ThrowIfCancellationRequested();var result=DispatchAutomation(request);completion.TrySetResult(new JsonObject{["result"]=JsonSerializer.SerializeToNode(result)});}catch(Exception){completion.TrySetResult(new JsonObject{["error"]="Zugriff gesperrt oder ungültige Abfrage"});}}));
            var response=await completion.Task.WaitAsync(timeout.Token);await writer.WriteLineAsync(response.ToJsonString());
        }catch(OperationCanceledException){if(lifetime.IsCancellationRequested)break;}catch(Exception){if(lifetime.IsCancellationRequested)break;await Task.Delay(250,lifetime.Token);}}});
    }
}
