using System.Text.Json;
using System.Text.Json.Nodes;
namespace SimpleBanking;

internal static class MacUpdateTests
{
    static void Check(bool condition,[System.Runtime.CompilerServices.CallerLineNumber]int line=0) { if(!condition)throw new Exception("Mac update regression failed at line "+line); }
    internal static void Register(Action<string,Action> test) {
        test("Bank group takes precedence over geographical BLZ prefix",()=>{
            Account A(string blz)=>new("a","Konto","DE00"+blz+"0000000000","EUR",0);
            Check(BankArtwork.Resolve(A("20050550"))=="sparkasse");
            Check(BankArtwork.Resolve(A("20190003"))=="volk");
            Check(BankArtwork.Resolve(A("12050000"))=="sparkasse");
            Check(BankArtwork.Resolve(A("20041111"))=="comdirect");
        });
        test("Expired recurring costs leave the cycle; current-cycle overdue remain",()=>{
            var start=new DateOnly(2026,10,1);var end=start.AddMonths(1);
            var payment=new RecurringPayment("Rent","Rent",850,3,3,PaymentFrequency.Monthly,new(2026,8,5),"other",.9,[]);
            Check(RecurringCosts.LeftToPay([payment],start,end)==0);
            Check(RecurringCosts.LeftToPay([payment with{LastDate=new(2026,9,5)}],start,end)==850);
            Check(RecurringCosts.LeftToPay([payment with{LastDate=new(2026,10,5)}],start,end)==0);
            Check(RecurringCosts.LeftToPay([payment with{LastDate=new(2026,7,1),Frequency=PaymentFrequency.Quarterly}],start,end)==850);
        });
        test("Payment processors do not become recurring merchants; resolved shops still do",()=>{
            Entry E(string name,int month)=>new(month.ToString(),"a",new(2026,month,5),-15,"EUR",name,"","other");
            foreach(var name in new[]{"PayPal Europe","PayPal (Intermediaer)","Klarna Bank AB","Stripe","Stichting Mollie","Amazon Payments","Apple Pay","Google Pay","Kartenzahlung (Intermediaer)"}) {
                Check(RecurringCosts.Analyze([E(name,7),E(name,8),E(name,9)]).Count==0);
                Check(Subscriptions.Detect([E(name,7),E(name,8),E(name,9)]).Count==0);
            }
            Check(!RecurringCosts.IsPaymentProcessor("Paypalooza")&&!RecurringCosts.IsPaymentProcessor("Apple"));
            Check(RecurringCosts.CanonicalMerchant("Amazon Payments")=="Amazon Payments");
            Check(RecurringCosts.Analyze([E("PayPal",7) with{Purpose="PAYPAL *NETFLIX"},E("PayPal",8) with{Purpose="PAYPAL *NETFLIX"}]).Count==1);
        });
        test("Two observations require similar amounts; pending rows are not evidence",()=>{
            var one=new Entry("1","a",new(2026,8,5),-350,"EUR","Local shop","","other");
            var two=one with{Id="2",Date=new(2026,9,5),Amount=-609.59m};
            Check(RecurringCosts.Analyze([one,two]).Count==0);
            Check(RecurringCosts.Analyze([one,two with{Amount=-350}]).Count==1);
            Check(RecurringCosts.Analyze([one,two with{Amount=-350,Pending=true}]).Count==0);
            Check(RecurringCosts.Analyze([one with{Merchant="Netflix",Amount=-6.99m},two with{Merchant="Netflix",Amount=-7.99m}]).Count==1);
        });
        test("Recurring assignments cross engines without merging unnamed counterparties",()=>{
            var assignments=new Dictionary<string,string>{{"a/Telekom|12345678","Ausschließen"},{"a/Unbekannt|11111111","Verbindlichkeiten"}};
            Check(RecurringAssignmentKeys.Find(assignments,"a","Telekom|agg")=="Ausschließen");
            Check(RecurringAssignmentKeys.Find(assignments,"b","Telekom|12345678")==null);
            Check(RecurringAssignmentKeys.Find(assignments,"a","unbekannt|11111111")=="Verbindlichkeiten");
            Check(RecurringAssignmentKeys.Find(assignments,"a","Unbekannt|22222222")==null);
            Check(RecurringAssignmentKeys.Canonical(" (Unbekannt) | 222 ")=="(unbekannt)|222");
            RecurringAssignmentKeys.Set(assignments,"a","Telekom|agg","Verträge");
            Check(RecurringAssignmentKeys.Find(assignments,"a","Telekom|12345678")=="Verträge");
            RecurringAssignmentKeys.Set(assignments,"a","Unbekannt|22222222","Sparen");
            Check(RecurringAssignmentKeys.Find(assignments,"a","Unbekannt|11111111")=="Verbindlichkeiten");
        });
        test("Background reads never fall back to credentials or start approval",()=>{
            var access=new BankReadAccess();var connection=new BankConnection("a","provider","Fixture bank","user","password",[1]);int calls=0,tickets=0;
            Task<string> Ticket(){tickets++;return Task.FromResult("fixture");}
            Task<JsonObject> Expired(JsonObject request){calls++;throw new YaxiException("Unauthorized","expired");}
            try { access.RunAsync(connection,"Balances",new(),Ticket,Expired,CancellationToken.None,false).GetAwaiter().GetResult();throw new Exception("Background fallback accepted"); }catch(BankApprovalRequiredException){}
            Check(calls==1&&tickets==1);calls=tickets=0;
            try { access.RunAsync(connection with{ConnectionData=null},"Transactions",new(),Ticket,Expired,CancellationToken.None,false).GetAwaiter().GetResult();throw new Exception("Background credentials accepted"); }catch(BankApprovalRequiredException){}
            Check(calls==0&&tickets==0);
            try { BankReadAccess.RequireInteractive(false);throw new Exception("Background prompt accepted"); }catch(BankApprovalRequiredException){}
            BankReadAccess.RequireInteractive(true);
            var restored=JsonSerializer.Deserialize<BankConnection>(JsonSerializer.Serialize(connection with{ApprovalRequired=true}));Check(restored!.ApprovalRequired);
        });
        test("Automation fixed costs use display cycle and preserve account/currency scope",()=>{
            var today=DateOnly.FromDateTime(DateTime.Today);var data=new BankData();var account=new Account("a","A","","EUR",1000);data.Accounts.AddRange([account,new("b","B","","USD",100)]);
            for(int n=1;n<=2;n++)data.Entries.Add(new(n.ToString(),"a",new DateOnly(today.Year,today.Month,1).AddMonths(-n).AddDays(4),-14.99m,"EUR","Netflix","subscription","other"));
            var prefs=new AppPreferences();prefs.FinanceFor("a").SalaryPreset=0;
            var expected=RecurringSummary.Calculate(data.Entries,account,prefs.FinanceFor("a"),today);
            var json=JsonSerializer.SerializeToNode(LocalAutomation.Query(data,"get_fixed_costs",new(){["account_id"]="a"},prefs))!.AsArray();
            Check(json.Count==1&&json[0]!["currency"]!.GetValue<string>()=="EUR");
            Check(json[0]!["outstanding"]!.GetValue<decimal>()==expected.Outstanding);
            var items=json[0]!["items"]!.AsArray();Check(items.Sum(p=>p!["amount"]!.GetValue<decimal>())==expected.Outstanding);
            data.RecurringAssignments["a/netflix"]="Ausschließen";
            json=JsonSerializer.SerializeToNode(LocalAutomation.Query(data,"get_subscriptions",new(){["account_id"]="a"},prefs))!.AsArray();Check(json[0]!["items"]!.AsArray().Count==0);
            data.Accounts.Remove(account);json=JsonSerializer.SerializeToNode(LocalAutomation.Query(data,"get_fixed_costs",new(){["account_id"]="a"},prefs))!.AsArray();Check(json.Count==0);
        });
        test("Backup retains distinct duplicate enrichment, credentials, logos and attachments",()=>{
            var data=DemoData.Create();var row=data.Entries[0];data.Entries=[row with{Id="fp",Note="first"},row with{Id="fp#2",Note="second",Attachments=[new("x","fixture.pdf",[1,2,3])]}];
            data.Connections.Add(new("connection","provider","bank","fixture-user","fixture-password",[4],ApprovalRequired:true));data.MerchantLogos["fixture"]=[1,2];
            var file=Path.Combine(Path.GetTempPath(),"sb-backup-test-"+Guid.NewGuid().ToString("N")+".sbw");
            try { EncryptedStore.WriteBackup(file,data,"fixture-password");var restored=EncryptedStore.Decrypt(File.ReadAllBytes(file),"fixture-password");Check(restored.Entries[1].Id=="fp#2"&&restored.Entries[1].Note=="second"&&restored.Entries[0].Note=="first");Check(restored.Entries[1].Attachments![0].Content.SequenceEqual(new byte[]{1,2,3}));Check(restored.Connections[0].ApprovalRequired&&restored.MerchantLogos["fixture"].Length==2);
                var reconciled=BookingEnrichment.Reconcile(restored.Entries,[row with{Id="fp"},row with{Id="fp#2"}]);Check(reconciled[1].Note=="second"&&reconciled[0].Note=="first");
                var before=File.ReadAllBytes(file);
                using(var locked=new FileStream(file,FileMode.Open,FileAccess.Read,FileShare.Read)) {
                    try { EncryptedStore.WriteBackup(file,new BankData(),"replacement");throw new Exception("Locked backup replaced"); }catch(Exception e) when(e is IOException or UnauthorizedAccessException){}
                }
                Check(File.ReadAllBytes(file).SequenceEqual(before));
            }finally{if(File.Exists(file))File.Delete(file);}
        });
    }
}
