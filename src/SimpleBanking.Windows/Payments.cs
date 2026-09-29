using System.Globalization;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;
namespace SimpleBanking;
public sealed record PaymentRecord(string Id,TransferDraft Draft,DateTimeOffset CreatedAt,string State,bool Demo,string? TicketId=null);
public sealed record PaymentTemplate(string Id,string Name,TransferDraft Draft,bool Favorite=false,string Symbol="");
internal static class SimpleSendLicense
{
    internal static async Task Validate(string key,CancellationToken cancellation){
        if(string.IsNullOrWhiteSpace(key))throw new InvalidOperationException("Bitte simplesend unter Einstellungen → Extras aktivieren.");
        using var client=new HttpClient{Timeout=TimeSpan.FromSeconds(15)};client.DefaultRequestHeaders.Add("Polar-Version","2026-04");
        using var response=await client.PostAsJsonAsync("https://api.polar.sh/v1/customer-portal/license-keys/validate",new{key,organization_id="2f70b809-473d-4d9b-9be6-8209fcd45973"},cancellation);
        if(!response.IsSuccessStatusCode)throw new InvalidOperationException("Lizenz konnte nicht bestätigt werden (HTTP "+(int)response.StatusCode+").");
        var body=await response.Content.ReadFromJsonAsync<JsonObject>(cancellation);if(body?["status"]?.GetValue<string>()!="granted")throw new InvalidOperationException("Die Lizenz ist nicht freigeschaltet.");
        if(body["expires_at"] is {} expiry&&DateTimeOffset.Parse(expiry.GetValue<string>(),CultureInfo.InvariantCulture)<DateTimeOffset.UtcNow)throw new InvalidOperationException("Die Lizenz ist abgelaufen.");
    }
}
internal sealed partial class MainWindow
{
    internal async void ActivateSimpleSend(){
        if(!RequireUnlocked())return;if(demo){MessageBox.Show(this,"simplesend ist im Demo-Modus als Simulation verfügbar. Es werden keine Zahlungs- oder Lizenzanfragen gesendet.");return;}
        string? key=Prompt("simplesend aktivieren","Lizenzschlüssel",true);if(string.IsNullOrWhiteSpace(key))return;
        try{await SimpleSendLicense.Validate(key,CancellationToken.None);if(!Unlocked)return;data.SimpleSendLicense=key;Save();MessageBox.Show(this,"Lizenz bestätigt. Für den Versand muss das separate Transfer-Schlüsselpaar im Build enthalten sein.");}catch(Exception ex) when(ex is HttpRequestException or TaskCanceledException or InvalidOperationException or JsonException){MessageBox.Show(this,ex.Message,"Aktivierung nicht abgeschlossen");}
    }
    internal void OpenPayments(){
        if(!RequireUnlocked())return;using var window=new Form{Text=demo?"simplesend · Demo-Journal":"simplesend · Zahlungsjournal",ClientSize=new Size(790,490),StartPosition=FormStartPosition.CenterParent,Font=Font};var table=DashboardWindow.Table(["Datum","Empfänger","Betrag","Status"]);
        void Reload(){table.Rows.Clear();foreach(var p in data.Payments.OrderByDescending(p=>p.CreatedAt)){int i=table.Rows.Add(p.CreatedAt.LocalDateTime.ToString("g"),p.Draft.Name,UiFormat.Money(p.Draft.Amount,"EUR"),p.State+(p.Demo?" · DEMO":""));table.Rows[i].Tag=p;}}
        var actions=new FlowLayoutPanel{Dock=DockStyle.Top,AutoSize=true};actions.Controls.Add(DashboardWindow.ActionButton("Geld senden",()=>{Transfer();Reload();}));actions.Controls.Add(DashboardWindow.ActionButton("Vorlagen",OpenPaymentTemplates));actions.Controls.Add(DashboardWindow.ActionButton("Details",()=>{if(table.CurrentRow?.Tag is PaymentRecord p)MessageBox.Show(window,p.Draft.Name+"\n"+p.Draft.Iban+"\n"+UiFormat.Money(p.Draft.Amount,"EUR")+"\n"+p.Draft.Purpose+"\nAusführung: "+(p.Draft.Date?.ToString("dd.MM.yyyy")??"sofort")+"\nEnd-to-End-ID: "+p.Id+"\n"+p.State,"Zahlungsauftrag");}));window.Controls.Add(table);window.Controls.Add(actions);Reload();window.ShowDialog(this);
    }
    internal void OpenPaymentTemplates(){
        if(!RequireUnlocked())return;using var window=new Form{Text="simplesend · Vorlagen",ClientSize=new Size(650,420),StartPosition=FormStartPosition.CenterParent,Font=Font};var table=DashboardWindow.Table(["Vorlage","Empfänger","Betrag"]);
        void Reload(){table.Rows.Clear();foreach(var p in data.PaymentTemplates.Where(p=>p.Draft.AccountId==SelectedAccount?.Id)){int i=table.Rows.Add((p.Favorite?"★ ":"")+p.Name,p.Draft.Name,UiFormat.Money(p.Draft.Amount,"EUR"));table.Rows[i].Tag=p;}}
        void Edit(PaymentTemplate? original){if(SelectedAccount is not {} account)return;using var editor=new PaymentTemplateEditor(account.Id,original,data.PaymentTemplates.Count(p=>p.Favorite&&p.Draft.AccountId==account.Id&&p.Id!=original?.Id));if(editor.ShowDialog(window)!=DialogResult.OK||editor.Result is not {} result)return;int index=original==null?-1:data.PaymentTemplates.IndexOf(original);if(index<0)data.PaymentTemplates.Add(result);else data.PaymentTemplates[index]=result;try{Save();}catch{if(index<0)data.PaymentTemplates.Remove(result);else data.PaymentTemplates[index]=original!;throw;}Reload();RefreshEntries();}
        table.CellDoubleClick+=(_,e)=>{if(e.RowIndex>=0&&table.Rows[e.RowIndex].Tag is PaymentTemplate p)Edit(p);};
        var actions=new FlowLayoutPanel{Dock=DockStyle.Bottom,AutoSize=true};actions.Controls.Add(DashboardWindow.ActionButton("Neu",()=>Edit(null)));actions.Controls.Add(DashboardWindow.ActionButton("Bearbeiten",()=>{if(table.CurrentRow?.Tag is PaymentTemplate p)Edit(p);}));actions.Controls.Add(DashboardWindow.ActionButton("Verwenden",()=>{if(table.CurrentRow?.Tag is PaymentTemplate p){window.Close();EditTransfer(p.Draft with{Date=null});}}));actions.Controls.Add(DashboardWindow.ActionButton("Favorit umschalten",()=>{if(table.CurrentRow?.Tag is PaymentTemplate p){if(!p.Favorite&&data.PaymentTemplates.Count(t=>t.Favorite&&t.Draft.AccountId==p.Draft.AccountId)>=4){MessageBox.Show(window,"Bis zu vier Favoriten pro Konto.");return;}int index=data.PaymentTemplates.IndexOf(p);data.PaymentTemplates[index]=p with{Favorite=!p.Favorite};try{Save();}catch{data.PaymentTemplates[index]=p;throw;}Reload();RefreshEntries();}}));actions.Controls.Add(DashboardWindow.ActionButton("Entfernen",()=>{if(table.CurrentRow?.Tag is PaymentTemplate p){int index=data.PaymentTemplates.IndexOf(p);data.PaymentTemplates.Remove(p);try{Save();}catch{data.PaymentTemplates.Insert(index,p);throw;}Reload();RefreshEntries();}}));window.Controls.Add(table);window.Controls.Add(actions);Reload();window.ShowDialog(this);
    }
    void EditTransfer(TransferDraft? initial){
        if(!RequireUnlocked()||SelectedAccount is not {} account)return;if(account.Currency!="EUR"){MessageBox.Show(this,"simplesend unterstützt SEPA-Überweisungen in EUR. Bitte ein EUR-Konto auswählen.");return;}
        using var form=new TransferWindow(account,initial,demo);if(form.ShowDialog(this)!=DialogResult.OK||form.Draft is not {} draft){if(initial!=null)ReleaseRoundup(initial);return;}
        if(form.SaveTemplate){ReleaseRoundup(draft);draft=draft with{RoundupId=null};string? name=Prompt("Vorlage speichern","Name der Vorlage");if(!string.IsNullOrWhiteSpace(name)){data.PaymentTemplates.Add(new(Guid.NewGuid().ToString("N"),name,draft));Save();}return;}
        if(!form.SendRequested){ReleaseRoundup(draft);draft=draft with{RoundupId=null};data.Drafts.Add(draft);Save();status.Text="Überweisungsentwurf gespeichert";return;}SendPayment(draft);
    }
    async void SendPayment(TransferDraft draft)=>await SubmitPayment(draft,false);
    internal async Task<string> SubmitPayment(TransferDraft draft,bool inlineConfirmed){
        if(!RequireUnlocked()||bankBusy)return "Nicht gesendet: Konto gesperrt oder Bankvorgang läuft.";var account=data.Accounts.SingleOrDefault(a=>a.Id==draft.AccountId);if(account==null||account.Currency!="EUR"||BankingRules.Validate(draft)!=null)return "Nicht gesendet: Überweisungsdaten ungültig.";
        if(data.Payments.Any(p=>!p.Demo&&p.Draft==draft&&p.State is "Übermittlung begonnen" or "Ausgang unklar")){const string unclear="Bankausgang unklar. Bitte zuerst direkt bei der Bank prüfen. Es wird nicht erneut gesendet.";if(!inlineConfirmed)MessageBox.Show(this,unclear);return unclear;}
        if(draft.RoundupId is {} roundupId&&!data.Roundups.Any(p=>p.Id==roundupId&&p.AccountId==draft.AccountId&&p.Amount==draft.Amount&&p.SavingsIban==draft.Iban&&p.Recipient==draft.Name&&p.State=="Vorbereitet"))return "Nicht gesendet: Auftrag ist nicht mehr vorbereitet.";
        if(!inlineConfirmed&&MessageBox.Show(this,(demo?"DEMO — keine echte Zahlung\n\n":"Überweisung verbindlich bei der Bank einreichen?\n\n")+draft.Name+"\n"+draft.Iban+"\n"+UiFormat.Money(draft.Amount,"EUR")+"\n"+draft.Purpose+"\nAusführung: "+(draft.Date?.ToString("dd.MM.yyyy")??"sofort"),"simplesend · Prüfen",MessageBoxButtons.OKCancel)!=DialogResult.OK){ReleaseRoundup(draft);return "Abgebrochen · nichts gesendet.";}
        var record=new PaymentRecord("SB"+Guid.NewGuid().ToString("N")[..30],draft,DateTimeOffset.UtcNow,"Vorbereitet",demo);
        if(demo){UpdateRoundupPayment(draft,"Auszahlung simuliert");data.Payments.Add(record with{State=draft.Date.HasValue?"Terminauftrag simuliert":"Bankannahme simuliert"});status.Text="DEMO · Auftrag simuliert · kein Netzwerkaufruf";if(!inlineConfirmed)OpenPayments();return status.Text;}
        if(data.Yaxi is not {} settings||data.Connections.FirstOrDefault(c=>c.Id==account.ConnectionId) is not {} connection){ReleaseRoundup(draft);const string missing="Nicht gesendet: Für dieses Konto fehlt eine aktive Bankverbindung.";if(!inlineConfirmed)MessageBox.Show(this,missing);return missing;}
        bankBusy=true;bool began=false;string outcome="";try{
            using var timeout=new CancellationTokenSource(TimeSpan.FromMinutes(5));bankCancellation=timeout;
            var keys=EmbeddedYaxiKeys.Load();if(string.IsNullOrWhiteSpace(keys.TransferKeyId)||string.IsNullOrWhiteSpace(keys.TransferSecretBase64))throw new InvalidOperationException("Das separate simplesend-Transfer-Schlüsselpaar fehlt in diesem Build.");
            await SimpleSendLicense.Validate(data.SimpleSendLicense??"",timeout.Token);timeout.Token.ThrowIfCancellationRequested();
            using var client=new YaxiClient(settings.Integration);using var tickets=new TicketClient(settings);using var callback=new LoopbackCallback();string ticket=await tickets.IssueTransferAsync(true,timeout.Token);
            record=record with{State="Übermittlung begonnen",TicketId=TicketClient.DecodeJwt(ticket)["data"]?["id"]?.GetValue<string>()};data.Payments.Add(record);UpdateRoundupPayment(draft,"Übermittlung begonnen");Save();began=true;
            var request=new JsonObject{["op"]="transfer",["ticket"]=ticket,["debtorIban"]=account.Iban,["debtorCurrency"]="EUR",["credentials"]=new JsonObject{["connectionId"]=connection.ProviderId,["userId"]=connection.UserId,["password"]=connection.Password,["connectionData"]=connection.ConnectionData==null?null:JsonSerializer.SerializeToNode(connection.ConnectionData)},["transfer"]=new JsonObject{["amount"]=draft.Amount.ToString("0.00",CultureInfo.InvariantCulture),["currency"]="EUR",["iban"]=draft.Iban,["name"]=draft.Name,["purpose"]=draft.Purpose,["endToEndId"]=record.Id}};
            if(draft.Date is {} date)request["executionUnix"]=new DateTimeOffset(date.ToDateTime(new TimeOnly(12),DateTimeKind.Local)).ToUnixTimeSeconds();
            var response=await new BankFlow(client).RunAsync(request,"Transfer",r=>BankDialogs.Prompt(this,client,ticket,callback,r,timeout.Token),timeout.Token);
            if(response["connectionData"] is JsonArray){var index=data.Connections.FindIndex(c=>c.Id==connection.Id);data.Connections[index]=BankReadAccess.UpdatedConsent(connection,response);}
            data.Payments[data.Payments.FindIndex(p=>p.Id==record.Id)]=record with{State="Von Bank angenommen"};UpdateRoundupPayment(draft,"Übertragen");Save();status.Text="Überweisungsauftrag von der Bank angenommen";outcome=status.Text;
        }catch(Exception ex){if(began){data.Payments[data.Payments.FindIndex(p=>p.Id==record.Id)]=record with{State="Ausgang unklar"};UpdateRoundupPayment(draft,"Ausgang unklar");try{Save();}catch(IOException){}status.Text="Bankausgang unklar — Journal und Bank prüfen, nicht erneut senden.";}else {ReleaseRoundup(draft);status.Text="Nicht gesendet: "+(ex is InvalidOperationException?ex.Message:ex.GetType().Name);}outcome=status.Text;if(!inlineConfirmed)MessageBox.Show(this,status.Text,"simplesend");}
        finally{bankBusy=false;bankCancellation=null;if(forceLockPending){forceLockPending=false;Lock();}}
        return outcome;
    }
}
