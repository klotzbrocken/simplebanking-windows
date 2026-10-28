using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
namespace SimpleBanking;
internal static class PriorityOneTests
{
    static void Check(bool condition,[System.Runtime.CompilerServices.CallerLineNumber]int line=0){if(!condition)throw new Exception("P1 regression at line "+line);}
    static Entry Entry(decimal amount=-20)=>new("test","a",new(2026,9,1),amount,"EUR","Example","Payment","Sonstiges");
    internal static void Register(Action<string,Action> test){
        test("BTX CRT preserves transparency, scan lines and edge shading",()=>{
            using var frame=BtxCrtOverlay.Frame(new(90,60),0);
            Check(frame.GetPixel(45,30).A<100&&frame.GetPixel(45,31).A<100);
            Check(frame.GetPixel(45,30).A>frame.GetPixel(45,31).A);
            Check(frame.GetPixel(0,0).A>frame.GetPixel(45,30).A);
        });
        test("Repeated theme changes preserve inherited font size and original text",()=>{
            using var root=new Panel();using var label=new Label{Text="Test"};root.Controls.Add(label);
            try{
                ThemeStyling.Use(new(){ThemeId="btx"});ThemeStyling.Apply(root);float size=label.Font.Size;Check(label.Text=="TEST");
                for(int i=0;i<3;i++){
                    using var child=new Label{Text="Dynamic"};root.Controls.Add(child);Check(Math.Abs(child.Font.Size-size)<.1);
                    ThemeStyling.Use(new(){ThemeId="default"});ThemeStyling.Apply(root);Check(label.Text=="Test");
                    ThemeStyling.Use(new(){ThemeId="btx"});ThemeStyling.Apply(root);Check(Math.Abs(label.Font.Size-size)<.1);
                }
            }finally{ThemeStyling.Use(new());}
        });
        test("Yaxi tagged bank codes survive parsing and classify standing orders and fees",()=>{
            var codes=JsonNode.Parse("""[{"iso":{"domain":"PMNT","family":"ICDT","subFamily":"STDO"}},{"swift":"CHG"},{"national":{"code":"52","country":"DE"}}]""");
            var value=BankBookingCode.Compact(codes);Check(value=="ISO:PMNT/ICDT/STDO;SWIFT:CHG;GVC:52");
            var payload=JsonNode.Parse("""[{"amount":{"amount":"-20","currency":"EUR"},"bookingDate":"2026-09-01","bankTransactionCodes":[]}]""")!.AsArray();payload[0]!["bankTransactionCodes"]=codes!.DeepClone();
            Check(BankMapping.Entries(payload,new("a","Bank","DE00","EUR",0)).Single().BankTransactionCode==value);
            Check(RecurringCosts.StandingOrder(Entry() with{BankTransactionCode=value}));
            Check(!RecurringCosts.StandingOrder(Entry() with{Purpose="Dauerauftrag kündigen"}));
            Check(!BankBookingCode.StandingOrder("GVC:520;NAT-AT:52"));
            Check(RecurringCosts.BankFee(Entry() with{BankCounterpartyName="",BankTransactionCode="SWIFT:CHG"},"Bank"));
            Check(!RecurringCosts.BankFee(Entry() with{BankCounterpartyName="Shop",BankTransactionCode="SWIFT:CHG"},"Bank"));
            Check(JsonSerializer.Deserialize<Entry>(JsonSerializer.Serialize(Entry() with{BankTransactionCode=value}))!.BankTransactionCode==value);
        });
        test("Mac category catalogue distinguishes whole brands and German word stems",()=>{
            Check(AutomaticCategories.Classify(Entry() with{Merchant="REWE"})=="Essen & Alltag");
            Check(AutomaticCategories.Classify(Entry() with{Merchant="Example",Purpose="Versicherungsbeitrag"})=="Versicherungen");
            Check(AutomaticCategories.Classify(Entry() with{Merchant="Hans Aldihausen",Purpose=""})!="Essen & Alltag");
            Check(AutomaticCategories.Classify(Entry(200))=="Einkommen");
        });
        test("Merchant rule suggestions match the original booking and retain provenance",()=>{
            var entry=Entry() with{Merchant="Dornseifer Frischemarkt",BankCounterpartyName="Dornseifers Frischeb.",OriginalMerchant="Dornseifers Frischeb."};
            var suggestion=MerchantRuleSuggestion.From(entry);var rule=new MerchantUserRule("r",true,1,suggestion.Scope,"Enthält",suggestion.Pattern,"Mein Markt");
            Check(UserMerchantRules.Matches(rule,entry));var result=UserMerchantRules.Apply(entry,[rule]);Check(result.Merchant=="Mein Markt"&&result.MerchantSource=="user_rule"&&result.MerchantConfidence==.99);
            var paypal=entry with{Merchant="PayPal",BankCounterpartyName="PayPal Europe",Purpose="PP.1234.PP . Ihr Einkauf bei Steam"};Check(MerchantRuleSuggestion.From(paypal).Scope=="Verwendungszweck");
            Check(MerchantResolver.ResolveDetailed(paypal).Source=="paypal_purpose");
            Check(MerchantResolver.ResolveDetailed(Entry() with{Merchant="Unbekannt",BankCounterpartyName=""}).Confidence==.1);
        });
        test("Money age exposes band and range while excluding pending cash flows",()=>{
            var first=Entry(100) with{Date=new(2026,1,1)};var last=Entry(-40) with{Date=new(2026,3,2)};
            var age=MoneyAgeResult.Calculate([first,last,Entry(-10000) with{Pending=true}]);Check(age.Days==60&&age.Minimum==60&&age.Maximum==60&&age.Total==1&&age.Band=="Du lebst einen Monat voraus");
            var mmi=Mmi.Calculate([first with{Date=new(2026,9,1)},Entry(-10),Entry(-999) with{Pending=true},Entry(-888) with{Date=new(2027,1,1)}],100,0,new(2026,9,12),[]);Check(mmi.Expenses==10);
        });
        test("SVG and PDF theme art render locally; external references are rejected",()=>{
            var svg=Encoding.UTF8.GetBytes("<svg xmlns='http://www.w3.org/2000/svg' width='20' height='12'><rect width='20' height='12' fill='red'/></svg>");
            using(var stream=new MemoryStream(ThemeArtwork.Normalize("mark.svg",svg)))using(var bitmap=new Bitmap(stream)){Check(bitmap.Width==20&&bitmap.Height==12&&bitmap.GetPixel(10,6).R>240);}
            bool rejected=false;try{ThemeArtwork.Normalize("bad.svg",Encoding.UTF8.GetBytes("<svg xmlns='http://www.w3.org/2000/svg' width='20' height='12'><image href='https://example.com/image'/></svg>"));}catch(InvalidDataException){rejected=true;}Check(rejected);
            using var pdf=new PdfSharp.Pdf.PdfDocument();var page=pdf.AddPage();page.Width=PdfSharp.Drawing.XUnit.FromPoint(100);page.Height=PdfSharp.Drawing.XUnit.FromPoint(80);using var bytes=new MemoryStream();pdf.Save(bytes,false);
            using var png=new MemoryStream(ThemeArtwork.Normalize("paper.pdf",bytes.ToArray()));using var rendered=new Bitmap(png);Check(rendered.Width>0&&rendered.Height>0);
        });
        test("Theme font assets and typography are usable without system installation",()=>{
            var bytes=File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory,"Assets","Fonts","VT323-Regular.ttf"));ThemeFonts.Validate(bytes);
            var palette=ThemePalette.For(new(){ThemeId="custom",CustomTheme=new(){["id"]="custom",["name"]="Font",["bodyFont"]="private.ttf"},CustomThemeImages=new(){["private.ttf"]=bytes}});
            using var font=palette.Font(16);Check(font.FontFamily.Name.Contains("VT323"));
            var btx=ThemePalette.For(new(){ThemeId="btx"});Check(btx.SquareControls&&!btx.GlyphControls&&btx.DottedLeaders);
        });
        test("Theme vector and private font assets survive an import-export round trip",()=>{
            var directory=Directory.CreateTempSubdirectory("simplebanking-theme-test-");
            try{
                string config=Path.Combine(directory.FullName,"theme.cfg");File.WriteAllText(config,"id=fixture\nname=Fixture\nlogo=mark.svg\nbodyFont=font.ttf\n");
                File.WriteAllText(Path.Combine(directory.FullName,"mark.svg"),"<svg xmlns='http://www.w3.org/2000/svg' width='10' height='10'><circle cx='5' cy='5' r='4' fill='blue'/></svg>");
                File.Copy(Path.Combine(AppContext.BaseDirectory,"Assets","Fonts","VT323-Regular.ttf"),Path.Combine(directory.FullName,"font.ttf"));
                var package=ThemePackage.Read(config);Check(package.Values["logo"].EndsWith(".png")&&package.Images.ContainsKey("font.ttf"));
                var zip=Path.Combine(directory.FullName,"theme.zip");package.Write(zip);var restored=ThemePackage.Read(zip);Check(restored.Images.Count==2);
                using var logo=ThemePalette.For(new(){ThemeId="custom",CustomTheme=restored.Values,CustomThemeImages=restored.Images}).Image("logo");Check(logo?.Width==10);
            }finally{foreach(var file in directory.EnumerateFiles())file.Delete();directory.Delete();}
        });
        test("CAMT bank codes and single standing-order confidence follow the Mac model",()=>{
            const string xml="<Document><Stmt><Ntry><Amt Ccy='EUR'>850</Amt><CdtDbtInd>DBIT</CdtDbtInd><BookgDt><Dt>2026-09-01</Dt></BookgDt><BkTxCd><Domn><Cd>PMNT</Cd><Fmly><Cd>ICDT</Cd><SubFmlyCd>STDO</SubFmlyCd></Fmly></Domn></BkTxCd></Ntry></Stmt></Document>";
            var rows=StatementImport.Camt(xml,new("a","Bank","DE00","EUR",0));Check(RecurringCosts.StandingOrder(rows.Single()));var payment=RecurringCosts.Analyze(rows).Single();Check(payment.Confidence>=.5&&payment.Confidence<.6);Check(RecurringCosts.LeftToPay([payment],new(2026,10,1),new(2026,11,1))==0);
        });
    }
}
