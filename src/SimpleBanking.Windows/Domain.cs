using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace SimpleBanking;

public sealed record Account(string Id, string Name, string Iban, string Currency, decimal Balance, string? ConnectionId = null, bool BalanceKnown = true, string? LogoId = null, DateTimeOffset? BalanceFetchedAt = null, string? BankName = null,string? OwnerName=null);
public sealed record Entry(string Id, string AccountId, DateOnly Date, decimal Amount, string Currency,
    string Merchant, string Purpose, string Category, bool Pending = false, bool Read = false, bool Flagged = false, string Note = "",
    DateOnly? ValueDate = null, string CounterpartyIban = "", string CounterpartyBic = "", string AdditionalInformation = "", string PurposeCode = "", string? BankCounterpartyName = null, string EndToEndId = "", bool CategoryManual = false,
    List<BookingAttachment>? Attachments = null, DateTime? ReminderAt = null, bool ReminderDone = false, string? OriginalMerchant = null, byte[]? CustomLogo = null, string? MerchantOverride = null, [property: System.Text.Json.Serialization.JsonIgnore] byte[]? MerchantLogo = null,
    string BankTransactionCode = "", string? MerchantSource = null, double? MerchantConfidence = null);
public sealed record BookingAttachment(string Id,string Name,byte[] Content);
public sealed record TransferDraft(string AccountId, string Name, string Iban, decimal Amount, string Purpose, DateOnly? Date = null, string? RoundupId = null);
public sealed class BankData
{
    public List<AutomationClient> AutomationClients {get;set;}=[];
    public List<AutomationDraft> AutomationDrafts {get;set;}=[];
    public AiConfiguration Ai {get;set;}=new();
    public Dictionary<string,DateOnly> AttentionReadUntil {get;set;} = new();
    public Dictionary<string,byte[]> MerchantLogos {get;set;} = new();
    public List<MerchantUserRule> MerchantRules {get;set;} = [];
    public string? SimpleSendLicense {get;set;}
    public List<PaymentRecord> Payments {get;set;} = [];
    public List<PaymentTemplate> PaymentTemplates {get;set;} = [];
    public List<Entry> UnmatchedEnrichments {get;set;} = [];
    public List<RoundupResolution> Roundups {get;set;} = [];
    public List<AssignmentRule> Rules { get; set; } = [];
    public Dictionary<string,string> RecurringAssignments { get; set; } = new();
    public int Version { get; set; } = 1;
    public List<Account> Accounts { get; set; } = [];
    public List<Entry> Entries { get; set; } = [];
    public List<TransferDraft> Drafts { get; set; } = [];
    public YaxiSettings? Yaxi { get; set; }
    public List<BankConnection> Connections { get; set; } = [];
}
public static class BankingRules
{
    public static string NormalizeIban(string value) => string.Concat(value.Where(c => !char.IsWhiteSpace(c))).ToUpperInvariant();
    public static bool ValidIban(string value)
    {
        var s = NormalizeIban(value);
        if (!Regex.IsMatch(s, "^[A-Z]{2}[0-9]{2}[A-Z0-9]{11,30}$")) return false;
        if (s.StartsWith("DE") && s.Length != 22) return false;
        int rem = 0;
        foreach (var c in s[4..] + s[..4])
            foreach (var d in c is >= 'A' and <= 'Z' ? (c - 'A' + 10).ToString(CultureInfo.InvariantCulture) : c.ToString())
                rem = (rem * 10 + d - '0') % 97;
        return rem == 1;
    }
    public static bool WordMatch(string haystack, string needle, bool prefix = false)
    {
        if (needle.Length == 0) return false;
        var pattern = (char.IsLetter(needle[0]) ? @"(?<!\p{L})" : "") + Regex.Escape(needle)
            + (!prefix && char.IsLetter(needle[^1]) ? @"(?!\p{L})" : "");
        return Regex.IsMatch(haystack, pattern, RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1));
    }
    public static string? Validate(TransferDraft t)
    {
        if (string.IsNullOrWhiteSpace(t.AccountId)) return "Quellkonto fehlt.";
        if (string.IsNullOrWhiteSpace(t.Name) || t.Name.Length > 70) return "Empfängername muss 1–70 Zeichen enthalten.";
        if (!ValidIban(t.Iban)) return "IBAN ist ungültig.";
        if (t.Amount <= 0 || decimal.Round(t.Amount, 2) != t.Amount) return "Betrag muss positiv sein und höchstens zwei Nachkommastellen haben.";
        if(t.Amount>100000)return "Betrag zu hoch (max. 100.000 €).";
        if (t.Purpose.Length > 140) return "Verwendungszweck darf höchstens 140 Zeichen enthalten.";
        if (t.Date < DateOnly.FromDateTime(DateTime.Today)) return "Ausführungstag liegt in der Vergangenheit.";
        return null;
    }
    public static TransferDraft? ParseGiroCode(string value, string accountId)
    {
        var lines = value.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n').Select(s => s.Trim()).ToArray();
        if (lines.Length < 7 || lines[0] != "BCD" || lines[3] != "SCT" || !ValidIban(lines[6])) return null;
        decimal amount = 0;
        if (lines.Length > 7 && lines[7].StartsWith("EUR"))
            decimal.TryParse(lines[7][3..], NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out amount);
        var purpose = lines.Length > 10 && lines[10].Length > 0 ? lines[10] : lines.Length > 9 ? lines[9] : "";
        return new(accountId, lines[5], NormalizeIban(lines[6]), amount, purpose);
    }
    public static string Money(decimal amount, string currency) => amount.ToString("N2", CultureInfo.GetCultureInfo("de-DE")) + " " + currency;
    public static Dictionary<string, decimal> Totals(IEnumerable<Entry> entries) => entries.GroupBy(e => e.Currency).ToDictionary(g => g.Key, g => g.Sum(e => e.Amount));
}

