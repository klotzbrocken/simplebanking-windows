namespace SimpleBanking;
internal sealed partial class MainWindow
{
    void LoadHistory(){
        if(!RequireUnlocked())return;using var window=new Form{Text="Ältere Umsätze laden",ClientSize=new Size(410,190),StartPosition=FormStartPosition.CenterParent,Font=Font};
        window.Controls.Add(new Label{Text="Umsätze ab dem gewählten Datum abrufen.\nDer verfügbare Zeitraum hängt von deiner Bank ab.",AutoSize=true,Location=new Point(20,20)});var date=new DateTimePicker{Value=DateTime.Today.AddYears(-1),MaxDate=DateTime.Today,MinDate=DateTime.Today.AddYears(-10),Location=new Point(20,75),Width=360};window.Controls.Add(date);var button=new Button{Text="Umsätze laden",DialogResult=DialogResult.OK,Location=new Point(230,125),Width=150};window.Controls.Add(button);window.AcceptButton=button;if(window.ShowDialog(this)==DialogResult.OK)RefreshBankRange(DateOnly.FromDateTime(date.Value));
    }
    void ResetDemo(){if(!demo){MessageBox.Show(this,"Diese Funktion ist nur in der Demo verfügbar.");return;}foreach(var form in OwnedForms.ToArray())form.Close();data=DemoData.Create();Preferences=new(){Ephemeral=true};Populate();ApplyPreferences();status.Text="DEMO · Beispieldaten zurückgesetzt";}
    internal void ReviewUnmatchedEnrichments(){
        if(!RequireUnlocked())return;using var window=new Form{Text="Nicht zugeordnete Buchungsdetails",ClientSize=new Size(720,440),StartPosition=FormStartPosition.CenterParent,Font=Font};var table=DashboardWindow.Table(["Datum","Händler","Betrag","Notiz / Anhänge"]);foreach(var e in data.UnmatchedEnrichments){int i=table.Rows.Add(e.Date.ToString("dd.MM.yyyy"),e.Merchant,UiFormat.Money(e.Amount,e.Currency),e.Note+" · "+(e.Attachments?.Count??0)+" Anhänge");table.Rows[i].Tag=e;}var view=DashboardWindow.ActionButton("Details ansehen / Anhänge exportieren",()=>{if(table.CurrentRow?.Tag is Entry entry){using var detail=new TransactionDetails(entry,false,changed=>{int index=data.UnmatchedEnrichments.FindIndex(e=>e.AccountId==changed.AccountId&&e.Id==changed.Id);if(index>=0&&Unlocked){data.UnmatchedEnrichments[index]=changed;Save();}});detail.ShowDialog(window);}});view.Dock=DockStyle.Bottom;window.Controls.Add(table);window.Controls.Add(view);window.ShowDialog(this);
    }
    internal int DueReminderCount()=>Unlocked?data.Entries.Count(e=>e.ReminderAt is {} date&&date<=DateTime.Now&&!e.ReminderDone):0;
}
