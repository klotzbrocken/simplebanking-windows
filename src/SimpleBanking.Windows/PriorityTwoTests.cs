using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
namespace SimpleBanking;
internal static class PriorityTwoTests
{
    static void Check(bool value,[System.Runtime.CompilerServices.CallerLineNumber]int line=0){if(!value)throw new Exception("P2 regression at line "+line);}
    static void Reject(Action action){bool rejected=false;try{action();}catch(Exception ex) when(ex is InvalidDataException or InvalidOperationException){rejected=true;}Check(rejected);}
    sealed class Handler(Func<HttpRequestMessage,HttpResponseMessage> respond):HttpMessageHandler{protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken token)=>Task.FromResult(respond(request));}
    internal static void Register(Action<string,Action> test){
        test("CAMT batches preserve individual amounts, parties and stable identifiers",()=>{
            string Part(string id,string amount,string name)=>$"<TxDtls><Refs><EndToEndId>{id}</EndToEndId></Refs><AmtDtls><TxAmt><Amt Ccy='EUR'>{amount}</Amt></TxAmt></AmtDtls><RltdPties><Cdtr><Nm>{name}</Nm></Cdtr></RltdPties><RmtInf><Ustrd>Rechnung {id}</Ustrd></RmtInf></TxDtls>";
            string xml="<Document><Stmt><Acct><IBAN>DE00</IBAN></Acct><Ntry><Amt Ccy='EUR'>30</Amt><CdtDbtInd>DBIT</CdtDbtInd><BookgDt><Dt>2026-01-02</Dt></BookgDt><NtryDtls>"+Part("one","10","First")+Part("two","20","Second")+"</NtryDtls></Ntry></Stmt></Document>";
            var account=new Account("a","Bank","DE00","EUR",0);var rows=StatementImport.Camt(xml,account);
            Check(rows.Count==2&&rows[0].Amount==-10&&rows[1].Amount==-20&&rows[1].Merchant=="Second"&&rows[0].Purpose=="Rechnung one");Check(rows[0].Id!=rows[1].Id&&StatementImport.NewEntries(rows,StatementImport.Camt(xml,account)).Count==0);
            Reject(()=>StatementImport.Camt(xml.Replace(">30<",">31<"),account));Reject(()=>StatementImport.Camt(xml.Replace("<Amt Ccy='EUR'>20","<Amt Ccy='USD'>20"),account));
        });
        test("Update selection rejects downgrades, foreign downloads and missing hashes",()=>{
            string hash=new('a',64);JsonNode Release(string version,bool beta,string url,string digest)=>new JsonObject{["tag_name"]=version,["draft"]=false,["prerelease"]=beta,["assets"]=new JsonArray(new JsonObject{["name"]="simplebanking-Beta-Setup.exe",["browser_download_url"]=url,["size"]=100L,["digest"]=digest})};
            string url="https://github.com/"+AppUpdater.Repository+"/releases/download/v1.0.0/simplebanking-Beta-Setup.exe";
            var releases=new JsonArray(Release("v1.0.0-beta.1",true,url,"sha256:"+hash));Check(AppUpdater.Select(releases,new(0,19,0),true)?.Version==new Version(1,0,0));Check(AppUpdater.Select(releases,new(0,19,0),false)==null);Check(AppUpdater.Select(releases,new(1,0,0),true)==null);
            Check(AppUpdater.Select(new(Release("v2.0.0",false,"https://evil.example/setup.exe","sha256:"+hash)),new(1,0,0),true)==null);Check(AppUpdater.Select(new(Release("v2.0.0",false,url,"")),new(1,0,0),true)==null);
        });
        test("Update downloads are verified before becoming executable",()=>Task.Run(async()=>{
            byte[] payload=Encoding.UTF8.GetBytes("synthetic update payload");var update=new AppUpdate(new(9,0,0),"v9","",new("https://github.com/"+AppUpdater.Repository+"/releases/download/v9/setup.exe"),payload.Length,Convert.ToHexString(SHA256.HashData(payload)));
            using var handler=new Handler(_=>new(HttpStatusCode.OK){Content=new ByteArrayContent(payload)});string path=await AppUpdater.Download(update,new Progress<int>(),CancellationToken.None,handler);
            try{await AppUpdater.Verify(path,update,CancellationToken.None);await File.AppendAllTextAsync(path,"tampered");bool rejected=false;try{await AppUpdater.Verify(path,update,CancellationToken.None);}catch(InvalidDataException){rejected=true;}Check(rejected);}finally{File.Delete(path);}
            bool corrupt=false;try{await AppUpdater.Download(update with{Sha256=new string('0',64)},new Progress<int>(),CancellationToken.None,handler);}catch(InvalidDataException){corrupt=true;}Check(corrupt);
        }).GetAwaiter().GetResult());
        test("AI streams handle all three providers, completion, truncation and errors",()=>Task.Run(async()=>{
            const string chat="data: {\"choices\":[{\"delta\":{\"content\":\"Hallo \"}}]}\n\ndata: {\"choices\":[{\"delta\":{\"content\":\"Welt\"}}]}\n\ndata: [DONE]\n\n";
            const string anthropic="event: content_block_delta\ndata: {\"type\":\"content_block_delta\",\"delta\":{\"type\":\"text_delta\",\"text\":\"Hallo Welt\"}}\n\nevent: message_stop\ndata: {\"type\":\"message_stop\"}\n\n";
            foreach(string provider in new[]{"OpenAI","Mistral","Anthropic"}){string seen="";using var handler=new Handler(request=>{Check(request.Content!.ReadAsStringAsync().Result.Contains("\"stream\":true"));return new(HttpStatusCode.OK){Content=new StringContent(provider=="Anthropic"?anthropic:chat)};});var result=await AiAssistant.Complete(new(){Provider=provider,Keys=new(){[provider]="test-only"}},"prompt","message",CancellationToken.None,handler,t=>seen+=t);Check(result=="Hallo Welt"&&seen==result);}
            bool rejected=false;try{await AiAssistant.ReadStream(new StringReader(chat.Replace("data: [DONE]\n\n","")),false,_=>{},CancellationToken.None);}catch(InvalidDataException){rejected=true;}Check(rejected);
            rejected=false;try{await AiAssistant.ReadStream(new StringReader("data: {\"error\":{}}\n\n"),false,_=>{},CancellationToken.None);}catch(InvalidOperationException){rejected=true;}Check(rejected);
            using var cancelled=new CancellationTokenSource();cancelled.Cancel();bool stopped=false;try{await AiAssistant.ReadStream(new StringReader(chat),false,_=>{},cancelled.Token);}catch(OperationCanceledException){stopped=true;}Check(stopped);
        }).GetAwaiter().GetResult());
        test("AI SQL sandbox restricts accounts, tables, functions and result sizes",()=>{
            Entry E(string id,string account,decimal amount)=>new(id,account,new(2026,1,1),amount,"EUR","Shop","DE89370400440532013000","Shopping");var rows=new[]{E("one","a",-10),E("two","b",-900),E("three","a",-99) with{Pending=true}};
            var result=AiQuerySandbox.Execute(rows,"a","SELECT sum(amount) AS total, count(*) AS count FROM llm_tx");Check(Convert.ToDecimal(result[0]["total"])==-10&&Convert.ToInt64(result[0]["count"])==1);
            Check(!JsonSerializer.Serialize(AiQuerySandbox.Execute(rows,"a","SELECT purpose FROM llm_tx")).Contains("DE8937"));
            foreach(string sql in new[]{"DELETE FROM llm_tx","SELECT * FROM llm_tx; DROP TABLE llm_tx","SELECT * FROM llm_tx JOIN sqlite_master","SELECT load_extension('evil') FROM llm_tx","SELECT randomblob(1000000000) FROM llm_tx","SELECT * FROM llm_tx JOIN pragma_table_info('llm_tx')"})Reject(()=>AiQuerySandbox.Execute(rows,"a",sql));
            Check(AiQuerySandbox.Execute(Enumerable.Range(0,250).Select(i=>E(i.ToString(),"a",-1)),"a","SELECT * FROM llm_tx LIMIT 9999").Count==200);
            Reject(()=>AiQuerySandbox.Execute(Enumerable.Range(0,1000).Select(i=>E(i.ToString(),"a",-1)),"a","SELECT sum(a.amount+b.amount+c.amount) FROM llm_tx a,llm_tx b,llm_tx c"));
        });
        test("MCP tokens enforce expiry, revocation, per-account scopes and draft idempotency",()=>{
            var (client,token)=AutomationAccess.Create("Fixture",["accounts","drafts"],["a"],1);Check(!client.TokenHash.Contains(token));Check(AutomationAccess.Authenticate([client],token)==client);
            Reject(()=>AutomationAccess.Authenticate([client with{Revoked=true}],token));Reject(()=>AutomationAccess.Authenticate([client with{ExpiresAt=DateTimeOffset.UtcNow.AddMinutes(-1)}],token));Reject(()=>AutomationAccess.Authenticate([client],new string('0',64)));
            var data=new BankData{Accounts=[new("a","A","DE00","EUR",1),new("b","B","DE01","EUR",2)]};Check(AutomationAccess.Restrict(data,client,"get_accounts",new()).Accounts.Count==1);Reject(()=>AutomationAccess.Restrict(data,client,"get_transactions",new()));Reject(()=>AutomationAccess.Restrict(data,client,"get_accounts",new(){["account_id"]="b"}));
            var args=new JsonObject{["account_id"]="a",["recipient"]="Example",["iban"]="DE89370400440532013000",["amount"]=10m,["purpose"]="Invoice",["request_id"]="one"};var draft=AutomationAccess.Prepare(data,client,args);data.AutomationDrafts.Add(draft);Check(AutomationAccess.Prepare(data,client,args).Id==draft.Id&&data.Payments.Count==0&&data.Drafts.Count==0);args["amount"]=11m;Reject(()=>AutomationAccess.Prepare(data,client,args));args["account_id"]="b";Reject(()=>AutomationAccess.Prepare(data,client,args));
            var restored=JsonSerializer.Deserialize<BankData>(JsonSerializer.Serialize(data))!;Check(restored.AutomationDrafts.Single()==draft);
        });
        test("Automatic AI categorization preserves manual and recognized categories",()=>{
            var e=new Entry("1","a",new(2026,1,1),-10,"EUR","Test","","Sonstiges");Check(MainWindow.NeedsAutomaticCategory(e));Check(!MainWindow.NeedsAutomaticCategory(e with{CategoryManual=true}));Check(!MainWindow.NeedsAutomaticCategory(e with{Pending=true}));Check(!MainWindow.NeedsAutomaticCategory(e with{Category="Versicherungen"}));
        });
        test("PDF scanner reads a rotated full-page invoice after the third page",()=>{
            string file=Path.Combine(Path.GetTempPath(),"simplebanking-scan-"+Guid.NewGuid().ToString("N")+".pdf");
            try{using(var document=new PdfSharp.Pdf.PdfDocument()){for(int i=0;i<3;i++){var cover=document.AddPage();cover.Width=PdfSharp.Drawing.XUnit.FromPoint(180);cover.Height=PdfSharp.Drawing.XUnit.FromPoint(180);using var drawing=PdfSharp.Drawing.XGraphics.FromPdfPage(cover);drawing.DrawString("Dokumentseite "+(i+1),new PdfSharp.Drawing.XFont("Arial",12),PdfSharp.Drawing.XBrushes.Black,10,35);}var page=document.AddPage();using(var g=PdfSharp.Drawing.XGraphics.FromPdfPage(page)){g.TranslateTransform(450,60);g.RotateTransform(90);var font=new PdfSharp.Drawing.XFont("Arial",24);g.DrawString("Rechnungsbetrag: 123,45 EUR",font,PdfSharp.Drawing.XBrushes.Black,10,60);}document.Save(file);}string text=DocumentScanner.Read(file);Check(DocumentScanner.Parse(text,"a").Amount==123.45m);}finally{if(File.Exists(file))File.Delete(file);}
        });
    }
}