// Independently encrypted data roots for demo and real use. Product API keys are build resources.
public sealed class EncryptedStore(string root)
{
    private const int Iterations = 600_000;
    private static readonly byte[] Aad = "simplebanking-windows-v1"u8.ToArray();
    public string Root { get; } = Path.GetFullPath(root);
    public string FilePath => Path.Combine(Root, "vault.sbw");
    public bool Exists => File.Exists(FilePath);
    private sealed record Envelope(int Version, int Iterations, byte[] Salt, byte[] Nonce, byte[] Tag, byte[] Ciphertext);
    public static byte[] Encrypt(BankData data, string password)
    {
        var salt = RandomNumberGenerator.GetBytes(16);
        var nonce = RandomNumberGenerator.GetBytes(12);
        var key = Rfc2898DeriveBytes.Pbkdf2(password, salt, Iterations, HashAlgorithmName.SHA256, 32);
        var plain = JsonSerializer.SerializeToUtf8Bytes(data);
        var cipher = new byte[plain.Length]; var tag = new byte[16];
        try { using var aes = new AesGcm(key, 16); aes.Encrypt(nonce, plain, cipher, tag, Aad); }
        finally { CryptographicOperations.ZeroMemory(key); CryptographicOperations.ZeroMemory(plain); }
        var bytes=JsonSerializer.SerializeToUtf8Bytes(new Envelope(1, Iterations, salt, nonce, tag, cipher));
        if(bytes.Length>64*1024*1024)throw new InvalidDataException("Die verschlüsselte Sicherung überschreitet 64 MB. Es wurden keine Daten ausgelassen.");
        return bytes;
    }
    public static BankData Decrypt(byte[] bytes, string password)
    {
        if (bytes.Length > 64 * 1024 * 1024) throw new InvalidDataException("Sicherung zu groß.");
        var e = JsonSerializer.Deserialize<Envelope>(bytes) ?? throw new InvalidDataException("Leere Sicherung.");
        if (e.Version != 1 || e.Iterations != Iterations || e.Salt.Length != 16 || e.Nonce.Length != 12 || e.Tag.Length != 16)
            throw new InvalidDataException("Nicht unterstütztes Sicherungsformat.");
        var key = Rfc2898DeriveBytes.Pbkdf2(password, e.Salt, e.Iterations, HashAlgorithmName.SHA256, 32);
        var plain = new byte[e.Ciphertext.Length];
        try
        {
            using var aes = new AesGcm(key, 16); aes.Decrypt(e.Nonce, e.Ciphertext, e.Tag, plain, Aad);
            var data = JsonSerializer.Deserialize<BankData>(plain) ?? throw new InvalidDataException("Leere Daten.");
            if (data.Version != 1 || data.Accounts.Select(a => a.Id).Distinct().Count() != data.Accounts.Count
                || data.Entries.Any(t => !data.Accounts.Any(a => a.Id == t.AccountId && a.Currency == t.Currency)))
                throw new InvalidDataException("Inkonsistente Kontodaten.");
            return data;
        }
        finally { CryptographicOperations.ZeroMemory(key); CryptographicOperations.ZeroMemory(plain); }
    }
    public BankData Load(string password) => Decrypt(File.ReadAllBytes(FilePath), password);
    public static void WriteBackup(string path,BankData data,string password) {
        var bytes=Encrypt(data,password);
        var full=Path.GetFullPath(path);var temporary=full+"."+Guid.NewGuid().ToString("N")+".tmp";
        try {
            using(var stream=new FileStream(temporary,FileMode.CreateNew,FileAccess.Write,FileShare.None)){stream.Write(bytes);stream.Flush(true);}
            File.Move(temporary,full,true);
        }finally{if(File.Exists(temporary))File.Delete(temporary);}
    }
    public void Save(BankData data, string password)
    {
        Directory.CreateDirectory(Root);
        var bytes = Encrypt(data, password);
        if(bytes.Length>64*1024*1024)throw new InvalidDataException("Der Tresor überschreitet 64 MB. Bitte Anhänge exportieren und entfernen.");
        var temporary = FilePath + ".tmp";
        using (var stream = new FileStream(temporary, FileMode.Create, FileAccess.Write, FileShare.None))
        { stream.Write(bytes); stream.Flush(true); }
        if (File.Exists(FilePath)) File.Replace(temporary, FilePath, FilePath + ".previous");
        else File.Move(temporary, FilePath);
    }
}

