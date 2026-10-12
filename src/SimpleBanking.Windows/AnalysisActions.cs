namespace SimpleBanking;
internal sealed partial class MainWindow
{
    DashboardWindow? dashboardWindow;
    internal (double Fraction,bool Dispo) RingState() {
        if(SelectedAccount is not {} account || !account.BalanceKnown)return (0,false);
        var finance=Preferences.FinanceFor(account.Id);
        if(account.Balance<0)return(finance.DispoLimit>0?Math.Clamp((double)(-account.Balance/finance.DispoLimit),0,1):0,true);
        decimal reference=finance.SalaryAmount;
        if(reference<=0){
            var cycle=RecurringCosts.Cycle(finance.EffectiveSalaryDay,DateOnly.FromDateTime(DateTime.Today));
            var current=cycle.Start.AddDays(-finance.SalaryTolerance);var previous=cycle.Start.AddMonths(-1).AddDays(-finance.SalaryTolerance);
            decimal Income(DateOnly from,DateOnly? until){
                var entries=data.Entries.Where(e=>e.AccountId==account.Id&&!e.Pending&&e.Amount>0&&e.Date<=DateOnly.FromDateTime(DateTime.Today)&&e.Date>=from&&(until==null||e.Date<until)).ToArray();
                var salary=entries.Where(e=>RecurringCosts.SalaryCredit(e)).Select(e=>e.Amount).DefaultIfEmpty().Max();
                return salary>0?salary:entries.Where(e=>e.Amount>=1000&&!new[]{"ERSTATTUNG","RETOURE","REFUND","RÜCKZAHLUNG"}.Any((e.Purpose+" "+e.AdditionalInformation).ToUpperInvariant().Contains)).Select(e=>e.Amount).DefaultIfEmpty().Max();
            }
            reference=Income(current,null);if(reference<=0)reference=Income(previous,current);if(reference<=0)reference=Preferences.Medium;
        }
        return(reference>0?Math.Clamp((double)(account.Balance/reference),0,1):0,false);
    }
    internal IEnumerable<Entry> RulePreviewEntries(bool allAccounts)=>allAccounts?data.Entries:AnalysisSnapshot().Entries;
    internal IReadOnlyList<AssignmentRule> AnalysisRules=>data.Rules;
    internal IReadOnlyDictionary<string,string> AnalysisAssignments=>data.RecurringAssignments;
    internal AnalysisSnapshot AnalysisSnapshot(){var list=unifiedAccounts?data.Accounts.ToArray():SelectedAccount is {} a?new[]{a}:Array.Empty<Account>();var ids=list.Select(a=>a.Id).ToHashSet();return new(unifiedAccounts?"Alle Konten":list.FirstOrDefault()?.Name??"Konten",list,data.Entries.Where(e=>ids.Contains(e.AccountId)).ToArray(),CurrentBalance);}
    internal void OpenRuleAssistant(){if(!RequireUnlocked())return;using var assistant=new RuleAssistant(this,AnalysisSnapshot());assistant.ShowDialog(this);}
    internal void ChangeMasterPassword(){
        if(!RequireUnlocked() || demo)return;
        int epoch=Volatile.Read(ref securityEpoch);
        var next=Prompt("App-Passwort ändern","Neues Passwort (mindestens 6 Zeichen)",true);if(next==null)return;
        if(next.Length<6){MessageBox.Show(this,"Bitte mindestens 6 Zeichen eingeben.");return;}
        if(Prompt("App-Passwort bestätigen","Neues Passwort wiederholen",true)!=next){MessageBox.Show(this,"Die Passwörter stimmen nicht überein.");return;}
        if(epoch!=Volatile.Read(ref securityEpoch)||!Unlocked)return;
        try{Hello.Disable();store.Save(data,next);password=next;status.Text="App-Passwort geändert";MessageBox.Show(this,"Passwort geändert. Falls gewünscht, Windows Hello unter Sicherheit neu einrichten.");}catch(IOException){MessageBox.Show(this,"Das Passwort konnte nicht gespeichert werden. Das bisherige Passwort bleibt gültig.");}
    }
    internal void OpenDashboard(int tab=0,bool fixedCosts=false){if(!RequireUnlocked())return;if(dashboardWindow==null||dashboardWindow.IsDisposed)dashboardWindow=new DashboardWindow(this,tab);else dashboardWindow.ShowTab(tab);if(fixedCosts)dashboardWindow.ShowFixedCosts();dashboardWindow.Show(this);dashboardWindow.Activate();}
    internal IEnumerable<Entry> MatchRule(AssignmentRule rule,IEnumerable<Entry> entries){var list=entries.ToArray();var frequencies=list.GroupBy(e=>e.AccountId).SelectMany(g=>RecurringCosts.Analyze(g).SelectMany(p=>p.EntryIds.Select(id=>(Key:g.Key+"/"+id,Frequency:p.Frequency)))).GroupBy(p=>p.Key).ToDictionary(g=>g.Key,g=>g.First().Frequency);return list.Where(e=>AssignmentRules.Matches(rule,e,frequencies.TryGetValue(e.AccountId+"/"+e.Id,out var f)?f:null));}
    internal void SaveAnalysisRule(AssignmentRule rule,bool apply,HashSet<string>? accounts){var previousRules=data.Rules.ToList();var previousEntries=data.Entries.ToList();var previousAssignments=new Dictionary<string,string>(data.RecurringAssignments);try{int i=data.Rules.FindIndex(r=>r.Id==rule.Id);if(i<0)data.Rules.Add(rule);else data.Rules[i]=rule;if(apply)ApplyAnalysisRules(accounts);Save();Populate();}catch{data.Rules=previousRules;data.Entries=previousEntries;data.RecurringAssignments=previousAssignments;Populate();throw;}}
    internal void DeleteAnalysisRule(string id){data.Rules.RemoveAll(r=>r.Id==id);Save();Populate();}
    internal void SetRecurringAssignment(string key,string value){
        var account=data.Accounts.FirstOrDefault(a=>key.StartsWith(a.Id+"/",StringComparison.Ordinal));
        if(account==null)return;
        RecurringAssignmentKeys.Set(data.RecurringAssignments,account.Id,key[(account.Id.Length+1)..],value);Save();Populate();
    }
    internal void ResetRecurringAssignment(string key){data.RecurringAssignments.Remove(key);Save();Populate();}
    void ApplyAnalysisRules(HashSet<string>? accounts=null,bool preserveManual=false){
        ApplyMerchantRules();
        var scope=data.Entries.Where(e=>accounts==null||accounts.Contains(e.AccountId)).ToArray();var applied=new HashSet<string>();var recurringApplied=new HashSet<string>();
        foreach(var rule in AssignmentRules.Sorted(data.Rules))foreach(var entry in MatchRule(rule,scope)){
            string key=entry.AccountId+"/"+entry.Id;int i=data.Entries.FindIndex(e=>e.AccountId==entry.AccountId&&e.Id==entry.Id);
            if(rule.Category!=null&&(!preserveManual||!entry.CategoryManual)&&applied.Add(key))data.Entries[i]=data.Entries[i] with{Category=rule.Category};
            if(rule.Recurring!=null){var match=Subscriptions.Detect(scope.Where(e=>e.AccountId==entry.AccountId)).FirstOrDefault(s=>s.Entries.Any(e=>e.Id==entry.Id));if(match!=null&&(!preserveManual||RecurringAssignmentKeys.Find(data.RecurringAssignments,entry.AccountId,match.Id)==null)&&recurringApplied.Add(entry.AccountId+"/"+RecurringAssignmentKeys.Canonical(match.Id)))RecurringAssignmentKeys.Set(data.RecurringAssignments,entry.AccountId,match.Id,rule.Recurring);}
        }
    }
}
