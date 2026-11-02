using System.Globalization;
namespace SimpleBanking;

internal sealed class AccountFinanceSettings
{
    public decimal SalaryAmount { get; set; }
    public int SalaryTolerance { get; set; } = 3;
    public decimal DispoLimit { get; set; }
    public int SalaryDay { get; set; } = 1;
    public int SalaryPreset { get; set; } = 2;
    public int EffectiveSalaryDay => SalaryPreset switch {0=>1,1=>15,_=>Math.Clamp(SalaryDay,1,31)};
}
internal sealed record RecurringSummary(decimal? Outstanding,decimal? BankFee,DateOnly? CycleEnd,IReadOnlyList<RecurringPayment> Payments,DateOnly? CycleStart=null)
{
    internal static readonly RecurringSummary Unknown=new(null,null,null,[]);
    internal static RecurringSummary Calculate(IEnumerable<Entry> entries,Account account,AccountFinanceSettings settings,DateOnly today,IReadOnlyDictionary<string,string>? assignments=null,IReadOnlyList<AssignmentRule>? rules=null) {
        var history=entries.Where(e=>!e.Pending && e.AccountId==account.Id && e.Currency==account.Currency && e.Date>=today.AddDays(-90) && e.Date<=today).ToList();
        if(history.Count==0)return Unknown;
        var payments=RecurringCosts.Analyze(history,account.BankName??account.Name);
        payments=payments.Where(p=>{
            string? action=RecurringAssignmentKeys.Find(assignments,account.Id,p.GroupKey)??AssignmentRules.Sorted(rules??[]).FirstOrDefault(r=>r.Recurring!=null&&history.Where(e=>p.EntryIds.Contains(e.Id)).Any(e=>AssignmentRules.Matches(r,e,p.Frequency)))?.Recurring;
            return action!="Ausschließen";
        }).ToList();
        DateOnly? salary=history.Where(e=>e.Amount>0 && RecurringCosts.SalaryCredit(e)).Select(e=>(DateOnly?)e.Date).Max();
        var cycle=RecurringCosts.Cycle(settings.EffectiveSalaryDay,today,salary);
        var fee=payments.FirstOrDefault(p=>p.Category=="bankFees")?.AverageAmount;
        return new(RecurringCosts.LeftToPay(payments,cycle.Start,cycle.End),fee,cycle.End,payments,cycle.Start);
    }
    internal string Subtitle(int mode,decimal? balance,string currency,DateOnly today,bool unified=false) {
        string Money(decimal value)=>decimal.Round(value,0,MidpointRounding.AwayFromZero).ToString("N0",CultureInfo.GetCultureInfo("de-DE"))+" "+(currency=="EUR"?"€":currency);
        if(unified)mode=0;
        if(mode==3 && BankFee is {} fee)return "Bankgebühren "+Money(fee);
        if(mode is 1 or 2 && balance is {} knownBalance && Outstanding is {} due && CycleEnd is {} end && end>today) {
            decimal available=knownBalance-due;
            if(available<0)return Money(-available)+" überzogen bis "+end.ToString("dd.MM.");
            return mode==2?Money(available/(end.DayNumber-today.DayNumber))+"/Tag verfügbar":Money(available)+" bis "+end.ToString("dd.MM.");
        }
        return Outstanding is {} value ? value>.5m?"Fixkosten offen: "+Money(value):"Alle Fixkosten gebucht" : "";
    }
}
