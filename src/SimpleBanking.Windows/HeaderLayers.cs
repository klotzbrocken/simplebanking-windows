namespace SimpleBanking;

// KopfSchichten.swift: opening search closes filters and vice versa.
internal readonly record struct HeaderLayers(bool Search = false, bool Filter = false)
{
    internal HeaderLayers ToggleSearch() => Search ? this with { Search = false } : new(true, false);
    internal HeaderLayers ToggleFilter() => Filter ? this with { Filter = false } : new(false, true);
}
internal enum BookingFilter { All, Income, Expense, FixedCosts, Uncategorized, Pending, Attachments, Reminders, Flagged }
internal static class BookingFilters
{
    internal static List<Entry> Select(IEnumerable<Entry> entries, string? accountId, bool unified, BookingFilter filter, string query, ISet<(string AccountId,string Merchant)>? fixedMerchants = null)
        => Select(entries,accountId,unified,new[]{filter},query,fixedMerchants);
    internal static List<Entry> Select(IEnumerable<Entry> entries,string? accountId,bool unified,IEnumerable<BookingFilter> filters,string query,ISet<(string AccountId,string Merchant)>? fixedMerchants=null)
    {
        var selected=filters.ToArray();
        query = query.Trim();
        return entries.Where(e => (unified || e.AccountId == accountId) && selected.All(filter=>filter==BookingFilter.FixedCosts ? fixedMerchants?.Contains((e.AccountId,RecurringCosts.Merchant(e)))==true : Matches(e,filter)) &&
            (query.Length == 0 || (e.Merchant + " " + e.Purpose + " " + e.Category + " " + e.Note + " " + e.Amount.ToString("N2") + " " + e.Date.ToString("MMMM yyyy")).Contains(query,StringComparison.CurrentCultureIgnoreCase)))
            .OrderByDescending(e => e.Date).ToList();
    }
    internal static bool Matches(Entry entry, BookingFilter filter) => filter switch {
        BookingFilter.Income => entry.Amount > 0,
        BookingFilter.Expense => entry.Amount < 0,
        BookingFilter.Uncategorized => string.IsNullOrWhiteSpace(entry.Category) || entry.Category is "Sonstiges" or "Unkategorisiert",
        BookingFilter.Pending => entry.Pending,
        BookingFilter.Attachments => entry.Attachments?.Count > 0,
        BookingFilter.Reminders => entry.ReminderAt != null && !entry.ReminderDone,
        BookingFilter.Flagged => entry.Flagged,
        BookingFilter.FixedCosts => false,
        _ => true
    };
}