public static class DemoData
{
    public static BankData Create()
    {
        var d = new BankData();
        d.Accounts.Add(new("demo-current", "Girokonto · Demo", "DE89370400440532013000", "EUR", 3284.52m, LogoId: "commerz", BankName: "Commerzbank"));
        d.Accounts.Add(new("demo-savings", "Tagesgeld · Demo", "DE12500105170648489890", "EUR", 12500m, LogoId: "ing", BankName: "ING"));
        var merchants = new[] { ("REWE", "Lebensmittel", -54.29m), ("Deutsche Bahn", "Mobilität", -49m), ("Stadtwerke", "Wohnen", -92m), ("Buchhandlung", "Freizeit", -24.90m), ("Arbeitgeber", "Gehalt", 2850m), ("Vermietung", "Wohnen", -850m) };
        for (int month = 0; month < 6; month++)
            for (int i = 0; i < merchants.Length; i++)
            {
                var m = merchants[i]; var date = DateOnly.FromDateTime(DateTime.Today).AddMonths(-month).AddDays(-i);
                d.Entries.Add(new($"demo-{month}-{i}", "demo-current", date, m.Item3, "EUR", m.Item1, m.Item2=="Gehalt"?"Gehalt · synthetische Beispielbuchung":"Synthetische Beispielbuchung", m.Item2));
            }
        var today=DateOnly.FromDateTime(DateTime.Today);
        for(int month=0;month<12;month++){
            var anchor=new DateOnly(today.Year,today.Month,1).AddMonths(-month);
            foreach(var service in new[]{("Netflix",13.99m,"Abos & Digital",5),("Spotify",10.99m,"Abos & Digital",8),("Allianz",72.30m,"Versicherungen",12),("Telekom",39.95m,"Abos & Digital",15)}){
                var date=anchor.AddDays(service.Item4-1);if(date>today)continue;
                d.Entries.Add(new($"demo-sub-{month}-{service.Item1}","demo-current",date,-service.Item2,"EUR",service.Item1,"Monatliches Abonnement · synthetische Daten",service.Item3,AdditionalInformation:"SEPA LASTSCHRIFT"));
            }
            if(anchor.AddDays(1)<=today)d.Entries.Add(new($"demo-interest-{month}","demo-savings",anchor.AddDays(1),23.42m,"EUR","ING","Zinsgutschrift · synthetische Daten","Einkommen"));
        }
        d.Entries.Add(new("demo-pending","demo-current",today,-42.80m,"EUR","REWE","Vorgemerkter Einkauf · synthetische Daten","Essen & Alltag",Pending:true));
        d.Entries.Add(new("demo-reminder","demo-current",today.AddDays(-2),-89m,"EUR","Fahrradwerkstatt","Reparatur · synthetische Daten","Mobilität",Note:"Beleg für die Garantie prüfen",ReminderAt:DateTime.Today.AddHours(9)));
        d.Rules.Add(new("demo-rule",true,10,[new("Händler","enthält","Netflix")],"Abos & Digital","Abos"));
        var demoTransfer=new TransferDraft("demo-current","Max Muster","DE89370400440532013000",25m,"Gemeinsames Essen · Demo");
        d.PaymentTemplates.Add(new("demo-template","Gemeinsames Essen",demoTransfer,true));
        d.Payments.Add(new("SBDEMO000000000000000000000000001",demoTransfer,DateTimeOffset.Now.AddDays(-2),"Bankannahme simuliert",true));
        using(var receipt=new Bitmap(360,180)){using(var g=Graphics.FromImage(receipt)){g.Clear(Color.White);using var font=new Font("Arial",18);g.DrawString("DEMO-BELEG\nFahrradwerkstatt\nReparatur 89,00 EUR\nKeine echte Rechnung",font,Brushes.Black,12,12);}using var stream=new MemoryStream();receipt.Save(stream,System.Drawing.Imaging.ImageFormat.Png);int i=d.Entries.FindIndex(e=>e.Id=="demo-reminder");d.Entries[i]=d.Entries[i] with{Attachments=[new("demo-attachment","Demo-Beleg.png",stream.ToArray())]};}
        return d;
    }
}
