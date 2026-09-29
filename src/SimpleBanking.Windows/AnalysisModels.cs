using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
namespace SimpleBanking;

public sealed record RuleCondition(string Field,string Operator,string Value,bool Or=false);
public sealed record AssignmentRule(string Id,bool Enabled,int Priority,List<RuleCondition> Conditions,string? Category,string? Recurring=null);
internal static class AssignmentRules
{
    internal static string Normalize(string value)=>string.Concat(value.Normalize(NormalizationForm.FormD).Where(c=>CharUnicodeInfo.GetUnicodeCategory(c)!=UnicodeCategory.NonSpacingMark)).ToLowerInvariant().Trim();
    internal static bool Matches(AssignmentRule rule,Entry e,PaymentFrequency? cadence=null) {
        bool Test(RuleCondition c) {
            if(c.Field=="Betrag") {
                if(!decimal.TryParse(c.Value.Replace(',','.'),NumberStyles.Number,CultureInfo.InvariantCulture,out var n))return false;
                return c.Operator switch {"="=>Math.Abs(Math.Abs(e.Amount)-n)<.005m,">"=>Math.Abs(e.Amount)>n,"<"=>Math.Abs(e.Amount)<n,_=>false};
            }
            if(c.Field=="Intervall")return cadence!=null && c.Value==Subscriptions.Frequency(cadence.Value);
            string text=c.Field switch {"Händler"=>RecurringCosts.Merchant(e),"Empfänger"=>e.Amount<0?e.BankCounterpartyName??e.Merchant:"","Absender"=>e.Amount>0?e.BankCounterpartyName??e.Merchant:"","Zweck"=>e.Purpose+" "+e.AdditionalInformation,"E2E-ID"=>e.EndToEndId,"Buchungsart"=>e.Amount<0?"Zahlung":"Eingang",_=>e.Merchant+" "+e.Purpose+" "+e.AdditionalInformation+" "+e.EndToEndId};
            text=Normalize(text);string target=Normalize(c.Value);if(target.Length==0)return false;
            return c.Operator switch {"enthält"=>text.Contains(target),"enthält nicht"=>!text.Contains(target),"ist"=>text==target,"ist nicht"=>text!=target,_=>false};
        }
        if(rule.Conditions.Count==0)return false;bool result=Test(rule.Conditions[0]);
        foreach(var c in rule.Conditions.Skip(1))result=c.Or?result|Test(c):result&Test(c);return result;
    }
    internal static IEnumerable<AssignmentRule> Sorted(IEnumerable<AssignmentRule> rules)=>rules.Where(r=>r.Enabled).OrderBy(r=>r.Priority);
    internal static string Summary(AssignmentRule rule)=>"Wenn "+string.Join(" ",rule.Conditions.Select((c,i)=>(i==0?"":c.Or?"ODER ":"UND ")+c.Field+" "+c.Operator+" „"+c.Value+"“"))+" → "+string.Join(" + ",new[]{rule.Category,rule.Recurring}.Where(s=>!string.IsNullOrEmpty(s)));
}
internal sealed record Subscription(string Id,string Name,decimal Average,decimal LastAmount,DateOnly LastDate,PaymentFrequency Cadence,int Confidence,string Category,string Tab,IReadOnlyList<Entry> Entries,string? CancellationUrl=null) {
    internal decimal Monthly=>Cadence switch{PaymentFrequency.Monthly=>Average,PaymentFrequency.Quarterly=>Average/3,PaymentFrequency.Yearly=>Average/12,_=>0};
}
internal static class Subscriptions
{
    internal sealed record Cancellation(string[] Keywords,string Name,string Url,string? Note);
    static readonly Cancellation[] cancellation=Load();
    static Cancellation[] Load(){using var stream=typeof(Subscriptions).Assembly.GetManifestResourceStream("SimpleBanking.Cancellation.json")!;return System.Text.Json.JsonSerializer.Deserialize<Cancellation[]>(stream)!;}
    internal static string Frequency(PaymentFrequency frequency)=>frequency switch{PaymentFrequency.Monthly=>"Monatlich",PaymentFrequency.Quarterly=>"Vierteljährlich",PaymentFrequency.Yearly=>"Jährlich",_=>"Unregelmäßig"};
    internal static List<Subscription> Detect(IEnumerable<Entry> source,string? bankName=null,IReadOnlyList<AssignmentRule>? rules=null,IReadOnlyDictionary<string,string>? assignments=null) {
        var list=source.Where(e=>e.Amount<0&&!new[]{"erstattung","rückzahlung","retoure","umbuchung","übertrag auf konto"}.Any(e.Purpose.ToLowerInvariant().Contains)).ToList();
        string Group(Entry e){
            if(RecurringCosts.BankFee(e,bankName))return "Bankgebühren";
            var merchant=RecurringCosts.Merchant(e);var category=RecurringCosts.Category(merchant);
            if(new[]{"paypal","klarna"}.Any(e.Merchant.ToLowerInvariant().Contains))return merchant+"|agg";
            var customer=Regex.Match(e.Purpose,@"(?:Kd\.|KdNr\.?\s*|Kundennr\.?\s*|Vertragsnr\.?\s*)(\d{6,12})");
            if(category=="telecom"&&customer.Success)return merchant+"|"+customer.Groups[1].Value;
            return category!="other"||e.CounterpartyIban.Length==0?merchant:merchant+"|"+e.CounterpartyIban[^Math.Min(8,e.CounterpartyIban.Length)..];
        }
        var result=new List<Subscription>();
        foreach(var group in list.GroupBy(Group)) {
            var rows=group.OrderBy(e=>e.Date).ToArray();bool standing=rows.Any(RecurringCosts.StandingOrder);if(rows.Length<2&&!standing)continue;
            string name=group.Key.Split('|')[0],lower=name.ToLowerInvariant(),category=RecurringCosts.Category(name);
            if(new[]{"paypal europe","klarna bank","amazon payments","google payment","apple pay"}.Any(lower.Contains))continue;
            decimal avg=rows.Average(e=>-e.Amount),dev=rows.Max(e=>Math.Abs(-e.Amount-avg)),variance=dev/avg;
            var gaps=rows.Zip(rows.Skip(1)).Select(p=>p.Second.Date.DayNumber-p.First.Date.DayNumber).ToArray();
            if(category=="bankFees" && (rows.Length<2||dev>=.01m||gaps.Any(d=>d<20||d>100)))continue;
            decimal limit=category=="finance"?5000:category is "insurance" or "utilities"?2000:new[]{"wohnungsbau","mietverwaltung","hausverwaltung"}.Any(lower.Contains)?3000:600;
            if(standing||(rows.Length>=3&&variance<=.05m))limit=Math.Max(limit,5000);if(avg>limit)continue;
            bool amountOK=variance<=(category is "telecom" or "transport" or "finance" or "insurance" or "utilities"?.30m:.15m)||dev<=2;
            double interval=gaps.Length>0?gaps.Average():0;bool monthly=interval is >=25 and <=35;
            var cancel=cancellation.FirstOrDefault(c=>c.Keywords.Any((lower+" "+rows[0].Purpose.ToLowerInvariant()).Contains));
            bool similar=rows.Select(e=>e.Purpose.ToLowerInvariant().Split(' ',StringSplitOptions.RemoveEmptyEntries).FirstOrDefault()).Where(s=>!string.IsNullOrEmpty(s)).Distinct().Count()<=2;
            int score=3+(rows.Length>=3?3:0)+(monthly?3:0)+(amountOK?2:-3)+(similar?2:0)+(cancel!=null||category is "streaming" or "software" or "membership" or "telecom" or "finance" or "insurance" or "utilities"?2:0)+(rows.Max(e=>e.Date.Day)-rows.Min(e=>e.Date.Day)<=5?1:0)+(!monthly&&gaps.Length>0?-2:0)+(standing?3:0);
            if(score<7)continue;
            string tab=category switch {"finance"=>"Sparen","bankFees" or "insurance" or "utilities"=>"Verbindlichkeiten","telecom" or "transport"=>"Verträge","membership" when new[]{"mcfit","clever fit","cleverfit","urban sports","fitx","fitness first","kieser","yoga","pilates","holmes place","john reed","sportstudio","fitnessclub","crossfit","anytime fitness"}.Any(lower.Contains)=>"Verträge",_=>new[]{"versicherung","krankenkasse","aok","barmer","debeka","stadtwerke","eon ","rwe","vattenfall","enpal","wohnungsbau","mietverwaltung","hausverwaltung","mietvertrag","kreditrate","kreditabrechnung","miete"}.Any(lower.Contains)?"Verbindlichkeiten":"Abos"};
            var cadence=monthly?PaymentFrequency.Monthly:PaymentFrequency.Irregular;
            string? action=assignments?.GetValueOrDefault(rows[0].AccountId+"/"+group.Key)??AssignmentRules.Sorted(rules??[]).FirstOrDefault(r=>r.Recurring!=null&&rows.Any(e=>AssignmentRules.Matches(r,e,cadence)))?.Recurring;
            if(action=="Ausschließen")continue;if(action!=null){tab=action;score=Math.Max(score,10);}
            result.Add(new(group.Key,cancel?.Name??name,avg,-rows[^1].Amount,rows[^1].Date,cadence,score,category,tab,rows,cancel?.Url));
        }
        return result.OrderByDescending(s=>s.Average).ToList();
    }
}
internal sealed record Mmi(decimal Income,decimal Expenses,decimal Savings,decimal Balance,int Months) {
    internal double SavingsRate=>Income>0?Math.Clamp((double)((Income-Expenses+Savings)/Income),-1,1):0;
    internal double Buffer=>Expenses>0?Math.Max(0,(double)(Balance/(Expenses/Math.Max(1,Months)))):Balance>0?double.PositiveInfinity:0;
    internal double Score {get{double[] x=[0,.25,.5,1,3,6],y=[0,.15,.30,.55,.85,1];double b=Buffer>=6?1:0;for(int i=1;i<x.Length;i++)if(Buffer<=x[i]){b=y[i-1]+(Buffer-x[i-1])/(x[i]-x[i-1])*(y[i]-y[i-1]);break;}return Math.Min(1,b+Math.Max(0,SavingsRate)*.15);}}
    internal string Rating=>Score<.15?"Kritisch":Score<.30?"Angeschlagen":Score<.55?"Solide":Score<.85?"Gesund":"Exzellent";
    internal static string Kind(Entry e) {
        string text=(e.Purpose+" "+e.AdditionalInformation+" "+(e.Amount<0?e.BankCounterpartyName??e.Merchant:"")).ToLowerInvariant();
        if(e.PurposeCode=="XBND"||text.Contains("umbuchung")||text.Contains("übertrag"))return "transfer";
        if(e.Amount>0)return e.Amount<5||new[]{"erstattung","rücküberweisung","rückbuchung","gutschrift","refund","storno","cashback","korrektur","retoure"}.Any(text.Contains)?"refund":"income";
        return new[]{"sparplan","etf","depot","vorsorge","riester","rürup","fondssparplan","invest","tagesgeld","festgeld","wertpapier"}.Any(text.Contains)?"savings":"expense";
    }
    internal static Mmi Calculate(IEnumerable<Entry> entries,decimal balance,int period,DateOnly today,IEnumerable<Subscription> subscriptions,bool includeSavings=true) {
        var tagged=subscriptions.Where(s=>s.Tab=="Sparen").SelectMany(s=>s.Entries).Select(e=>e.AccountId+"/"+e.Id).ToHashSet();
        var cutoff=period==0?today.AddMonths(-1):period==1?today.AddMonths(-3):new DateOnly(today.Year,1,1);
        var rows=entries.Where(e=>e.Date>=cutoff).ToList();bool Tagged(Entry e)=>tagged.Contains(e.AccountId+"/"+e.Id);
        return new(rows.Where(e=>Kind(e)=="income").Sum(e=>e.Amount),rows.Where(e=>Kind(e)=="expense"&&!Tagged(e)).Sum(e=>Math.Abs(e.Amount)),includeSavings?rows.Where(e=>Kind(e)=="savings"||(Kind(e)=="expense"&&Tagged(e))).Sum(e=>Math.Abs(e.Amount)):0,balance,period==0?1:period==1?3:today.Month);
    }
}
internal sealed record MoneyAgeResult(double? Days,int Samples,int Total,int Uncovered,double? Previous) {
    internal static MoneyAgeResult Calculate(IEnumerable<Entry> entries,int size=10) {
        var funds=new LinkedList<(DateOnly Date,decimal Amount)>();var ages=new List<double>();int total=0,uncovered=0;
        foreach(var e in entries.OrderBy(e=>e.Date)) {
            if(e.Amount>0){funds.AddLast((e.Date,e.Amount));continue;}if(e.Amount==0)continue;
            total++;decimal remaining=-e.Amount,weighted=0,consumed=0;
            while(remaining>0&&funds.First is {} first){decimal take=Math.Min(first.Value.Amount,remaining);weighted+=take*Math.Max(0,e.Date.DayNumber-first.Value.Date.DayNumber);consumed+=take;remaining-=take;if(take==first.Value.Amount)funds.RemoveFirst();else first.Value=(first.Value.Date,first.Value.Amount-take);}
            if(consumed>0)ages.Add((double)(weighted/consumed));if(remaining>0)uncovered++;
        }
        var window=ages.TakeLast(Math.Max(1,size)).ToArray();var previous=ages.Take(ages.Count-window.Length).TakeLast(Math.Max(1,size)).ToArray();
        return new(window.Length>0?window.Average():null,window.Length,total,uncovered,previous.Length>0?previous.Average():null);
    }
}
