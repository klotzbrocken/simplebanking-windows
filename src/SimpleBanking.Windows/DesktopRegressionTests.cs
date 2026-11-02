using System.Diagnostics;
using System.Text.Json;
namespace SimpleBanking;

internal static class DesktopRegressionTests
{
    static void Check(bool value){if(!value)throw new InvalidOperationException("Desktop placement regression.");}
    internal static void Register(Action<string,Action> test){
        test("Compact vault preserves optional defaults and every populated entry field",()=>{
            var data=new BankData();data.Accounts.Add(new("one","Test","DE89370400440532013000","EUR",0,BalanceKnown:false));
            var entry=new Entry("one","one",new(2026,10,1),-1,"EUR","Test","Zweck","Kategorie");data.Entries.Add(entry);
            data.Entries.Add(entry with{Id="two",Pending=true,Read=true,Flagged=true,Note="Notiz",ValueDate=new(2026,10,2),CounterpartyIban="DE89370400440532013000",CounterpartyBic="TESTDEFF",AdditionalInformation="Zusatz",PurposeCode="SALA",BankCounterpartyName="Bankname",EndToEndId="E2E",CategoryManual=true,ReminderAt=new(2026,10,3),ReminderDone=true,OriginalMerchant="Original",MerchantOverride="Eigener Name",BankTransactionCode="PMNT",MerchantSource="manual",MerchantConfidence=.8});
            var restored=EncryptedStore.Decrypt(EncryptedStore.Encrypt(data,"test"),"test");
            Check(restored.Entries.SequenceEqual(data.Entries)&&!restored.Accounts[0].BalanceKnown);
        });
        test("Window placement handles negative monitors and disconnected displays",()=>{
            var area=new Rectangle(-1920,-200,1920,1040);
            Check(WindowPlacement.Clamp(new(-3000,-500,348,178),area)==new Point(-1920,-200));
            Check(WindowPlacement.Clamp(new(-100,800,348,178),area)==new Point(-348,662));
            Check(WindowPlacement.Clamp(new(5000,3000,348,178),new(0,0,1920,1040))==new Point(1572,862));
            Check(WindowPlacement.Clamp(new(0,0,2000,1500),area)==area.Location);
        });
        test("Tray placement respects all four taskbar edges at multiple scales",()=>{
            foreach(int dpi in new[]{96,120,144,192}){
                var bounds=new Rectangle(-1920,0,1920,1080);var size=new Size(348*dpi/96,178*dpi/96);
                foreach(var area in new[]{new Rectangle(-1920,0,1920,1040),new(-1920,40,1920,1040),new(-1880,0,1880,1080),new(-1920,0,1880,1080)}){
                    var position=WindowPlacement.Tray(new(-10,1070),size,bounds,area,8*dpi/96);
                    Check(area.Contains(new Rectangle(position,size)));
                }
            }
        });
        test("Fixed window presets fit small work areas without losing the footer",()=>{
            foreach(int dpi in new[]{96,120,144,192})foreach(bool wide in new[]{false,true}){
                var area=new Rectangle(0,40,1280,680);var frame=new Size(16,39);
                var size=WindowPlacement.MainClient(wide,dpi,frame,area);
                Check(size.Width+frame.Width<=area.Width&&size.Height+frame.Height<=area.Height);
            }
            Check(WindowPlacement.MainClient(true,96,new(16,39),new(0,0,1920,1040))==new Size(840,688));
        });
    }

    internal static int Benchmark(string reportPath){
        string root=Path.Combine(Path.GetTempPath(),"simplebanking-benchmark-"+Guid.NewGuid().ToString("N"));
        try{
            var data=new BankData();
            for(int account=0;account<10;account++)data.Accounts.Add(new("synthetic-"+account,"Synthetisches Konto","DE89370400440532013000","EUR",1000));
            for(int i=0;i<100000;i++)data.Entries.Add(new("synthetic-entry-"+i,"synthetic-"+(i%10),new DateOnly(2026,10,1).AddDays(-i/100),-(i%10000)/100m,"EUR","Synthetischer Händler","Synthetische Buchung "+i,"Essen & Alltag",Note:i%100==0?"Testnotiz":"",CategoryManual:i%100==0));
            var store=new EncryptedStore(root);var timer=Stopwatch.StartNew();store.Save(data,"synthetic-benchmark-password");long saveMs=timer.ElapsedMilliseconds;
            timer.Restart();var restored=store.Load("synthetic-benchmark-password");long loadMs=timer.ElapsedMilliseconds;
            if(restored.Entries.Count!=data.Entries.Count||!restored.Entries.SequenceEqual(data.Entries))throw new InvalidDataException("Large vault round trip lost transaction data.");
            restored.Entries[0]=restored.Entries[0] with{Note="Geändert"};timer.Restart();store.Save(restored,"synthetic-benchmark-password");long replaceMs=timer.ElapsedMilliseconds;
            var previous=EncryptedStore.Decrypt(File.ReadAllBytes(store.FilePath+".previous"),"synthetic-benchmark-password");
            if(previous.Entries[0].Note!="Testnotiz"||store.Load("synthetic-benchmark-password").Entries[0].Note!="Geändert")throw new InvalidDataException("Previous vault generation was not preserved.");
            File.WriteAllText(reportPath,JsonSerializer.Serialize(new{entries=restored.Entries.Count,accounts=restored.Accounts.Count,saveMs,loadMs,replaceMs,bytes=new FileInfo(store.FilePath).Length,peakWorkingSetBytes=Process.GetCurrentProcess().PeakWorkingSet64,roundTripPassed=true,previousGenerationPassed=true},new JsonSerializerOptions{WriteIndented=true}));
            return 0;
        }finally{if(Directory.Exists(root))Directory.Delete(root,true);}
    }
}
