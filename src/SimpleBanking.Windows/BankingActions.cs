using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace SimpleBanking;

internal sealed partial class MainWindow
{
    bool bankBusy;
    readonly BankReadAccess bankReadAccess=new();
    void ConfigureYaxi()
    {
        if (!RequireUnlocked() || demo || bankBusy) return;
        var choice = MessageBox.Show(this, "Yaxi-Integrationsumgebung verwenden?\nJa: Integration. Nein: Produktivbanken.", "Umgebung", MessageBoxButtons.YesNoCancel);
        if (choice == DialogResult.Cancel) return;
        bool integration = choice == DialogResult.Yes;
        if (data.Connections.Count > 0 && data.Yaxi?.Integration != integration) { MessageBox.Show(this, "Die Umgebung eines bestehenden Kontobestands kann nicht gewechselt werden."); return; }
        data.Yaxi = new(integration); Save(); status.Text = "Yaxi-Umgebung gespeichert. Die App erstellt Tickets lokal wie die Mac-Version.";
    }
    void AddBank()
    {
        if (!RequireUnlocked() || demo || bankBusy) return;
        if (data.Yaxi == null) data.Yaxi = new(false);
        using var setup = new BankSetupWindow(data.Yaxi);
        if (setup.ShowDialog(this) != DialogResult.OK || setup.Connection == null) return;
        data.Connections.Add(setup.Connection); data.Accounts.AddRange(setup.Accounts); Save(); Populate();
        status.Text = "Bankverbindung gespeichert. Kontostand und Umsätze können jetzt abgerufen werden.";
    }
    void RefreshBank() => RefreshBankRange(null);
    async void RefreshBankRange(DateOnly? historyFrom)
    {
        if (!RequireUnlocked() || bankBusy || SelectedAccount is not { } account) return;
        if (demo) { status.Text = "Demo-Daten · Kein Netzwerkaufruf."; return; }
        if (data.Yaxi is not { } settings || data.Connections.FirstOrDefault(c => c.Id == account.ConnectionId) is not { } connection)
        { MessageBox.Show(this, "Bankverbindung oder Yaxi-Konfiguration fehlt."); return; }
        bankBusy = true; accounts.Enabled = false;
        try {
            using var client = new YaxiClient(settings.Integration); using var tickets = new TicketClient(settings); using var callback = new LoopbackCallback();
            using var timeout = new CancellationTokenSource(TimeSpan.FromMinutes(5));bankCancellation=timeout;
            var flow = new BankFlow(client);
            async Task<JsonObject> Run(string service, JsonObject request, JsonNode? ticketData = null) {
                var result=await bankReadAccess.RunAsync(connection,service,request,()=>tickets.IssueAsync(service,ticketData,timeout.Token),
                    next=>flow.RunAsync(next,service,r=>BankDialogs.Prompt(this,client,next["ticket"]!.GetValue<string>(),callback,r,timeout.Token),timeout.Token),timeout.Token);
                if (result["connectionData"] is JsonArray) {
                    connection = BankReadAccess.UpdatedConsent(connection,result);
                    var i = data.Connections.FindIndex(c => c.Id == connection.Id); data.Connections[i] = connection; Save();
                }
                timeout.Token.ThrowIfCancellationRequested();
                return result;
            }
            status.Text = "Kontostand wird abgerufen …";
            var balances = await Run("Balances", new JsonObject { ["op"] = "balances", ["accounts"] = new JsonArray(new JsonObject { ["iban"] = account.Iban, ["currency"] = account.Currency }) });
            var payload = TicketClient.DecodeJwt(balances["jwt"]!.GetValue<string>())["data"]!["data"]!;
            var amount = BankMapping.Balance(payload, account);
            if (amount == null) throw new InvalidDataException("Kein gebuchter Saldo für dieses Konto und diese Währung erhalten.");
            var index = data.Accounts.FindIndex(a => a.Id == account.Id); data.Accounts[index] = account with { Balance = amount.Value, BalanceKnown = true, BalanceFetchedAt = DateTimeOffset.UtcNow }; Save();
            status.Text = "Umsätze werden abgerufen …";
            var to = DateOnly.FromDateTime(DateTime.Today);var from = historyFrom ?? BankReadAccess.AutomaticFrom(to);
            var transactions = await Run("Transactions", new JsonObject { ["op"] = "transactions" }, new JsonObject {
                ["account"] = new JsonObject { ["iban"] = account.Iban, ["currency"] = account.Currency }, ["range"] = new JsonObject { ["from"] = from.ToString("yyyy-MM-dd"), ["to"] = to.ToString("yyyy-MM-dd") } });
            var tx = TicketClient.DecodeJwt(transactions["jwt"]!.GetValue<string>())["data"]!["data"] as JsonArray
                ?? throw new InvalidDataException("Keine vollständige Umsatzliste erhalten.");
            var entries = BankMapping.Entries(tx, account);
            data.Accounts[index]=OwnPartyRegistry.Learn(data.Accounts[index],tx);
            // Reconcile only the exact requested account/range after successful full parsing.
            var previous = data.Entries.Where(e => e.AccountId == account.Id).ToDictionary(e => e.Id);
            var unmatched=new List<Entry>();
            entries = BookingEnrichment.Reconcile(previous.Values.Where(e=>e.Date>=from&&e.Date<=to).Concat(data.UnmatchedEnrichments.Where(e=>e.AccountId==account.Id)).DistinctBy(e=>e.Id), entries,unmatched);
            data.UnmatchedEnrichments.RemoveAll(e=>e.AccountId==account.Id);data.UnmatchedEnrichments.AddRange(unmatched);
            data.Entries.RemoveAll(e => e.AccountId == account.Id && e.Date >= from && e.Date <= to);
            foreach (var e in entries) { data.Entries.RemoveAll(old => old.AccountId == account.Id && old.Id == e.Id); data.Entries.Add(e); }
            ApplyAnalysisRules(new HashSet<string>{account.Id},true);
            bool hasNewBookings=entries.Any(e=>!previous.ContainsKey(e.Id));
            Save(); Populate(); if(hasNewBookings)NewBookings?.Invoke(); status.Text = "Aktualisiert · " + DateTime.Now.ToString("g") + ".";
        }
        catch (Exception e) { status.Text = e is YaxiException or InvalidDataException or OperationCanceledException ? e.Message : "Bankabruf fehlgeschlagen: " + e.GetType().Name; Populate(); }
        finally { bankBusy = false; bankCancellation=null; accounts.Enabled = true; if(forceLockPending){forceLockPending=false;Lock();} }
    }
}

