using System.Text.RegularExpressions;
namespace SimpleBanking;
public sealed record MerchantUserRule(string Id,bool Enabled,int Priority,string Scope,string MatchType,string Pattern,string Merchant);
internal static class UserMerchantRules
{
    internal static bool Matches(MerchantUserRule rule,Entry entry){
        if(!rule.Enabled||rule.Pattern.Trim().Length==0)return false;
        string name=entry.OriginalMerchant??entry.BankCounterpartyName??entry.Merchant;
        string text=rule.Scope switch{"Empfänger"=>name,"Verwendungszweck"=>entry.Purpose+" "+entry.AdditionalInformation,"End-to-End-ID"=>entry.EndToEndId,_=>name+" "+entry.Purpose+" "+entry.AdditionalInformation+" "+entry.EndToEndId};
        try{return rule.MatchType switch{"Regex"=>Regex.IsMatch(text,rule.Pattern,RegexOptions.IgnoreCase|RegexOptions.CultureInvariant,TimeSpan.FromMilliseconds(100)),"Ist"=>AssignmentRules.Normalize(text)==AssignmentRules.Normalize(rule.Pattern),_=>AssignmentRules.Normalize(text).Contains(AssignmentRules.Normalize(rule.Pattern))};}catch(RegexMatchTimeoutException){return false;}catch(ArgumentException){return false;}
    }
    internal static Entry Apply(Entry entry,IEnumerable<MerchantUserRule> rules){string original=entry.OriginalMerchant??entry.Merchant;if(!string.IsNullOrWhiteSpace(entry.MerchantOverride))return entry with{OriginalMerchant=original,Merchant=entry.MerchantOverride,MerchantSource="tx_override",MerchantConfidence=1};var rule=rules.OrderBy(r=>r.Priority).FirstOrDefault(r=>Matches(r,entry));return entry with{OriginalMerchant=original,Merchant=rule?.Merchant??original,MerchantSource=rule==null?null:"user_rule",MerchantConfidence=rule==null?null:.99};}
}
internal sealed partial class MainWindow
{
    internal void SaveMerchantRuleFromEntry(Entry entry){
        if(!RequireUnlocked())return;
        var suggestion=MerchantRuleSuggestion.From(entry);
        using var editor=new MerchantRuleEditor(new(Guid.NewGuid().ToString("N"),true,100,suggestion.Scope,"Enthält",suggestion.Pattern,entry.Merchant),data.Entries);
        if(editor.ShowDialog(this)!=DialogResult.OK||editor.Result is not {} rule)return;
        var previous=data.Entries.ToList();
        try{data.MerchantRules.Add(rule);ApplyMerchantRules();Save();Populate();}
        catch{data.MerchantRules.Remove(rule);data.Entries=previous;Populate();throw;}
    }
    void ApplyMerchantRules(){for(int i=0;i<data.Entries.Count;i++){var original=data.Entries[i];var resolved=UserMerchantRules.Apply(original,data.MerchantRules);if(string.IsNullOrWhiteSpace(original.MerchantOverride)&&!data.MerchantRules.Any(r=>UserMerchantRules.Matches(r,original))){var resolution=MerchantResolver.ResolveDetailed(original,data.Accounts.FirstOrDefault(a=>a.Id==original.AccountId),data.Accounts);resolved=resolved with{Merchant=resolution.Name,MerchantSource=resolution.Source,MerchantConfidence=resolution.Confidence};}if(!resolved.CategoryManual&&resolved.Category is "" or "Unkategorisiert")resolved=resolved with{Category=AutomaticCategories.Classify(resolved)};data.Entries[i]=MerchantLogoStore.Apply(resolved,data.MerchantLogos);}}
    void RenameMerchant(Entry entry){if(!RequireUnlocked())return;var name=Prompt("Händlername ändern","Anzeigename nur für diesen Umsatz",value:entry.Merchant);if(name==null)return;if(string.IsNullOrWhiteSpace(name)||name.Trim().Length>120){MessageBox.Show(this,"Bitte einen Namen mit 1 bis 120 Zeichen eingeben.");return;}SetMerchantOverride(entry,name.Trim());}
    void SetMerchantOverride(Entry entry,string? name){if(!RequireUnlocked())return;int index=data.Entries.FindIndex(e=>e.Id==entry.Id&&e.AccountId==entry.AccountId);if(index<0)return;var previous=data.Entries.ToList();try{data.Entries[index]=data.Entries[index] with{MerchantOverride=name};ApplyMerchantRules();Save();Populate();}catch{data.Entries=previous;Populate();throw;}}
    internal void OpenMerchantRules(){
        if(!RequireUnlocked())return;using var window=new Form{Text="Händler-Regeln",ClientSize=new Size(720,480),StartPosition=FormStartPosition.CenterParent,Font=Font};var table=DashboardWindow.Table(["Aktiv","Bedingung","Händler","Priorität"]);
        void Reload(){table.Rows.Clear();foreach(var r in data.MerchantRules.OrderBy(r=>r.Priority)){int row=table.Rows.Add(r.Enabled?"Ja":"Nein",r.Scope+" "+r.MatchType+" "+r.Pattern,r.Merchant,r.Priority);table.Rows[row].Tag=r;}}
        void Commit(Action change){var previousRules=data.MerchantRules.ToList();var previousEntries=data.Entries.ToList();try{change();ApplyMerchantRules();Save();Populate();Reload();}catch{data.MerchantRules=previousRules;data.Entries=previousEntries;Populate();Reload();throw;}}
        void Edit(MerchantUserRule? old){using var editor=new MerchantRuleEditor(old,data.Entries);if(editor.ShowDialog(window)!=DialogResult.OK||editor.Result is not {} rule)return;Commit(()=>{int index=data.MerchantRules.FindIndex(r=>r.Id==rule.Id);if(index<0)data.MerchantRules.Add(rule);else data.MerchantRules[index]=rule;});}
        table.CellDoubleClick+=(_,e)=>{if(e.RowIndex>=0&&table.Rows[e.RowIndex].Tag is MerchantUserRule rule)Edit(rule);};
        var actions=new FlowLayoutPanel{Dock=DockStyle.Top,AutoSize=true};actions.Controls.Add(DashboardWindow.ActionButton("Neue Regel",()=>Edit(null)));actions.Controls.Add(DashboardWindow.ActionButton("Bearbeiten",()=>{if(table.CurrentRow?.Tag is MerchantUserRule r)Edit(r);}));actions.Controls.Add(DashboardWindow.ActionButton("Aktivieren / Deaktivieren",()=>{if(table.CurrentRow?.Tag is MerchantUserRule r){int i=data.MerchantRules.IndexOf(r);Commit(()=>data.MerchantRules[i]=r with{Enabled=!r.Enabled});}}));actions.Controls.Add(DashboardWindow.ActionButton("Löschen",()=>{if(table.CurrentRow?.Tag is MerchantUserRule r){Commit(()=>data.MerchantRules.Remove(r));}}));window.Controls.Add(table);window.Controls.Add(actions);Reload();window.ShowDialog(this);
    }
}
