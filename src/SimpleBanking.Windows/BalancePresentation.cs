namespace SimpleBanking;

// Direct port of BalanceSignal.classify and BalanceWash.colors (light appearance).
internal sealed record BalancePresentation(int Level, Color Top, Color Bottom, Color Ink, Color Detail, string Status, string Emoji)
{
    internal static BalancePresentation Create(decimal? value, AppPreferences preferences)
    {
        int low = Math.Max(0, preferences.Low);
        int medium = Math.Max(low + 1, preferences.Medium);
        int high = Math.Max(medium + 1, preferences.VeryGood);
        int level = value == null ? 6 : value < Math.Min(-1, preferences.Deep) ? 0 : value < 0 ? 1 : value < low ? 2 : value <= medium ? 3 : value <= high ? 4 : 5;
        string[] colors = level < 2 ? ["#fbeeee", "#f5e2e2", "#b0242c", "#9a6060"] : level < 4 ? ["#f7f3e8", "#f1ece0", "#8a6d1e", "#8a7d5f"] : level < 6 ? ["#eef7f1", "#e3f1e9", "#177046", "#5f8974"] : ["#f5f5f5", "#ededed", "#777777", "#777777"];
        string[] status = ["Tief im Dispo", "Konto überzogen", "Niedriger Stand", "Mittlerer Stand", "Gutes Polster", "Sehr wohlhabend", "Kein Stand verfügbar"];
        string[] emoji = ["💀", "😟", "🥵", "🙃", "🙂", "😎", ""];
        var theme=ThemePalette.For(preferences);
        if(!theme.IsDefault)return new(level,theme.Surface,theme.Surface,value<0?theme.Negative:theme.Ink,theme.Secondary,status[level],emoji[level]);
        if(theme.IsDark){string[] palette=["#B05050","#D77979","#D9A354","#E4BA78","#67B487","#5DBE8B","#AAAAAA"];var color=ColorTranslator.FromHtml(palette[level]);return new(level,ThemePalette.Blend(color,theme.Surface,level==6?0:.20),ThemePalette.Blend(color,theme.Surface,level==6?0:.06),color,theme.Secondary,status[level],emoji[level]);}
        return new(level, ColorTranslator.FromHtml(colors[0]), ColorTranslator.FromHtml(colors[1]), ColorTranslator.FromHtml(colors[2]), ColorTranslator.FromHtml(colors[3]), status[level], emoji[level]);
    }
    internal static decimal? Aggregate(IReadOnlyList<Account> accounts) => accounts.Count > 0 && accounts.All(a => a.BalanceKnown && a.Currency == accounts[0].Currency) ? accounts.Sum(a => a.Balance) : null;
}
