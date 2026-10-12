using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace SimpleBanking;

internal static class SelfTests
{
    internal sealed class HelloFixtureKeys : IHelloKeys,IDisposable
    {
        readonly Dictionary<string,RSA> keys=[];
        internal bool Cancel;
        internal Action? AfterUnwrap;
        internal int Count=>keys.Count;
        internal int Prompts;
        public byte[] Create(string name,IntPtr parent){var rsa=RSA.Create(2048);keys.Add(name,rsa);return rsa.ExportSubjectPublicKeyInfo();}
        public byte[] Unwrap(string name,byte[] hash,byte[] wrapped,IntPtr parent){Prompts++;if(Cancel)throw new CryptographicException(unchecked((int)0x80090036));var rsa=keys[name];if(!CryptographicOperations.FixedTimeEquals(hash,SHA256.HashData(rsa.ExportSubjectPublicKeyInfo())))throw new CryptographicException();var result=rsa.Decrypt(wrapped,RSAEncryptionPadding.Pkcs1);AfterUnwrap?.Invoke();return result;}
        public void Delete(string name){if(keys.Remove(name,out var rsa))rsa.Dispose();}
        public void Dispose(){foreach(var rsa in keys.Values)rsa.Dispose();keys.Clear();}
    }
    sealed class LogoFixtureHandler(byte[] png,bool missing=false):HttpMessageHandler {
        internal int Calls;
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken token){
            Calls++;if(request.RequestUri?.Host!="img.logo.dev"||!request.RequestUri.Query.Contains("fallback=404"))throw new Exception("Unexpected logo destination");
            return Task.FromResult(new HttpResponseMessage(missing?System.Net.HttpStatusCode.NotFound:System.Net.HttpStatusCode.OK){Content=new ByteArrayContent(png)});
        }
    }
    sealed class AiFixtureHandler(string provider) : HttpMessageHandler
    {
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken cancellation){
            var payload=JsonNode.Parse(await request.Content!.ReadAsStringAsync(cancellation))!;
            if(payload["messages"]==null||payload["model"]==null)throw new Exception("Missing AI payload");
            if(provider=="Anthropic"?!request.Headers.Contains("x-api-key"):request.Headers.Authorization?.Scheme!="Bearer")throw new Exception("Wrong AI authentication contract");
            string json=provider=="Anthropic"?"{\"content\":[{\"type\":\"text\",\"text\":\"Fixture answer\"}]}":"{\"choices\":[{\"message\":{\"content\":\"Fixture answer\"}}]}";
            return new(System.Net.HttpStatusCode.OK){Content=new StringContent(json,System.Text.Encoding.UTF8,"application/json")};
        }
    }
    public static int Run(string[] args)
    {
        var results = new List<object>(); int failed = 0;
        void Test(string name, Action action)
        {
            try { action(); results.Add(new { name, passed = true }); }
            catch (Exception e) { failed++; results.Add(new { name, passed = false, error = e.GetType().Name + ": " + e.Message }); }
        }
        static void Check(bool condition) { if (!condition) throw new Exception("Assertion failed"); }
        MacUpdateTests.Register(Test);PriorityOneTests.Register(Test);
        Test("Remote logos use whitelist, persist cache and enforce daily request budget",()=>{
            string root=Path.Combine(Path.GetTempPath(),"sb-logo-test-"+Guid.NewGuid().ToString("N"));
            using var bitmap=new Bitmap(128,128);using var stream=new MemoryStream();bitmap.Save(stream,System.Drawing.Imaging.ImageFormat.Png);
            var handler=new LogoFixtureHandler(stream.ToArray());var service=new RemoteMerchantLogos(root,handler);var sample=DemoData.Create().Entries[0];
            var entries=RemoteMerchantLogos.KnownMerchants.Select(n=>sample with{Merchant=n,Purpose="",CustomLogo=null,MerchantLogo=null}).Where(e=>!MerchantArtwork.HasBundled(e)).DistinctBy(e=>RemoteMerchantLogos.Domain(e.Merchant)).Take(4).ToArray();
            try {
                Check(RemoteMerchantLogos.Domain("private merchant arbitrary.example")==null&&entries.Length==4);
                Check(service.Fetch(entries,CancellationToken.None).GetAwaiter().GetResult()&&handler.Calls==3);
                using var cached=service.Load(entries[0].Merchant);Check(cached!=null&&cached.Width==128);
                Check(!service.Fetch(entries,CancellationToken.None).GetAwaiter().GetResult()&&handler.Calls==3);
                var restarted=new RemoteMerchantLogos(root,handler);Check(!restarted.Fetch(entries,CancellationToken.None).GetAwaiter().GetResult()&&handler.Calls==3);
            } finally {if(Directory.Exists(root))Directory.Delete(root,true);}
        });
        Test("Remote logos reject tiny artwork and cache a missing logo",()=>{
            string root=Path.Combine(Path.GetTempPath(),"sb-logo-test-"+Guid.NewGuid().ToString("N"));
            using var bitmap=new Bitmap(16,16);using var stream=new MemoryStream();bitmap.Save(stream,System.Drawing.Imaging.ImageFormat.Png);
            try{RemoteMerchantLogos.ValidateImage(stream.ToArray());throw new Exception("Tiny logo accepted");}catch(InvalidDataException){}
            var handler=new LogoFixtureHandler([],true);var service=new RemoteMerchantLogos(root,handler);var sample=DemoData.Create().Entries[0];
            var entry=RemoteMerchantLogos.KnownMerchants.Select(n=>sample with{Merchant=n,Purpose=""}).First(e=>!MerchantArtwork.HasBundled(e));
            try {Check(!service.Fetch([entry],CancellationToken.None).GetAwaiter().GetResult());Check(!service.Fetch([entry],CancellationToken.None).GetAwaiter().GetResult()&&handler.Calls==1);}
            finally {if(Directory.Exists(root))Directory.Delete(root,true);}
        });
        Test("Hello envelope requires private-key release, detects tampering and revokes",()=>{
            string root=Path.Combine(Path.GetTempPath(),"sb-hello-test-"+Guid.NewGuid().ToString("N"));
            using var keys=new HelloFixtureKeys();var hello=new HelloVault(root,keys);
            try {
                const string password="Synthetic-private-master-password";
                hello.Enable(password,IntPtr.Zero);Check(hello.Enabled&&keys.Count==1&&keys.Prompts==1);
                string path=Path.Combine(root,"hello.json");byte[] original=File.ReadAllBytes(path);Check(!System.Text.Encoding.UTF8.GetString(original).Contains(password));
                Check(hello.Unlock(IntPtr.Zero)==password&&keys.Prompts==2);
                var e=JsonSerializer.Deserialize<HelloVault.Envelope>(original)!;e.Tag[0]^=1;File.WriteAllBytes(path,JsonSerializer.SerializeToUtf8Bytes(e));
                try{hello.Unlock(IntPtr.Zero);throw new Exception("Tampered envelope accepted");}catch(CryptographicException){}
                File.WriteAllBytes(path,original);keys.Cancel=true;
                try{hello.Unlock(IntPtr.Zero);throw new Exception("Canceled Hello unlocked");}catch(CryptographicException ex){Check(HelloVault.Canceled(ex));}
                keys.Cancel=false;hello.Disable();Check(!hello.Enabled&&keys.Count==0);
            } finally {if(Directory.Exists(root))Directory.Delete(root,true);}
        });
        Test("Hello enrollment cancellation preserves previous registration and root binding",()=>{
            string root=Path.Combine(Path.GetTempPath(),"sb-hello-test-"+Guid.NewGuid().ToString("N"));
            using var keys=new HelloFixtureKeys();var hello=new HelloVault(root,keys);
            try {
                hello.Enable("Original-password",IntPtr.Zero);keys.Cancel=true;
                try{hello.Enable("Replacement-password",IntPtr.Zero);throw new Exception("Canceled enrollment saved");}catch(CryptographicException){}
                keys.Cancel=false;Check(keys.Count==1&&hello.Unlock(IntPtr.Zero)=="Original-password");
                string other=Path.Combine(root,"other");Directory.CreateDirectory(other);File.Copy(Path.Combine(root,"hello.json"),Path.Combine(other,"hello.json"));
                try{new HelloVault(other,keys).Unlock(IntPtr.Zero);throw new Exception("Different vault accepted Hello registration");}catch(InvalidDataException){}
                hello.Enable("Replacement-password",IntPtr.Zero);Check(keys.Count==1&&hello.Unlock(IntPtr.Zero)=="Replacement-password");
                File.WriteAllText(Path.Combine(root,"hello.json"),"{}");hello.Disable();Check(!hello.Enabled);
            } finally {if(Directory.Exists(root))Directory.Delete(root,true);}
        });
        Test("Automation pagination is stable and filters scope every summary",()=>{
            var data=DemoData.Create();var sample=data.Entries[0];var today=DateOnly.FromDateTime(DateTime.Today);
            data.Entries=Enumerable.Range(0,12).Select(i=>sample with{Id=i.ToString("D2"),AccountId=i<10?"a":"b",Date=today,Amount=-10,Currency=i==9?"USD":"EUR",Pending=false,Category="Test"}).ToList();
            var args=new JsonObject{["account_id"]="a",["currency"]="EUR",["limit"]=3,["offset"]=3};
            var page=JsonSerializer.SerializeToNode(LocalAutomation.Query(data,"get_transactions",args))!.AsArray();
            Check(page.Count==3&&page[0]!["id"]!.GetValue<string>()=="03"&&page[2]!["id"]!.GetValue<string>()=="05");
            var summary=JsonSerializer.SerializeToNode(LocalAutomation.Query(data,"get_spending_summary",args))!.AsArray();
            Check(summary.Count==1&&summary[0]!["total"]!.GetValue<decimal>()==-90);
            var excluded=JsonSerializer.SerializeToNode(LocalAutomation.Query(data,"get_merchants",new(){["search"]="does-not-exist"}))!.AsArray();Check(excluded.Count==0);
            try{LocalAutomation.Query(data,"get_transactions",new(){["date_from"]="2026-10-01",["date_to"]="2026-09-01"});throw new Exception("Reversed range accepted");}catch(InvalidDataException){}
        });
        Test("Local automation request framing limits allocation and respects cancellation",()=>{
            Check(LocalAutomation.ReadRequest(new StringReader("{}\r\nnext"),CancellationToken.None).GetAwaiter().GetResult()=="{}");
            try{LocalAutomation.ReadRequest(new StringReader(new string('x',65537)),CancellationToken.None).GetAwaiter().GetResult();throw new Exception("Oversized request accepted");}catch(InvalidDataException){}
            using var canceled=new CancellationTokenSource();canceled.Cancel();
            try{LocalAutomation.ReadRequest(new StringReader("{}"),canceled.Token).GetAwaiter().GetResult();throw new Exception("Cancellation ignored");}catch(OperationCanceledException){}
        });
        Test("Theme ZIP roundtrip preserves case-insensitive colors and PNG assets",()=>{
            string path=Path.Combine(Path.GetTempPath(),Guid.NewGuid()+".zip");
            try{
                using var bitmap=new Bitmap(12,8);using(var g=Graphics.FromImage(bitmap))g.Clear(Color.Coral);using var stream=new MemoryStream();bitmap.Save(stream,System.Drawing.Imaging.ImageFormat.Png);
                var values=ThemeFiles.Read("id=fixture\nname=Fixture\nACCENT=#123456\nWALLPAPER=paper.png\nlogo=paper.png\n");var package=new ThemePackage(values,new(){["paper.png"]=stream.ToArray()});package.Write(path);
                var read=ThemePackage.Read(path);Check(read.Values["accent"]=="#123456"&&read.Images.Count==1);
                var prefs=new AppPreferences{ThemeId="custom",CustomTheme=read.Values,CustomThemeImages=read.Images};prefs=JsonSerializer.Deserialize<AppPreferences>(JsonSerializer.Serialize(prefs))!;
                using var image=ThemePalette.For(prefs).Image("wallpaper");Check(image!=null&&image.Width==12&&ThemePalette.For(prefs).Accent==ColorTranslator.FromHtml("#123456"));
            }finally{File.Delete(path);}
        });
        Test("Theme packages reject traversal, duplicate configs and unsupported referenced art",()=>{
            string path=Path.Combine(Path.GetTempPath(),Guid.NewGuid()+".zip");
            try{
                void Reject(params (string Name,string Text)[] entries){
                    using(var file=File.Create(path))using(var zip=new System.IO.Compression.ZipArchive(file,System.IO.Compression.ZipArchiveMode.Create)){foreach(var e in entries){using var writer=new StreamWriter(zip.CreateEntry(e.Name).Open());writer.Write(e.Text);}}
                    bool rejected=false;try{ThemePackage.Read(path);}catch(InvalidDataException){rejected=true;}Check(rejected);
                }
                Reject(("../theme.cfg","id=x\nname=X"));Reject(("one.cfg","id=x\nname=X"),("two.cfg","id=y\nname=Y"));
                Reject(("theme.cfg","id=x\nname=X\nwallpaper=missing.png"));Reject(("theme.cfg","id=x\nname=X\nlogo=logo.svg"));
            }finally{File.Delete(path);}
        });
        Test("Own parties use vault accounts and learn only unambiguous account-side names",()=>{
            var a=new Account("a","Giro","DE89370400440532013000","EUR",0);var b=new Account("b","Sparen","DE12500105170648489890","EUR",0,OwnerName:"Anna Beispiel Dr.");
            Check(OwnPartyRegistry.Contains(new[]{a,b}," ANNA  BEISPIEL ",null));Check(!OwnPartyRegistry.Contains(new[]{a},"Anna Beispiel",null));
            Check(OwnPartyRegistry.Contains(new[]{a,b},"Andere",b.Iban));
            var entry=new Entry("x","a",new DateOnly(2026,9,1),-10,"EUR","Anna Beispiel","Zahlung an Kleiner Laden","",BankCounterpartyName:"Anna Beispiel");
            Check(MerchantResolver.Resolve(entry,a,new[]{a,b})=="Kleiner Laden");
            JsonObject Tx(string name,string iban)=>new(){["amount"]=new JsonObject{["amount"]="-10"},["debtor"]=new JsonObject{["name"]=name,["iban"]=iban},["creditor"]=new JsonObject{["name"]="Händler",["iban"]=b.Iban}};
            Check(OwnPartyRegistry.Learn(a,new JsonArray(Tx("Anna Beispiel",a.Iban))).OwnerName=="Anna Beispiel");
            Check(OwnPartyRegistry.Learn(a,new JsonArray(Tx("Fremder Name",b.Iban))).OwnerName==null);
            Check(OwnPartyRegistry.Learn(a,new JsonArray(Tx("Anna Beispiel",a.Iban),Tx("Ben Anders",a.Iban))).OwnerName==null);
            Check(JsonSerializer.Deserialize<Account>(JsonSerializer.Serialize(b))!.OwnerName==b.OwnerName);
        });
        Test("Bank reads reuse consent, fall back once with fresh ticket and consume scoped sessions",()=>{
            var access=new BankReadAccess();var connection=new BankConnection("local-a","provider","Bank","user","fixture-password",[1,2,3]);int tickets=0,calls=0;
            Task<string> Ticket()=>Task.FromResult("fixture-"+(++tickets));
            var response=access.RunAsync(connection,"Transactions",new(){["op"]="transactions"},Ticket,request=>{
                calls++;Check(request["useConnectionData"]!.GetValue<bool>());Check(request["credentials"]!["password"]==null);Check(request["session"]==null);
                return Task.FromResult(new JsonObject{["kind"]="result",["session"]=new JsonArray(7,8),["connectionData"]=new JsonArray(4,5)});
            },CancellationToken.None).GetAwaiter().GetResult();Check(calls==1&&tickets==1);
            Check(access.Take("local-b","Transactions")==null&&access.Take("local-a","Balances")==null);
            calls=0;access.RunAsync(connection,"Transactions",new(){["op"]="transactions"},Ticket,request=>{
                calls++;if(calls==1){Check(request["session"]==null);throw new YaxiException("InterruptError","fixture");}
                Check(!request["useConnectionData"]!.GetValue<bool>());Check(request["ticket"]!.GetValue<string>()=="fixture-3");Check(request["credentials"]!["connectionData"]!.AsArray().Count==3);
                Check(request["session"]!.AsArray()[0]!.GetValue<int>()==7);return Task.FromResult(new JsonObject{["kind"]="result"});
            },CancellationToken.None).GetAwaiter().GetResult();Check(calls==2&&access.Take("local-a","Transactions")==null);
            var updated=BankReadAccess.UpdatedConsent(connection,response);Check(updated.ConnectionData!.SequenceEqual(new[]{4,5})&&updated.ConnectionDataUpdatedAt!=null);
            Check(BankReadAccess.UpdatedConsent(updated,new()).ConnectionData!.SequenceEqual(new[]{4,5}));
            var persisted=JsonSerializer.Deserialize<BankConnection>(JsonSerializer.Serialize(updated))!;Check(persisted.ConnectionData!.SequenceEqual(new[]{4,5}));
            for(int month=1;month<=12;month++){var today=new DateOnly(2026,month,28);Check(today.DayNumber-BankReadAccess.AutomaticFrom(today).DayNumber==89);}
        });
        Test("Bank read errors do not trigger repeated authorization or discard consent",()=>{
            var access=new BankReadAccess();var connection=new BankConnection("a","b","Bank","","",[9]);int calls=0;
            try{access.RunAsync(connection,"Balances",new(){["op"]="balances"},()=>Task.FromResult("ticket"),request=>{calls++;throw new YaxiException("ProviderError","fixture");},CancellationToken.None).GetAwaiter().GetResult();throw new Exception("Error swallowed");}
            catch(YaxiException e){Check(e.ErrorType=="ProviderError"&&calls==1&&connection.ConnectionData![0]==9);}
            access.Remember("a","Transactions",new(){["session"]=new JsonArray(1)});access.Clear();Check(access.Take("a","Transactions")==null);
        });
        Test("Shared merchant logos are account scoped, inherited and serialized once",()=>{
            var entry=new Entry("x","a",new DateOnly(2026,9,1),-10,"EUR","Local Shop","","Shopping");
            using var bitmap=new Bitmap(30,10);using(var g=Graphics.FromImage(bitmap))g.Clear(Color.Red);
            var bytes=MerchantLogoStore.Normalize(bitmap);var data=new BankData();data.MerchantLogos[MerchantLogoStore.Key(entry)!]=bytes;
            var inherited=MerchantLogoStore.Apply(entry,data.MerchantLogos);Check(inherited.MerchantLogo==bytes);
            Check(MerchantLogoStore.Apply(entry with{Id="future",Merchant=" LOCAL  SHOP "},data.MerchantLogos).MerchantLogo==bytes);
            Check(MerchantLogoStore.Apply(entry with{AccountId="b"},data.MerchantLogos).MerchantLogo==null);
            Check(MerchantLogoStore.Key(entry with{Merchant="Unbekannt"})==null);
            Check(MerchantLogoStore.Key(entry with{Merchant="PayPal (Intermediaer)"})==null);
            data.Entries=[inherited,inherited with{Id="next"}];string json=JsonSerializer.Serialize(data);
            Check(json.Split(Convert.ToBase64String(bytes)).Length==2);
            var restored=JsonSerializer.Deserialize<BankData>(json)!;Check(restored.Entries[0].MerchantLogo==null);
            using var image=MerchantArtwork.Load(MerchantLogoStore.Apply(restored.Entries[0],restored.MerchantLogos));Check(image!=null&&image.Width==128);
            using var blue=new Bitmap(10,10);using(var g=Graphics.FromImage(blue))g.Clear(Color.Blue);
            using var custom=(Bitmap)MerchantArtwork.Load(inherited with{CustomLogo=MerchantLogoStore.Normalize(blue)})!;Check(custom.GetPixel(64,64).B>200);
            data.MerchantLogos.Clear();Check(MerchantLogoStore.Apply(inherited,data.MerchantLogos).MerchantLogo==null);
        });
        Test("Merchant pipeline extracts providers, structured cards and preserves bank originals",()=>{
            var entry=new Entry("x","a",new DateOnly(2026,9,1),-10,"EUR","PayPal Europe","Ihr Einkauf bei REWE 1234","",BankCounterpartyName:"PayPal Europe");
            Check(MerchantResolver.Resolve(entry)=="Rewe");Check(MerchantResolver.Resolve(entry with{Purpose="Unbekannte Referenz"})=="PayPal (Intermediaer)");
            Check(MerchantResolver.Resolve(entry with{Merchant="Debitk.1",BankCounterpartyName="Debitk.1",Purpose="ABWA+CRV.Cafeteria Jung Sti/Street/City"})=="Cafeteria Jung Sti");
            Check(MerchantResolver.Resolve(entry with{BankCounterpartyName="",Purpose=@"Bezahlung Karte 16:57 BILLA DANKT 0002220\WIEN"})=="Billa Dankt");
            Check(MerchantResolver.Resolve(entry with{BankCounterpartyName="F01234",Purpose=""})=="Bargeldabhebung");
            Check(MerchantResolver.Clean("MUSTER GMBH")=="Muster GmbH");Check(MerchantResolver.StripBranch("12345 67890")=="12345 67890");
            var own=entry with{BankCounterpartyName="Kontoinhaber",CounterpartyIban="DE89370400440532013000",Purpose="Zahlung an Kleiner Laden"};Check(MerchantResolver.Resolve(own,new("a","Giro",own.CounterpartyIban,"EUR",0))=="Kleiner Laden");
            var rule=new MerchantUserRule("r",true,0,"Empfänger","Enthält","PayPal","Eigener Händler");var changed=UserMerchantRules.Apply(entry,[rule]);Check(changed.Merchant=="Eigener Händler"&&changed.OriginalMerchant=="PayPal Europe"&&changed.BankCounterpartyName==entry.BankCounterpartyName);
        });
        Test("Per-booking merchant override wins rules and survives reconciliation",()=>{
            var entry=new Entry("x","a",new DateOnly(2026,9,1),-10,"EUR","PayPal","Order","",MerchantOverride:"Mein Laden");var rule=new MerchantUserRule("r",true,0,"Empfänger","Enthält","paypal","Regelname");var changed=UserMerchantRules.Apply(entry,[rule]);Check(changed.Merchant=="Mein Laden"&&changed.OriginalMerchant=="PayPal");Check(UserMerchantRules.Apply(changed with{MerchantOverride=null},[rule]).Merchant=="Regelname");
            var reconciled=BookingEnrichment.Reconcile([changed],[entry with{MerchantOverride=null}]).Single();Check(reconciled.MerchantOverride=="Mein Laden");Check(JsonSerializer.Deserialize<Entry>(JsonSerializer.Serialize(reconciled))!.MerchantOverride=="Mein Laden");
        });
        Test("Theme imports validate colors and roundtrip without replacing built-ins",()=>{
            var theme=ThemeFiles.Read("id=test\nname=Test\ncardLight=#123456\nripple=off");Check(ThemeFiles.Read(ThemeFiles.Write(theme))["cardLight"]=="#123456");var pref=new AppPreferences{ThemeId="custom",CustomTheme=theme};Check(ThemePalette.For(pref).Surface.ToArgb()==ColorTranslator.FromHtml("#123456").ToArgb());
            try{ThemeFiles.Read("id=x\nname=X\naccent=bad");throw new Exception("Invalid color accepted");}catch(InvalidDataException){}
            pref.CustomTheme!["cardLight"]="invalid";Check(ThemePalette.For(pref).IsDefault);
        });
        Test("Recipient suggestions isolate accounts and IBANs and prefer frequent amount",()=>{
            var day=new DateOnly(2026,9,28);var entry=new Entry("a","one",day,-20,"EUR","Display name","Rent","",CounterpartyIban:"DE89370400440532013000",BankCounterpartyName:"Legal Name");
            var result=RecipientHistory.Candidates(new[]{entry,entry with{Id="b",Date=day.AddDays(-2)},entry with{Id="c",Amount=-50},entry with{Id="d",AccountId="two"},entry with{Id="e",Pending=true},entry with{Id="f",Amount=100},entry with{Id="g",CounterpartyIban="DE12500105170648489890"}},"one");
            Check(result.Length==2&&result[0].Frequency==3&&result[0].Amount==20&&result[0].Name=="Legal Name");Check(result[0].Iban!=result[1].Iban);Check(QuickSendPanel.TemplateSymbol(new("id","🏠 Miete",new("one","Name","DE89370400440532013000",20,"")))=="🏠 ");
        });
        Test("Artwork preserves aspect ratio, alpha and graphics state",()=>{
            Check(ArtworkDrawing.Fit(new Size(100,50),new Rectangle(0,0,24,24))==new Rectangle(0,6,24,12));
            using var source=new Bitmap(40,20);using(var g=Graphics.FromImage(source))g.Clear(Color.Red);
            using var target=new Bitmap(24,24);using(var g=Graphics.FromImage(target)){g.Clear(Color.White);var mode=g.InterpolationMode;ArtworkDrawing.Draw(g,source,new Rectangle(0,0,24,24),.5f);Check(g.InterpolationMode==mode);}
            Check(target.GetPixel(12,0).ToArgb()==Color.White.ToArgb());var pixel=target.GetPixel(12,12);if(!(pixel.R>245&&pixel.G is >100 and <230&&pixel.B==pixel.G))throw new Exception("Opacity pixel: "+pixel);
            var template=new PaymentTemplate("a","Favorite",new("account","Recipient","DE89370400440532013000",20,"Test"),true);Check(JsonSerializer.Deserialize<PaymentTemplate>(JsonSerializer.Serialize(template))==template);
        });
        Test("Original theme palettes preserve mood level and disable BTX imagery and ripple",()=>{
            var preferences=new AppPreferences{ThemeId="gameboy"};var card=BalancePresentation.Create(-20,preferences);Check(card.Level==1&&card.Top.ToArgb()==ColorTranslator.FromHtml("#9BBC0F").ToArgb()&&card.Top==card.Bottom);
            preferences.ThemeId="btx";var btx=ThemePalette.For(preferences);Check(!btx.Ripple&&!btx.MerchantLogos&&!btx.BankLogos&&btx.Text("Konto")=="KONTO");using var font=btx.Font(14);Check(font.FontFamily.Name.Contains("VT323"));
            preferences.ThemeId="default";preferences.DarkAppearance=true;card=BalancePresentation.Create(2500,preferences);Check(card.Top.GetBrightness()<.4&&card.Ink.GetBrightness()>.4);
        });
        Test("OFX export roundtrip preserves booked amounts, text and account scope",()=>{
            var data=DemoData.Create();var account=data.Accounts[0];data.Entries[0]=data.Entries[0] with{Merchant="A & B <Shop>",Purpose="Rechnung <1> & Test"};var result=StatementImport.Ofx(StatementExport.Ofx(account,data.Entries),account);var expected=data.Entries.Where(e=>e.AccountId==account.Id&&!e.Pending).ToArray();Check(result.Count==expected.Length&&result.Sum(e=>e.Amount)==expected.Sum(e=>e.Amount)&&result.Any(e=>e.Merchant=="A & B <Shop>"&&e.Purpose=="Rechnung <1> & Test"));
        });
        Test("AI providers follow source contracts without network; context excludes banking secrets",()=>{
            foreach(string provider in new[]{"Anthropic","Mistral","OpenAI"}){using var handler=new AiFixtureHandler(provider);var config=new AiConfiguration{Provider=provider,Keys=new(){[provider]="synthetic-key"}};Check(AiAssistant.Complete(config,"system","user",CancellationToken.None,handler).GetAwaiter().GetResult()=="Fixture answer");}
            var row=DemoData.Create().Entries[0] with{Purpose="IBAN: DE89 3704 0044 0532 0130 00",Note="private-note",CounterpartyIban="DE89370400440532013000"};string context=AiAssistant.Context([row]);Check(!context.Contains("DE89")&&!context.Contains("private-note")&&!context.Contains("CounterpartyIban"));
            Check(AiAssistant.ParseCategories("[{\"id\":\"0\",\"category\":\"transport\"}]",1)[0]=="Mobilität");try{AiAssistant.ParseCategories("[{\"id\":\"1\",\"category\":\"transport\"}]",1);throw new Exception("AI assigned outside submitted batch");}catch(InvalidDataException){}
        });
        Test("Roundups use booked expenses and exclude reserved days and savings transfers",()=>{
            Check(RoundupMath.Cents(-4.25m,100)==75&&RoundupMath.Cents(-4m,100)==0&&RoundupMath.Cents(4.25m,100)==0&&RoundupMath.Cents(-4.25m,500)==75);
            var day=new DateOnly(2026,9,1);var e=new Entry("a","a",day,-4.25m,"EUR","Shop","","Shopping");var rows=new[]{e,e with{Id="pending",Pending=true},e with{Id="usd",Currency="USD"},e with{Id="saved",CounterpartyIban="DE89370400440532013000"},e with{Id="reserved",Date=day.AddDays(1)}};
            var result=RoundupMath.Days(rows,100,"DE89370400440532013000",new HashSet<DateOnly>{day.AddDays(1)});Check(result.Count==1&&result[day]==.75m);
        });
        Test("Automation excludes enrichment and rejects write commands",()=>{
            var data=DemoData.Create();data.SimpleSendLicense="secret-license";data.Entries[0]=data.Entries[0] with{Note="private-note"};
            string json=JsonSerializer.Serialize(LocalAutomation.Query(data,"get_transactions",new(){["days"]=3650}));Check(!json.Contains("private-note")&&!json.Contains("secret-license")&&!json.Contains("Content"));
            try{LocalAutomation.Query(data,"send_payment",new());throw new Exception("Write tool accepted");}catch(InvalidOperationException){}
        });
        Test("Encrypted enrichments and payment journal survive roundtrip",()=>{
            var data=DemoData.Create();data.Roundups.Add(new("pot",data.Accounts[0].Id,[DateOnly.FromDateTime(DateTime.Today)],1.25m,"Ausgang unklar","DE89370400440532013000","Test"));var restored=EncryptedStore.Decrypt(EncryptedStore.Encrypt(data,"synthetic-test"),"synthetic-test");
            Check(restored.Entries.Single(e=>e.Id=="demo-reminder").Attachments![0].Content.SequenceEqual(data.Entries.Single(e=>e.Id=="demo-reminder").Attachments![0].Content));Check(restored.Payments.Count==data.Payments.Count&&restored.Roundups[0].State=="Ausgang unklar");
        });
        Test("Document parser validates IBAN and prefers invoice total",()=>{
            var draft=DocumentScanner.Parse("Empfänger: Muster GmbH\nIBAN: DE89 3704 0044 0532 0130 00\nPosition 3,90 EUR\nGesamtbetrag: 1.234,56 EUR\nRechnungsnummer: R-123","a");Check(draft.Iban=="DE89370400440532013000"&&draft.Amount==1234.56m&&draft.Name=="Muster GmbH"&&draft.Purpose=="R-123");
        });
        Test("QR scanner decodes a synthetic SEPA GiroCode",()=>{
            string text="BCD\n002\n1\nSCT\n\nTest\nDE89370400440532013000\nEUR1.00\n\n\nTest";var writer=new ZXing.BarcodeWriterPixelData{Format=ZXing.BarcodeFormat.QR_CODE,Options=new ZXing.Common.EncodingOptions{Width=420,Height=420,Margin=4}};var pixels=writer.Write(text);using var bitmap=new Bitmap(pixels.Width,pixels.Height,System.Drawing.Imaging.PixelFormat.Format32bppArgb);var bits=bitmap.LockBits(new Rectangle(0,0,bitmap.Width,bitmap.Height),System.Drawing.Imaging.ImageLockMode.WriteOnly,System.Drawing.Imaging.PixelFormat.Format32bppArgb);try{System.Runtime.InteropServices.Marshal.Copy(pixels.Pixels,0,bits.Scan0,pixels.Pixels.Length);}finally{bitmap.UnlockBits(bits);}Check(DocumentScanner.ReadQr(bitmap)==text);
        });
        Test("OCR recognizes synthetic invoice locally",()=>{
            using var bitmap=new Bitmap(1500,240);bitmap.SetResolution(300,300);using(var graphics=Graphics.FromImage(bitmap)){graphics.Clear(Color.White);using var font=new Font("Arial",42,FontStyle.Regular,GraphicsUnit.Pixel);graphics.DrawString("Rechnungsbetrag: 123,45 EUR",font,Brushes.Black,30,70);}using var stream=new MemoryStream();bitmap.Save(stream,System.Drawing.Imaging.ImageFormat.Png);Check(DocumentScanner.Ocr(stream.ToArray()).Contains("123,45"));
        });
        Test("Enrichment follows unique pending to booked matches and refuses ambiguity",()=>{
            var old=new Entry("pending","a",new DateOnly(2026,9,1),-10,"EUR","Shop","Order","Shopping",Pending:true,Note:"Keep",Attachments:[new("att","file.pdf",[1,2,3])],ReminderAt:new DateTime(2026,10,1));var next=old with{Id="booked",Date=old.Date.AddDays(2),Pending=false,Note="",Attachments=null,ReminderAt=null};var matched=BookingEnrichment.Reconcile([old], [next]).Single();Check(matched.Note=="Keep"&&matched.Attachments?.Count==1&&matched.ReminderAt!=null);Check(BookingEnrichment.Reconcile([old,old with{Id="other"}],[next]).Single().Note=="");Check(BookingEnrichment.Reconcile([old with{AccountId="other"}],[next]).Single().Note=="");
        });
        Test("Attention detects duplicate and due reminder but excludes pending",()=>{
            var now=new DateOnly(2026,9,20);var e=new Entry("1","a",now,-45,"EUR","Testshop","","Sonstiges");var cards=AttentionInbox.Analyze([e,e with{Id="2"},e with{Id="reminder",Amount=0,ReminderAt=new DateTime(2026,9,19)}],now,new());Check(cards.Any(c=>c.Key.StartsWith("duplicate:"))&&cards.Any(c=>c.Key=="reminder:reminder"));Check(!AttentionInbox.Analyze([e,e with{Id="2",Pending=true}],now,new()).Any(c=>c.Key.StartsWith("duplicate:")));
        });
        Test("Merchant rules preserve originals, respect priority and reset",()=>{
            var entry=new Entry("1","a",new DateOnly(2026,9,1),-1,"EUR","Payment provider","Order NETFLIX","Sonstiges");var rule=new MerchantUserRule("1",true,1,"Verwendungszweck","Enthält","netflix","Netflix");var changed=UserMerchantRules.Apply(entry,[rule]);Check(changed.Merchant=="Netflix"&&changed.OriginalMerchant==entry.Merchant);Check(UserMerchantRules.Apply(changed,[]).Merchant==entry.Merchant);Check(!UserMerchantRules.Matches(rule with{MatchType="Regex",Pattern="["},entry));
        });
        Test("Monthly report excludes pending and other accounts, paginates 55 booked rows",()=>{
            string path=Path.Combine(Path.GetTempPath(),"simple-report-"+Guid.NewGuid()+".pdf");
            try {
                var account=new Account("report","Testkonto","DE89370400440532013000","EUR",100);
                var entries=Enumerable.Range(0,56).Select(i=>new Entry(i.ToString(),account.Id,new DateOnly(2026,9,2),-10,"EUR","Händler "+i,"Test","Sonstiges")).ToArray();
                Check(MonthlyReport.Rows(entries.Append(entries[0] with{Pending=true}),new DateOnly(2026,9,1)).Length==56);
                MonthlyReport.Write(path,account,entries.Append(entries[0] with{Id="foreign",AccountId="other"}),new DateOnly(2026,9,1));
                using var pdf=PdfSharp.Pdf.IO.PdfReader.Open(path,PdfSharp.Pdf.IO.PdfDocumentOpenMode.Import);Check(pdf.PageCount==4);Check(pdf.Pages[0].Width.Point==595);
            }finally{if(File.Exists(path))File.Delete(path);}
        });
        Test("Bank logos resolve brand aliases, connection names, IBAN and unknown provider IDs",()=>{
            var account=new Account("a","Girokonto","","EUR",0,LogoId:"provider-unknown",BankName:"Deutsche Bank");
            Check(BankArtwork.Resolve(account)=="deutsche");
            Check(BankArtwork.Resolve(account with {BankName="Volksbank"})=="volk");
            Check(BankArtwork.Resolve(account with {BankName=null,Iban="DE89500105170000000000"})=="ing");
            Check(BankArtwork.Resolve(account with {BankName=null,LogoId="bunq"})=="bunq");
            using var logo=BankArtwork.LoadLogo(account,20);Check(logo!=null);
            Check(BankArtwork.Resolve(account with {BankName=null,LogoId="../missing"})==null);
        });
        Test("Ripple clamps edges, preserves opaque pixels and is idle at time zero",()=>{
            using var input=new Bitmap(40,20,System.Drawing.Imaging.PixelFormat.Format32bppArgb);
            using(var g=Graphics.FromImage(input)){g.Clear(Color.White);g.FillRectangle(Brushes.Blue,5,5,10,10);}
            using var idle=Ripple.Render(input,new PointF(20,10),0);
            using var wave=Ripple.Render(input,new PointF(20,10),.15f);
            bool changed=false;
            for(int y=0;y<20;y++)for(int x=0;x<40;x++){
                Check(input.GetPixel(x,y)==idle.GetPixel(x,y));Check(wave.GetPixel(x,y).A==255);
                changed|=wave.GetPixel(x,y)!=input.GetPixel(x,y);
            }
            Check(changed);
        });
        Test("MMI anchor scores, savings clamp and period exclusion match Mac",()=>{
            Check(Math.Abs(new Mmi(100,100,0,100,1).Score-.55)<.000001);
            Check(Math.Abs(new Mmi(100,100,0,25,1).Score-.15)<.000001);
            Check(new Mmi(0,0,0,0,1).Score==0 && new Mmi(0,0,0,1,1).Score==1);
            var e=new Entry("e","a",new(2026,9,1),-20,"EUR","Depot","ETF Sparplan","");Check(Mmi.Kind(e)=="savings");
            Check(Mmi.Kind(e with {Amount=100,Purpose="Umbuchung"})=="transfer");
            Check(Mmi.Kind(e with {Amount=4,Purpose=""})=="refund");
        });
        Test("Money age FIFO weights partial inflows and reports uncovered expenses",()=>{
            Entry E(int day,decimal amount)=>new(day.ToString(),"a",new(2026,9,day),amount,"EUR","","","");
            var age=MoneyAgeResult.Calculate([E(1,100),E(6,100),E(11,-150),E(12,-100)]);
            Check(age.Samples==2&&age.Uncovered==1&&Math.Abs(age.Days!.Value-(1250d/150+6)/2)<.00001);
            Check(MoneyAgeResult.Calculate([E(1,-20)]).Days==null);
        });
        Test("Rules fold left to right, normalize accents and require interval evidence",()=>{
            var e=new Entry("e","a",new(2026,9,1),-10,"EUR","Café","","Sonstiges");
            var r=new AssignmentRule("r",true,100,[new("Händler","ist","Cafe"),new("Betrag",">","20"),new("Betrag","=","10",true)],"Gastronomie");
            Check(AssignmentRules.Matches(r,e));Check(!AssignmentRules.Matches(r with {Conditions=[new("Intervall","ist","Monatlich")]},e));
            Check(AssignmentRules.Matches(r with {Conditions=[new("Intervall","ist","Monatlich")]},e,PaymentFrequency.Monthly));
        });
        Test("Subscriptions reject one-offs and separate account savings assignments",()=>{
            Entry E(string id,int month)=>new(id,"a",new(2026,month,5),-14.99m,"EUR","Netflix","Abo","");
            Check(Subscriptions.Detect([E("1",7)]).Count==0);
            var rows=new[]{E("1",7),E("2",8),E("3",9)};var sub=Subscriptions.Detect(rows).Single();Check(sub.Confidence>=10&&sub.Tab=="Abos"&&sub.Monthly==14.99m);
            Check(Subscriptions.Detect(rows,assignments:new Dictionary<string,string>{{"a/"+sub.Id,"Sparen"}}).Single().Tab=="Sparen");
            Check(Subscriptions.Detect(rows,assignments:new Dictionary<string,string>{{"b/"+sub.Id,"Sparen"}}).Single().Tab=="Abos");
        });
        Test("CAMT imports booked amount and rejects other currencies without partial writes",()=>{
            var a=new Account("a","Test","DE89370400440532013000","EUR",0);
            string xml="<Document xmlns='urn:test'><BkToCstmrStmt><Stmt><Acct><Id><IBAN>DE89370400440532013000</IBAN></Id><Ccy>EUR</Ccy></Acct><Ntry><Amt Ccy='EUR'>12.34</Amt><CdtDbtInd>DBIT</CdtDbtInd><BookgDt><Dt>2026-09-01</Dt></BookgDt><NtryDtls><TxDtls><Refs><EndToEndId>x</EndToEndId></Refs><RltdPties><Cdtr><Nm>Test Shop</Nm></Cdtr></RltdPties><RmtInf><Ustrd>Einkauf</Ustrd></RmtInf></TxDtls></NtryDtls></Ntry></Stmt></BkToCstmrStmt></Document>";
            var rows=StatementImport.Camt(xml,a);Check(rows.Count==1&&rows[0].Amount==-12.34m&&rows[0].Merchant=="Test Shop"&&rows[0].EndToEndId=="x");
            Check(StatementImport.NewEntries(rows,rows).Count==0);
            bool rejected=false;try{StatementImport.Camt(xml.Replace("Ccy='EUR'","Ccy='USD'"),a);}catch(InvalidDataException){rejected=true;}Check(rejected);
        });
        Test("OFX supports SGML fields and preserves identical repeated transactions",()=>{
            var a=new Account("a","Test","","EUR",0);string ofx="<OFX><CURDEF>EUR<STMTTRN><DTPOSTED>20260901<TRNAMT>-10.00<NAME>Shop<MEMO>Test<FITID>id1</STMTTRN><STMTTRN><DTPOSTED>20260901<TRNAMT>-10.00<NAME>Shop<MEMO>Test<FITID>id2</STMTTRN></OFX>";
            var rows=StatementImport.Ofx(ofx,a);Check(rows.Count==2&&rows[0].Id!=rows[1].Id);Check(StatementImport.NewEntries(rows.Take(1),rows).Count==1);
        });
        Entry Expense(string id,int month,int day=5,decimal amount=-100,string merchant="Vermieter")=>new(id,"a",new DateOnly(2026,month,day),amount,"EUR",merchant,"","Sonstiges");
        Test("Recurring evidence excludes one-offs and same-month duplicates",()=>{
            Check(RecurringCosts.Analyze([Expense("a",7,merchant:"Netflix")]).Count==0);
            Check(RecurringCosts.Analyze([Expense("a",7,5),Expense("b",7,20)]).Count==0);
            var monthly=RecurringCosts.Analyze([Expense("a",7),Expense("b",8)]).Single();
            Check(monthly.Frequency==PaymentFrequency.Monthly && monthly.AverageAmount==100 && Math.Abs(monthly.Confidence-.7)<.00001);
            Check(RecurringCosts.Analyze([Expense("a",7,amount:-65),Expense("b",8,amount:-135)]).Count==0);
        });
        Test("Standing order evidence is bank text only and one observation cannot alter due sums",()=>{
            var entry=Expense("a",7) with {Purpose="Dauerauftrag kündigen"};Check(!RecurringCosts.StandingOrder(entry));
            entry=entry with {AdditionalInformation="DAUERAUFTRAG"};var payment=RecurringCosts.Analyze([entry]).Single();
            Check(payment.Confidence==.55 && RecurringCosts.LeftToPay([payment],new(2026,8,1),new(2026,9,1))==0);
        });
        Test("Recurring groups retain IBAN distinction and recognize quarterly frequency",()=>{
            var first=Expense("a",3) with {CounterpartyIban="DE000000000000000001"};var second=first with {Id="b",Date=new(2026,6,5)};
            Check(RecurringCosts.Analyze([first,second]).Single().Frequency==PaymentFrequency.Quarterly);
            Check(RecurringCosts.Analyze([first,second with {CounterpartyIban="DE000000000000000002"}]).Count==0);
        });
        Test("Bank fees need own-bank evidence plus equal amounts and regular intervals",()=>{
            var fee=Expense("a",7,31,-10.99m,"Unbekannt") with {BankCounterpartyName="",Purpose="Entgeltabrechnung siehe Anlage"};
            Check(RecurringCosts.Analyze([fee,fee with {Id="b",Date=new(2026,8,31)}]).Single().Category=="bankFees");
            Check(!RecurringCosts.BankFee(fee with {BankCounterpartyName="Stadtwerke"},"Sparkasse"));
            Check(!RecurringCosts.BankFee(fee with {Purpose="Mahngebühr Entgelt"},null));
            Check(RecurringCosts.Analyze([fee,fee with {Id="b",Date=new(2026,8,2)}]).Count==0);
        });
        Test("Salary cycles clamp month ends and follow actual salary, not tax refunds",()=>{
            Check(RecurringCosts.Cycle(31,new(2026,2,20))==(new DateOnly(2026,1,31),new DateOnly(2026,2,28)));
            Check(RecurringCosts.Cycle(31,new(2024,2,29))==(new DateOnly(2024,2,29),new DateOnly(2024,3,31)));
            Check(RecurringCosts.Cycle(1,new(2026,8,29),new(2026,8,28))==(new DateOnly(2026,8,28),new DateOnly(2026,9,28)));
            Check(!RecurringCosts.SalaryCredit(Expense("a",8) with {Purpose="LOHNSTEUER-ERSTATTUNG"}));
            Check(RecurringCosts.SalaryCredit(Expense("a",8) with {Purpose="GEHALTSZAHLUNG"}));
        });
        Test("Outstanding amounts exclude already booked costs and retain overdue costs",()=>{
            var old=new RecurringPayment("Miete","Miete",850,3,3,PaymentFrequency.Monthly,new(2026,7,5),"other",.8,[]);
            var paid=old with {LastDate=new(2026,8,5)};
            Check(RecurringCosts.LeftToPay([old,paid],new(2026,8,1),new(2026,9,1))==850);
            Check(RecurringSummary.Unknown.Subtitle(2,1000,"EUR",new(2026,8,1))=="");
            var summary=new RecurringSummary(100,null,new(2026,8,11),[]);
            Check(summary.Subtitle(2,1100,"EUR",new(2026,8,1))=="100 €/Tag verfügbar");
            Check(summary.Subtitle(1,50,"EUR",new(2026,8,1))=="50 € überzogen bis 11.08.");
        });
        Test("Fixed-cost filter uses account-specific detected merchants",()=>{
            var first=Expense("same",7);var other=first with {AccountId="b"};
            var result=BookingFilters.Select([first,other],null,true,BookingFilter.FixedCosts,"",new HashSet<(string,string)>{("a","Vermieter")});
            Check(result.SequenceEqual([first]));
            Check(BookingFilters.Select([first],"a",false,BookingFilter.FixedCosts,"").Count==0);
        });
        Test("Bank details preserve value date and the correct counterparty", () => {
            var input = System.Text.Json.Nodes.JsonNode.Parse("""[{"bookingDate":"2026-09-01","valueDate":"2026-09-02","status":"pending","amount":{"amount":"-19.99","currency":"EUR"},"creditor":{"name":"Shop","iban":"creditor-iban","bic":"creditor-bic"},"debtor":{"name":"Sender","iban":"debtor-iban"},"additionalInformation":"Kartenzahlung","purposeCode":"CARD"}]""")!.AsArray();
            var entry = BankMapping.Entries(input,DemoData.Create().Accounts[0]).Single();
            Check(entry.ValueDate == new DateOnly(2026,9,2) && entry.Pending && entry.CounterpartyIban == "creditor-iban" && entry.CounterpartyBic == "creditor-bic");
            Check(entry.AdditionalInformation == "Kartenzahlung" && entry.PurposeCode == "CARD");
            input[0]!["amount"]!["amount"] = "19.99";
            Check(BankMapping.Entries(input,DemoData.Create().Accounts[0]).Single().CounterpartyIban == "debtor-iban");
            var restored = JsonSerializer.Deserialize<Entry>(JsonSerializer.Serialize(entry));Check(restored == entry);
        });
        Test("Account filters isolate colliding booking IDs", () => {
            var first = DemoData.Create().Entries[0]; var other = first with { AccountId = "other", Merchant = "Other merchant" };
            Check(BookingFilters.Select([first,other],first.AccountId,false,BookingFilter.All,"").SequenceEqual([first]));
            Check(BookingFilters.Select([first,other],null,true,BookingFilter.All,"Other merchant").SequenceEqual([other]));
            Check(BookingFilters.Select([first,other],null,false,BookingFilter.All,"").Count == 0);
        });
        Test("Original merchant aliases respect word boundaries and intermediaries", () => {
            Check(MerchantArtwork.Resolve("REWE Markt","") == "rewe");
            Check(MerchantArtwork.Resolve("PayPal","REWE Einkauf") == "rewe");
            Check(MerchantArtwork.Resolve("Mobilitaet","") == null);
            Check(MerchantArtwork.Resolve("Lotto24","") != "otto");
        });
        Test("Mac search and filter layers are mutually exclusive", () => {
            var empty = new HeaderLayers();
            Check(empty.ToggleSearch() == new HeaderLayers(true,false));
            Check(empty.ToggleFilter() == new HeaderLayers(false,true));
            Check(new HeaderLayers(true,false).ToggleFilter() == new HeaderLayers(false,true));
            Check(new HeaderLayers(false,true).ToggleSearch() == new HeaderLayers(true,false));
            Check(new HeaderLayers(true,false).ToggleSearch() == empty);
            Check(new HeaderLayers(false,true).ToggleFilter() == empty);
        });
        Test("Combined filters intersect and preserve account scope",()=>{
            var basis=DemoData.Create().Entries[0] with {AccountId="a",Amount=-10,Pending=true};
            var entries=new[]{basis,basis with{AccountId="b"},basis with{Pending=false},basis with{Amount=20}};
            var selected=BookingFilters.Select(entries,"a",false,new[]{BookingFilter.Pending,BookingFilter.Expense},"");
            Check(selected.Count==1&&selected[0]==basis);
            Check(BookingFilters.Select(entries,"a",false,Array.Empty<BookingFilter>(),"").Count==3);
        });
        Test("Booking filters exclude zero from income and expense", () => {
            var e = DemoData.Create().Entries[0] with { Amount = 0, Pending = true, Category = "Sonstiges" };
            Check(!BookingFilters.Matches(e,BookingFilter.Income) && !BookingFilters.Matches(e,BookingFilter.Expense));
            Check(BookingFilters.Matches(e,BookingFilter.Pending) && BookingFilters.Matches(e,BookingFilter.Uncategorized));
            Check(BookingFilters.Matches(e with { Category="Unkategorisiert" },BookingFilter.Uncategorized));
            Check(BookingFilters.Matches(e with { Amount = 1 },BookingFilter.Income));
            Check(BookingFilters.Matches(e with { Amount = -1 },BookingFilter.Expense));
        });
        Test("Mac money mood boundary fixtures", () => {
            var prefs = new AppPreferences();
            foreach (var (value, level) in new (decimal?, int)[] { (null,6), (-1000.01m,0), (-1000m,1), (-0.01m,1), (0m,2), (499.99m,2), (500m,3), (2000m,3), (2000.01m,4), (5000m,4), (5000.01m,5) })
                Check(BalancePresentation.Create(value, prefs).Level == level);
            prefs.Low = -100; prefs.Medium = -200; prefs.VeryGood = -300;
            Check(BalancePresentation.Create(0, prefs).Level == 3);
        });
        Test("Aggregate rejects unknown balances and mixed currencies", () => {
            var a = new Account("a","a","", "EUR", 10);
            Check(BalancePresentation.Aggregate([]) == null);
            Check(BalancePresentation.Aggregate([a, a with { Id = "b", Balance = 20 }]) == 30);
            Check(BalancePresentation.Aggregate([a, a with { Currency = "USD" }]) == null);
            Check(BalancePresentation.Aggregate([a, a with { BalanceKnown = false }]) == null);
        });
        Test("Embedded keys validate when supplied; source-only builds fail closed", () =>
        {
            var embedded = typeof(EmbeddedYaxiKeys).Assembly.GetManifestResourceNames().Contains("SimpleBanking.YaxiKeys.json");
            if (args.Contains("--require-embedded-keys")) Check(embedded);
            if (embedded)
            {
                var k = EmbeddedYaxiKeys.Load();
                Check(k.KeyId.Length > 0 && Convert.FromBase64String(k.SecretBase64).Length > 0);
            }
            else
            {
                try { EmbeddedYaxiKeys.Load(); throw new Exception("Missing keys accepted"); }
                catch (InvalidOperationException) { }
            }
        });
        Test("HS256 matches independent Python golden fixture", () => {
            var keys = new YaxiKeys("test-standard", Convert.ToBase64String(Enumerable.Repeat((byte)1, 32).ToArray()));
            var ticket = LocalTicketMaker.Issue(keys, "Accounts", null, false, DateTimeOffset.FromUnixTimeSeconds(1700000000), Guid.Parse("00000000-0000-0000-0000-000000000001"));
            Check(ticket.Split('.')[2] == "wMPzdRBKc4FlswpFQGPXv-xqghFmdN4mO9_CNkwv0Ys");
        });
        Test("Local ticket lifetime and unique IDs match Mac contract", () => {
            var keys = new YaxiKeys("test-standard", Convert.ToBase64String(Enumerable.Repeat((byte)1, 32).ToArray()));
            var a = TicketClient.DecodeJwt(LocalTicketMaker.Issue(keys, "Accounts", null, false));
            var b = TicketClient.DecodeJwt(LocalTicketMaker.Issue(keys, "Accounts", null, false));
            Check(a["exp"]!.GetValue<long>() - a["iat"]!.GetValue<long>() == 600);
            Check(a["data"]!["id"]!.GetValue<string>() != b["data"]!["id"]!.GetValue<string>());
            Check(a["data"]!["service"]!.GetValue<string>() == "Accounts" && a["data"]!["data"] == null);
        });
        Test("Only activated simplesend Transfer changes key pair", () => {
            var keys = new YaxiKeys("test-standard", Convert.ToBase64String(new byte[32]), "test-transfer", Convert.ToBase64String(Enumerable.Repeat((byte)2, 32).ToArray()));
            static string Kid(string jwt) { var b = jwt.Split('.')[0].Replace('-', '+').Replace('_', '/'); b += new string('=', (4-b.Length%4)%4); return System.Text.Json.Nodes.JsonNode.Parse(Convert.FromBase64String(b))!["kid"]!.GetValue<string>(); }
            Check(Kid(LocalTicketMaker.Issue(keys, "Transfer", null, false)) == "test-standard");
            Check(Kid(LocalTicketMaker.Issue(keys, "Transfer", null, true)) == "test-transfer");
            Check(Kid(LocalTicketMaker.Issue(keys, "Balances", null, true)) == "test-standard");
            try{LocalTicketMaker.Issue(keys with { TransferKeyId = null, TransferSecretBase64 = null }, "Transfer", null, true);throw new Exception("Missing transfer keys accepted");}catch(InvalidOperationException){}
        });
        Test("Transactions ticket preserves account currency and calendar dates", () => {
            var keys = new YaxiKeys("test-standard", Convert.ToBase64String(new byte[32]));
            var data = System.Text.Json.Nodes.JsonNode.Parse("{\"account\":{\"iban\":\"DE89370400440532013000\",\"currency\":\"EUR\"},\"range\":{\"from\":\"2026-03-29\",\"to\":\"2026-10-25\"}}");
            var actual = TicketClient.DecodeJwt(LocalTicketMaker.Issue(keys, "Transactions", data, false));
            Check(System.Text.Json.Nodes.JsonNode.DeepEquals(data, actual["data"]!["data"]));
        });
        Test("Native Yaxi ABI and repeated handle lifetime", () => {
            for (int i = 0; i < 10; i++) { using var client = new YaxiClient(); var r = client.Invoke(new System.Text.Json.Nodes.JsonObject { ["op"] = "version" }); Check(r["sdk"]?.GetValue<string>() == "0.2.0"); }
        });
        Test("Native errors do not cross ABI as exceptions", () => { using var client = new YaxiClient(); var r = client.Invoke(new System.Text.Json.Nodes.JsonObject { ["op"] = "not-an-operation" }); Check(r["type"]?.GetValue<string>() == "BridgeError"); });
        Test("Balance cannot fall back to a different account", () => {
            var p = System.Text.Json.Nodes.JsonNode.Parse("{\"balances\":[{\"account\":{\"iban\":\"DE89370400440532013000\"},\"balances\":[{\"amount\":\"12.34\",\"currency\":\"EUR\",\"balanceType\":\"Booked\"}]}]}")!;
            var a = DemoData.Create().Accounts[0]; Check(BankMapping.Balance(p, a) == 12.34m); Check(BankMapping.Balance(p, a with { Iban = "OTHER" }) == null); Check(BankMapping.Balance(p, a with { Currency = "USD" }) == null);
        });
        Test("Identical bookings retain multiplicity and decimal amounts", () => {
            var tx = System.Text.Json.Nodes.JsonNode.Parse("{\"bookingDate\":\"2026-09-01\",\"status\":\"Booked\",\"amount\":{\"amount\":\"-19.99\",\"currency\":\"EUR\"},\"creditor\":{\"name\":\"Shop\"},\"remittanceInformation\":[\"Purchase\"]}")!;
            var array = new System.Text.Json.Nodes.JsonArray(tx.DeepClone(), tx.DeepClone()); var list = BankMapping.Entries(array, DemoData.Create().Accounts[0]);
            Check(list.Count == 2 && list[0].Id != list[1].Id && list.Sum(e => e.Amount) == -39.98m);
            Check(list.Select(e => e.Id).SequenceEqual(BankMapping.Entries(array, DemoData.Create().Accounts[0]).Select(e => e.Id)));
        });
        Test("IBAN checksum and whitespace", () => { Check(BankingRules.ValidIban("DE89 3704 0044 0532 0130 00")); Check(!BankingRules.ValidIban("DE89370400440532013001")); });
        Test("Word boundaries prevent OBI/mobility collision", () => { Check(!BankingRules.WordMatch("mobility", "obi")); Check(BankingRules.WordMatch("obi markt", "obi")); Check(BankingRules.WordMatch("versicherungsbeitrag", "versicherung", true)); });
        Test("GiroCode zero amount and free reference", () => { var t = BankingRules.ParseGiroCode("BCD\n002\n1\nSCT\n\nTest\nDE89370400440532013000\nEUR0.0\n\n\nRechnung", "a"); Check(t != null && t.Amount == 0 && t.Purpose == "Rechnung"); });
        Test("Transfer validation rejects negative and fractional cents", () => { var t = new TransferDraft("a", "Test", "DE89370400440532013000", 1m, ""); Check(BankingRules.Validate(t) == null); Check(BankingRules.Validate(t with { Amount = -1m }) != null); Check(BankingRules.Validate(t with { Amount = 1.001m }) != null); });
        Test("Currencies never summed together", () => { var d = DemoData.Create(); d.Entries.Add(d.Entries[0] with { Currency = "USD", Amount = 12.34m }); Check(BankingRules.Totals(d.Entries)["USD"] == 12.34m); });
        Test("Encrypted backup roundtrip", () => { var d = DemoData.Create(); var restored = EncryptedStore.Decrypt(EncryptedStore.Encrypt(d, "test passphrase"), "test passphrase"); Check(restored.Entries.Count == d.Entries.Count); Check(restored.Accounts[0].Balance == 3284.52m); });
        Test("Wrong password rejected", () => { var bytes = EncryptedStore.Encrypt(DemoData.Create(), "correct"); try { EncryptedStore.Decrypt(bytes, "wrong"); } catch (CryptographicException) { return; } throw new Exception("Accepted wrong password"); });
        Test("Tampering rejected", () => { var bytes = EncryptedStore.Encrypt(DemoData.Create(), "correct"); var node = System.Text.Json.Nodes.JsonNode.Parse(bytes)!; var cipher = Convert.FromBase64String(node["Ciphertext"]!.GetValue<string>()); cipher[0] ^= 1; node["Ciphertext"] = Convert.ToBase64String(cipher); try { EncryptedStore.Decrypt(JsonSerializer.SerializeToUtf8Bytes(node), "correct"); } catch (CryptographicException) { return; } throw new Exception("Accepted modified ciphertext"); });
        Test("Cross-account currency inconsistency rejected", () => { var d = DemoData.Create(); d.Entries[0] = d.Entries[0] with { Currency = "USD" }; try { EncryptedStore.Decrypt(EncryptedStore.Encrypt(d, "test"), "test"); } catch (InvalidDataException) { return; } throw new Exception("Accepted inconsistent backup"); });
        PriorityTwoTests.Register(Test);
        DesktopRegressionTests.Register(Test);
        TodoTests.Register(Test);
        var report = JsonSerializer.Serialize(new { passed = results.Count - failed, failed, tests = results }, new JsonSerializerOptions { WriteIndented = true });
        var index = Array.IndexOf(args, "--report");
        if (index >= 0 && index + 1 < args.Length) File.WriteAllText(args[index + 1], report);
        Console.WriteLine(report); return failed == 0 ? 0 : 1;
    }
}
