namespace SimpleBanking;
internal sealed record RecipientCandidate(string Name,string Iban,decimal Amount,string Purpose,int Frequency,DateOnly LastDate)
{
    public override string ToString()=>Name+" · …"+Iban[^4..];
    internal TransferDraft Draft(string accountId)=>new(accountId,Name,Iban,Amount,Purpose);
}
internal static class RecipientHistory
{
    internal static RecipientCandidate[] Candidates(IEnumerable<Entry> entries,string accountId)=>entries
        .Where(e=>e.AccountId==accountId&&e.Currency=="EUR"&&!e.Pending&&e.Amount<0&&BankingRules.ValidIban(e.CounterpartyIban))
        .Select(e=>new{Entry=e,Name=(e.BankCounterpartyName??e.OriginalMerchant??e.Merchant).Trim(),Iban=BankingRules.NormalizeIban(e.CounterpartyIban)})
        .Where(x=>x.Name.Length>=2&&x.Name!="Unbekannt")
        .GroupBy(x=>(Name:x.Name.ToUpperInvariant(),x.Iban))
        .Select(g=>{var recent=g.OrderByDescending(x=>x.Entry.Date).First();var mode=g.GroupBy(x=>-x.Entry.Amount).OrderByDescending(v=>v.Count()).ThenByDescending(v=>v.Max(x=>x.Entry.Date)).First().Key;return new RecipientCandidate(recent.Name,g.Key.Iban,mode,recent.Entry.Purpose,g.Count(),recent.Entry.Date);})
        .OrderByDescending(c=>c.Frequency).ThenByDescending(c=>c.LastDate).Take(100).ToArray();
}