internal static class BankMapping
{
    public static decimal? Balance(JsonNode payload, Account account)
    {
        var matched = payload["balances"]?.AsArray().FirstOrDefault(b => BankingRules.NormalizeIban(b?["account"]?["iban"]?.GetValue<string>() ?? "") == account.Iban);
        var booked = matched?["balances"]?.AsArray().FirstOrDefault(b => b?["balanceType"]?.GetValue<string>() == "Booked" && b?["currency"]?.GetValue<string>() == account.Currency);
        return booked == null ? null : decimal.Parse(booked["amount"]!.GetValue<string>(), CultureInfo.InvariantCulture);
    }
    public static List<Entry> Entries(JsonArray input, Account account)
    {
        var output = new List<Entry>(); var occurrences = new Dictionary<string, int>();
        foreach (var n in input) {
            if (n == null) throw new InvalidDataException("Leere Buchung.");
            var amount = decimal.Parse(n["amount"]!["amount"]!.GetValue<string>(), CultureInfo.InvariantCulture);
            var currency = n["amount"]!["currency"]!.GetValue<string>();
            if (currency != account.Currency) throw new InvalidDataException("Fremdwährung in Kontoumsätzen; Abgleich abgebrochen.");
            var dateText = n["bookingDate"]?.GetValue<string>() ?? n["valueDate"]?.GetValue<string>() ?? n["transactionDate"]?.GetValue<string>();
            if (!DateOnly.TryParseExact(dateText, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date)) throw new InvalidDataException("Buchungsdatum fehlt oder ist ungültig.");
            var party = n[amount < 0 ? "creditor" : "debtor"];
            var merchant = party?["name"]?.GetValue<string>() ?? "Unbekannt";
            var purpose = n["remittanceInformation"] is JsonArray remittance ? string.Join(" ", remittance.Select(r => r?.GetValue<string>())) : "";
            var reference = n["accountServicerReference"]?.GetValue<string>() ?? n["entryReference"]?.GetValue<string>() ?? n["transactionId"]?.GetValue<string>();
            if (string.IsNullOrWhiteSpace(reference) || reference == "NOTPROVIDED") reference = null;
            var key = reference != null ? account.Id + "|ref|" + reference : JsonSerializer.Serialize(new { account.Id, date, amount, currency, merchant, purpose, iban = party?["iban"]?.GetValue<string>() });
            var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(key)));
            occurrences.TryGetValue(hash, out var count); occurrences[hash] = ++count;
            var id = hash + ":" + count;
            DateOnly? valueDate = DateOnly.TryParseExact(n["valueDate"]?.GetValue<string>(), "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var value) ? value : null;
            output.Add(new(id, account.Id, date, amount, currency, merchant, purpose, "Unkategorisiert",
                string.Equals(n["status"]?.GetValue<string>(),"Pending",StringComparison.OrdinalIgnoreCase),
                ValueDate: valueDate, CounterpartyIban: party?["iban"]?.GetValue<string>() ?? "", CounterpartyBic: party?["bic"]?.GetValue<string>() ?? "",
                AdditionalInformation: n["additionalInformation"]?.GetValue<string>() ?? "", PurposeCode: n["purposeCode"]?.GetValue<string>() ?? "", BankCounterpartyName: party?["name"]?.GetValue<string>() ?? "", EndToEndId: n["endToEndId"]?.GetValue<string>() ?? ""));
        }
        return output;
    }
}
