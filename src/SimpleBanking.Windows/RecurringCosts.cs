using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
namespace SimpleBanking;

internal enum PaymentFrequency { Monthly, Quarterly, Yearly, Irregular }
internal sealed record RecurringPayment(string Merchant,string GroupKey,decimal AverageAmount,int Occurrences,int Months,PaymentFrequency Frequency,DateOnly LastDate,string Category,double Confidence,IReadOnlyList<string> EntryIds);
internal static class RecurringCosts
{
    internal sealed record Rule(string Pattern,string Name,string Category="other");
    static readonly Rule[] services=Load("RecurringServices"), aliases=Load("MerchantAliases");
    static readonly (Rule Rule,Regex Match)[] aliasMatchers=aliases.Select(a=>(a,new Regex(@"(?<!\p{L})"+Regex.Escape(a.Pattern)+@"(?!\p{L})",RegexOptions.IgnoreCase|RegexOptions.CultureInvariant))).ToArray();
    static Rule[] Load(string name) { using var stream=typeof(RecurringCosts).Assembly.GetManifestResourceStream("SimpleBanking."+name+".json")!;return JsonSerializer.Deserialize<Rule[]>(stream)!; }
    internal static bool WordStart(string text,string marker)=>Regex.IsMatch(text,@"(?<!\p{L})"+Regex.Escape(marker),RegexOptions.IgnoreCase|RegexOptions.CultureInvariant);
    static bool Word(string text,string marker)=>Regex.IsMatch(text,@"(?<!\p{L})"+Regex.Escape(marker)+@"(?!\p{L})",RegexOptions.IgnoreCase|RegexOptions.CultureInvariant);
    internal static bool StandingOrder(Entry entry)=>new[]{"DAUERAUFTR","STANDING ORDER","DAUERBUCHUNG"}.Any(m=>WordStart(entry.AdditionalInformation,m));
    internal static string CanonicalMerchant(string name)=>IsPaymentProcessor(name)?name:aliasMatchers.FirstOrDefault(a=>a.Match.IsMatch(name)).Rule?.Name??name;
    internal static string Merchant(Entry entry) {
        string name=Regex.Replace(entry.Merchant,@"\s+"," ").Trim(' ','.',',',';','/','|');
        bool intermediary=new[]{"paypal","klarna","stripe","mollie","adyen","payone","sumup"}.Any(p=>Word(name,p));
        if(intermediary) {
            var found=aliasMatchers.FirstOrDefault(a=>a.Match.IsMatch(entry.Purpose)).Rule;
            if(found!=null)return found.Name;
            // Do not combine unrelated intermediary purchases into one recurring group.
            var match=Regex.Match(entry.Purpose,@"(?:ihr\s+einkauf\s+bei|purchase\s+at|paypal\s*\*)\s*(.+)$",RegexOptions.IgnoreCase);
            name=match.Success?match.Groups[1].Value.Trim():name+" · "+entry.Purpose.Trim();
        }
        if(IsPaymentProcessor(name))return name;
        return aliasMatchers.FirstOrDefault(a=>a.Match.IsMatch(name)).Rule?.Name ?? name;
    }
    internal static string Category(string merchant)=>merchant=="Bankgebühren"?"bankFees":services.FirstOrDefault(s=>merchant.Contains(s.Pattern,StringComparison.OrdinalIgnoreCase))?.Category??"other";
    internal static bool IsPaymentProcessor(string merchant) {
        var name=" "+Regex.Replace(Regex.Replace(merchant.ToLowerInvariant(),@"[^a-z0-9 ]"," "),@"\s+"," ").Trim()+" ";
        return new[]{"paypal","pp europe","klarna","sofort","sofortueberweisung","mollie","adyen","stripe","sumup","zettle","izettle","amazon payments","amazon pay","google pay","apple pay","concardis","nexi","unzer","payone","worldline","six payment services","shopify payments","ratepay","billpay","paysafe","skrill","wirecard","kartenzahlung intermediaer"}.Any(p=>name.Contains(" "+p+" ",StringComparison.Ordinal));
    }
    internal static bool BankFee(Entry e,string? bankName) {
        if(e.Amount>=0)return false;
        var text=(e.Purpose+" "+e.AdditionalInformation).ToLowerInvariant();
        if(new[]{"mahngebühr","mahngebuehr","stornogebühr","stornogebuehr","versandkosten","bearbeitungsgebühr","bearbeitungsgebuehr"}.Any(text.Contains))return false;
        if(!new[]{"entgeltabrechnung","entgelt","kontoführung","kontofuehrung","kontopreis","grundpreis","buchungsposten","rechnungsabschluss","kontogebühr","kontogebuehr"}.Any(text.Contains))return false;
        var party=e.BankCounterpartyName??e.Merchant;
        if(string.IsNullOrWhiteSpace(party))return true;
        static IEnumerable<string> Tokens(string t)=>Regex.Matches(t.ToLowerInvariant(),@"\p{L}+").Select(m=>m.Value).Where(t=>t.Length>=4 && t is not ("bank" or "gmbh" or "aktiengesellschaft"));
        return bankName!=null && Tokens(party).Intersect(Tokens(bankName)).Any();
    }
    internal static List<RecurringPayment> Analyze(IEnumerable<Entry> entries,string? bankName=null,ISet<string>? excluded=null) {
        var groups=entries.Where(e=>e.Amount<0&&!e.Pending).GroupBy(e=>{
            if(BankFee(e,bankName))return "Bankgebühren";
            var merchant=Merchant(e);var iban=e.CounterpartyIban;
            return Category(merchant)!="other" || iban.Length==0?merchant:merchant+"|"+iban[^Math.Min(8,iban.Length)..];
        });
        var result=new List<RecurringPayment>();
        foreach(var group in groups) {
            if(excluded?.Contains(group.Key)==true)continue;
            var list=group.OrderBy(e=>e.Date).ToList();var merchant=group.Key.Split('|')[0];
            if(IsPaymentProcessor(merchant))continue;
            if(merchant=="Bankgebühren" && (list.Count<2 || list.Any(e=>Math.Abs(e.Amount-list[0].Amount)>=.01m) || list.Zip(list.Skip(1)).Any(p=>(p.Second.Date.DayNumber-p.First.Date.DayNumber) is <20 or >100)))continue;
            var standing=list.Any(StandingOrder);var known=Category(merchant)!="other";
            int months=list.Select(e=>(e.Date.Year,e.Date.Month)).Distinct().Count();
            decimal average=list.Average(e=>Math.Abs(e.Amount));
            if(standing && list.Count==1) {result.Add(new(merchant,group.Key,average,1,1,PaymentFrequency.Monthly,list[0].Date,Category(merchant),.55,list.Select(e=>e.Id).ToArray()));continue;}
            if(list.Count<2 || (months<2 && !standing))continue;
            double variance=(double)list.Max(e=>Math.Abs(Math.Abs(e.Amount)-average)/average);
            double varianceLimit=list.Count==2&&!standing?(known?.15:.10):(known||standing?.50:.35);
            if(variance >= varianceLimit)continue;
            double interval=list.Zip(list.Skip(1)).Average(p=>p.Second.Date.DayNumber-p.First.Date.DayNumber);
            var frequency=interval is >=25 and <=35?PaymentFrequency.Monthly:interval is >=85 and <=100?PaymentFrequency.Quarterly:interval is >=350 and <=380?PaymentFrequency.Yearly:PaymentFrequency.Irregular;
            if(frequency==PaymentFrequency.Yearly && list.Select(e=>e.Date.Year).Distinct().Count()<2)frequency=PaymentFrequency.Irregular;
            if(frequency==PaymentFrequency.Irregular && months>=2 && list.Count>=months)frequency=PaymentFrequency.Monthly;
            double confidence=.5+Math.Min(.2,Math.Max(0,(list.Count-2)*.05))+Math.Min(.15,Math.Max(0,(months-2)*.05))+(1-variance)*.1+(frequency!=PaymentFrequency.Irregular ? .1 : 0)+(standing ? .2 : 0);
            confidence=Math.Clamp(confidence,0,1);
            if(confidence>=.5)result.Add(new(merchant,group.Key,average,list.Count,months,frequency,list[^1].Date,Category(merchant),confidence,list.Select(e=>e.Id).ToArray()));
        }
        return result.OrderByDescending(p=>p.AverageAmount).ToList();
    }
    internal static DateOnly NextExpected(RecurringPayment payment)=>payment.LastDate.AddMonths(payment.Frequency switch {PaymentFrequency.Monthly=>1,PaymentFrequency.Quarterly=>3,PaymentFrequency.Yearly=>12,_=>0});
    internal static (DateOnly Start,DateOnly End) Cycle(int salaryDay,DateOnly today,DateOnly? salaryArrival=null) {
        DateOnly InMonth(DateOnly month)=>new(month.Year,month.Month,Math.Clamp(salaryDay,1,DateTime.DaysInMonth(month.Year,month.Month)));
        var month=new DateOnly(today.Year,today.Month,1);var nominal=InMonth(month);
        var start=nominal<=today?nominal:InMonth(month.AddMonths(-1));var end=nominal>today?nominal:InMonth(month.AddMonths(1));
        if(salaryArrival is {} arrival && arrival>start && arrival<=today)return(arrival,arrival.AddMonths(1));
        return(start,end);
    }
    internal static IEnumerable<RecurringPayment> OutstandingPayments(IEnumerable<RecurringPayment> payments,DateOnly start,DateOnly end)=>payments.Where(p=>p.Frequency!=PaymentFrequency.Irregular && p.Confidence>=.6 && p.LastDate<start && NextExpected(p)>=start && NextExpected(p)<=end);
    internal static decimal LeftToPay(IEnumerable<RecurringPayment> payments,DateOnly start,DateOnly end)=>OutstandingPayments(payments,start,end).Sum(p=>p.AverageAmount);
    internal static bool SalaryCredit(Entry e) {
        if(e.PurposeCode.Equals("SALA",StringComparison.OrdinalIgnoreCase))return true;
        var text=e.Purpose+" "+e.AdditionalInformation;
        if(new[]{"LOHNSTEUER","LOHNERSATZ","LOHNPFAEND","LOHNPFÄND","GEHALTSPFAEND","GEHALTSPFÄND","KURZARBEITERGELD"}.Any(m=>WordStart(text,m)))return false;
        return new[]{"GEHALT","LOHN","BEZUEGE","BEZÜGE","SALAER","SALÄR"}.Any(m=>WordStart(text,m));
    }
}
