using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;
namespace SimpleBanking;

internal static class NovemberRegressionTests
{
    static void Check(bool value) {if(!value)throw new Exception("November regression failed");}
    internal static void Register(Action<string,Action> test) {
        test("User initiated balance and history reads can renew expired consent",()=>{
            foreach(var service in new[]{"Balances","Transactions"}){
                int calls=0;var access=new BankReadAccess();var connection=new BankConnection("a","bank","Test","user","password",[1],ApprovalRequired:true);
                var result=access.RunAsync(connection,service,new(),()=>Task.FromResult("fixture"),request=>{
                    calls++;if(calls==1)throw new YaxiException("Unauthorized","expired");
                    Check(request["credentials"]!["userId"]!.GetValue<string>()=="user");return Task.FromResult(new JsonObject{["ok"]=true});
                },CancellationToken.None,true).GetAwaiter().GetResult();Check(calls==2&&result["ok"]!.GetValue<bool>());
            }
        });
        test("Pending promotion across the refresh boundary does not retain a duplicate",()=>{
            var day=new DateOnly(2026,11,3);var old=new Entry("old","a",day.AddDays(1),-20,"EUR","Shop","Test","Shopping",Pending:true,Read:true,CategoryManual:true,EndToEndId:"unique");
            var result=BookingEnrichment.Refresh([old],[],[old with{Id="booked",Pending=false,Date=day,Read=false,Category="Unkategorisiert"}],"a",day.AddDays(-89),day);
            Check(result.Entries.Count==1&&result.Entries[0].Id=="booked"&&result.Entries[0].Read&&result.Entries[0].Category=="Shopping");
        });
        test("Future transactions remain queryable but are excluded from posted spending",()=>{
            var today=DateOnly.FromDateTime(DateTime.Today);var data=new BankData();data.Entries.Add(new("future","a",today.AddDays(8),-20,"EUR","Shop","","Shopping"));
            var args=new JsonObject{["date_from"]=today.ToString("yyyy-MM-dd"),["date_to"]=today.AddDays(10).ToString("yyyy-MM-dd")};
            Check(JsonSerializer.SerializeToNode(LocalAutomation.Query(data,"get_transactions",args))!.AsArray().Count==1);
            Check(JsonSerializer.SerializeToNode(LocalAutomation.Query(data,"get_spending_summary",args))!.AsArray().Count==0);
            Check(MonthlyReport.Rows(data.Entries,today.AddDays(8)).Length==0&&MoneyAgeResult.Calculate(data.Entries).Total==0);
            Check(RoundupMath.Days(data.Entries,1,"",new HashSet<DateOnly>()).Count==0);
        });
        test("Balance selection uses freshness, overdraft exclusion and deterministic type priority",()=>{
            var account=new Account("a","Test","DE89370400440532013000","EUR",0);var now=new DateTimeOffset(2026,11,3,12,0,0,TimeSpan.Zero);
            var rows=JsonNode.Parse("""[{"amount":"10","currency":"EUR","balanceType":"Booked","dateTime":"2026-11-02T23:00:00Z"},{"amount":"20","currency":"EUR","balanceType":"Available","dateTime":"2026-11-03T10:00:00Z"},{"amount":"999","currency":"EUR","balanceType":"Booked","dateTime":"2026-11-11T10:00:00Z"},{"amount":"888","currency":"EUR","balanceType":"Available","dateTime":"2026-11-03T11:00:00Z","creditLimitIncluded":true}]""")!.AsArray();
            JsonObject Payload(IEnumerable<JsonNode?> values)=>new(){["balances"]=new JsonArray(new JsonObject{["account"]=new JsonObject{["iban"]=account.Iban},["balances"]=new JsonArray(values.Select(x=>x?.DeepClone()).ToArray())})};
            Check(BankMapping.Balance(Payload(rows),account,now)==20);Check(BankMapping.Balance(Payload(rows.Reverse()),account,now)==20);
            rows[0]!["dateTime"]="2026-11-03T10:00:00Z";Check(BankMapping.Balance(Payload(rows),account,now)==10);
            Check(BankMapping.Balance(Payload([rows[2]]),account,now)==null);
            rows[0]!.AsObject().Remove("dateTime");Check(BankMapping.Balance(Payload([rows[0]]),account,now)==10);
        });
        test("Strict instant transfer rejects scheduled execution and roundtrips",()=>{
            var d=new TransferDraft("a","Test","DE89370400440532013000",1,"Test",InstantOnly:true);
            Check(BankingRules.Validate(d)==null&&JsonSerializer.Deserialize<TransferDraft>(JsonSerializer.Serialize(d))!.InstantOnly);
            Check(BankingRules.Validate(d with{Date=DateOnly.FromDateTime(DateTime.Today)})!=null);
        });
        test("MCP token alias and copied Windows configuration preserve exact values",()=>{
            Check(LocalAutomation.ClientToken(null,"mac")=="mac"&&LocalAutomation.ClientToken("win",null)=="win");
            try{LocalAutomation.ClientToken("win","mac");throw new Exception("Conflicting tokens accepted");}catch(InvalidOperationException){}
            var config=JsonNode.Parse(LocalAutomation.ClientConfiguration(@"C:\Program Files\simplebanking\simplebanking.exe","secret"))!["mcpServers"]!["simplebanking"]!;
            Check(config["command"]!.GetValue<string>()==@"C:\Program Files\simplebanking\simplebanking.exe"&&config["args"]![0]!.GetValue<string>()=="--mcp");
        });
        test("MCP lifecycle returns parse, initialization, parameter and tool errors without notification replies",()=>{
            var input=new StringReader(string.Join('\n',new[]{
                "{", "[]", "{\"jsonrpc\":\"2.0\",\"id\":1,\"method\":\"tools/list\"}",
                "{\"jsonrpc\":\"2.0\",\"id\":2,\"method\":\"initialize\",\"params\":{\"protocolVersion\":\"2025-03-26\",\"capabilities\":{},\"clientInfo\":{\"name\":\"test\",\"version\":\"1\"}}}",
                "{\"jsonrpc\":\"2.0\",\"method\":\"notifications/initialized\"}",
                "{\"jsonrpc\":\"2.0\",\"id\":3,\"method\":\"tools/list\"}",
                "{\"jsonrpc\":\"2.0\",\"id\":4,\"method\":\"tools/call\",\"params\":{\"name\":\"get_accounts\",\"arguments\":[]}}",
                "{\"jsonrpc\":\"2.0\",\"id\":5,\"method\":\"tools/call\",\"params\":{\"name\":\"get_accounts\"}}"}));
            var output=new StringWriter();int calls=0;
            Check(LocalAutomation.RunMcp(input,output,(name,args)=>{calls++;if(name=="list_tools")return JsonSerializer.SerializeToNode(LocalAutomation.ToolDefinitions(["get_accounts"]));throw new InvalidOperationException("secret");})==0);
            var replies=output.ToString().Split('\n',StringSplitOptions.RemoveEmptyEntries).Select(x=>JsonNode.Parse(x)!).ToArray();
            Check(replies.Length==7&&calls==2&&replies[0]["error"]!["code"]!.GetValue<int>()==-32700&&replies[2]["error"]!["code"]!.GetValue<int>()==-32002);
            Check(replies[3]["result"]!["protocolVersion"]!.GetValue<string>()=="2025-03-26"&&replies[5]["error"]!["code"]!.GetValue<int>()==-32602&&replies[6]["result"]!["isError"]!.GetValue<bool>());
            Check(!output.ToString().Contains("secret"));
        });
        test("Future booking refresh preserves the latest local edits and account isolation",()=>{
            var today=new DateOnly(2026,11,3);var e=new Entry("future","a",new(2026,11,11),-20,"EUR","Shop","Test","Shopping",Read:true,Flagged:true,Note:"local",CategoryManual:true,Attachments:[new("x","test.pdf",[1,2])]);
            var incoming=e with{Read=false,Flagged=false,Note="",Category="Unkategorisiert",CategoryManual=false,Attachments=null};
            var other=e with{AccountId="b"};
            var result=BookingEnrichment.Refresh([e,other],[],[incoming],"a",today.AddDays(-89),today);
            Check(result.Entries.Single(x=>x.AccountId=="a")==e&&result.Entries.Contains(other)&&result.Unmatched.Count==0);
            var restored=JsonSerializer.Deserialize<List<Entry>>(JsonSerializer.Serialize(result.Entries))!;
            Check(restored.Single(x=>x.AccountId=="a").Read&&restored.Single(x=>x.AccountId=="a").CategoryManual);
            Check(BookingEnrichment.Refresh(restored,[],[incoming],"a",today.AddDays(-89),today).Entries.Single(x=>x.AccountId=="a").Note=="local");
        });
        test("Unmatched read-only edits survive refresh without assigning ambiguous duplicates",()=>{
            var day=new DateOnly(2026,11,3);var e=new Entry("old","a",day,-20,"EUR","Shop","Test","Unkategorisiert",Read:true);
            var result=BookingEnrichment.Refresh([e,e with{Id="second"}],[],[e with{Id="new",Read=false}],"a",day,day);
            Check(result.Unmatched.Count==2&&!result.Entries.Single().Read);
        });
        test("Booking and value dates remain distinct and future dates are labelled",()=>{
            var account=new Account("a","Test","DE89370400440532013000","EUR",0);
            var rows=JsonNode.Parse("""[{"bookingDate":"2026-11-03","valueDate":"2026-11-11","amount":{"amount":"-20","currency":"EUR"}}]""")!.AsArray();
            var e=BankMapping.Entries(rows,account).Single();Check(e.Date==new DateOnly(2026,11,3)&&e.ValueDate==new DateOnly(2026,11,11));
            rows[0]!.AsObject().Remove("bookingDate");e=BankMapping.Entries(rows,account).Single();Check(e.Date==new DateOnly(2026,11,11)&&e.DateSource=="valueDate");
            var data=new BankData{Accounts=[account],Entries=[e]};Check(EncryptedStore.Decrypt(EncryptedStore.Encrypt(data,"fixture"),"fixture").Entries.Single().DateSource=="valueDate");
            Check(TransactionList.DateTitle(e.Date,new(2026,11,3)).Contains("Zukünftiges Datum"));
        });
        test("Passport creation avoids persisted-property flags without allowing key export",()=>{
            var p=PassportHelloKeys.CreationParameters(IntPtr.Zero);
            Check(p.ExportPolicy==null&&p.KeyUsage==null);
            Check(p.Parameters.All(x=>x.Options==CngPropertyOptions.None));
            Check(BitConverter.ToInt32(p.Parameters.Single(x=>x.Name=="Export Policy").GetValue()!)==0);
            Check(BitConverter.ToInt32(p.Parameters.Single(x=>x.Name=="Key Usage").GetValue()!)==(int)CngKeyUsages.Decryption);
        });
    }
}
