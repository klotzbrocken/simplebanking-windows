using System.Globalization;
namespace SimpleBanking;
internal static class UiFormat
{
    internal static string Money(decimal value, string currency)
    {
        string symbol = currency switch { "EUR" => "€", "USD" => "$", "GBP" => "£", _ => currency };
        return value.ToString("N2", CultureInfo.GetCultureInfo("de-DE")).Replace("-","−") + " " + symbol;
    }
}
