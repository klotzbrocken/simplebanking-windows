namespace SimpleBanking;
internal sealed partial class DashboardWindow
{
    IEnumerable<RecurringPayment> RecurringPayments()=>snapshot.Accounts.SelectMany(a=>{
        var entries=snapshot.Entries.Where(e=>e.AccountId==a.Id&&e.Currency==a.Currency&&!e.Pending&&e.Date<=DateOnly.FromDateTime(DateTime.Today)).ToArray();
        return RecurringCosts.Analyze(entries,a.BankName??a.Name).Where(p=>{
            string? assignment=RecurringAssignmentKeys.Find(owner.AnalysisAssignments,a.Id,p.GroupKey)??AssignmentRules.Sorted(owner.AnalysisRules).FirstOrDefault(r=>r.Recurring!=null&&entries.Where(e=>p.EntryIds.Contains(e.Id)).Any(e=>AssignmentRules.Matches(r,e,p.Frequency)))?.Recurring;
            return assignment!="Ausschließen";
        });
    });
    void FixedCostsPage(){
        var today=DateOnly.FromDateTime(DateTime.Today);
        var list=Table(["Händler / Konto","Betrag","Rhythmus","Nächster Termin","Status"]);
        decimal total=0;int known=0;
        foreach(var account in snapshot.Accounts){
            var summary=RecurringSummary.Calculate(snapshot.Entries,account,owner.Preferences.FinanceFor(account.Id),today,owner.AnalysisAssignments,owner.AnalysisRules);
            if(summary.Outstanding is {} due){total+=due;known++;}
            foreach(var item in summary.Payments){
                bool pending=summary.CycleStart is {} start&&summary.CycleEnd is {} end&&RecurringCosts.OutstandingPayments([item],start,end).Any();
                var next=item.Frequency==PaymentFrequency.Irregular?(DateOnly?)null:RecurringCosts.NextExpected(item);
                string status=item.Confidence<.6?"Noch nicht bestätigt":pending?"Im Gehaltszyklus offen":summary.CycleStart is {} cycle&&item.LastDate>=cycle?"Bereits gebucht":next<summary.CycleStart?"Keine aktuelle Wiederkehr":"Außerhalb des Gehaltszyklus";
                int row=list.Rows.Add(item.Merchant+(snapshot.Accounts.Length>1?" · "+account.Name:""),UiFormat.Money(item.AverageAmount,account.Currency),Subscriptions.Frequency(item.Frequency),next?.ToString("dd.MM.yyyy")??"—",status);
                list.Rows[row].Tag=(account.Id,item);
            }
        }
        void Details(){if(list.CurrentRow?.Tag is not ValueTuple<string,RecurringPayment> selected)return;using var details=new Form{Text=selected.Item2.Merchant,ClientSize=new Size(600,430),StartPosition=FormStartPosition.CenterParent};var table=Table(["Datum","Betrag","Verwendungszweck"]);foreach(var e in snapshot.Entries.Where(e=>e.AccountId==selected.Item1&&selected.Item2.EntryIds.Contains(e.Id)).OrderByDescending(e=>e.Date))table.Rows.Add(e.Date.ToString("dd.MM.yyyy"),UiFormat.Money(e.Amount,e.Currency),e.Purpose);details.Controls.Add(table);var remove=ActionButton("Aus Fixkosten und Abos ausschließen",()=>{owner.SetRecurringAssignment(selected.Item1+"/"+selected.Item2.GroupKey,"Ausschließen");details.Close();ShowTab(5);});remove.Dock=DockStyle.Bottom;details.Controls.Add(remove);details.ShowDialog(this);}
        list.CellDoubleClick+=(_,e)=>{if(e.RowIndex>=0)Details();};
        page.Controls.Add(list);page.Controls.Add(Header("Fixkosten",known==0?"Noch keine auswertbare Historie":UiFormat.Money(total,snapshot.Currency)+" im aktuellen Gehaltszyklus offen",ActionButton("Details",Details)));
    }
}
