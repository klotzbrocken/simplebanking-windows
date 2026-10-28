namespace SimpleBanking;
internal sealed partial class MainWindow
{
    internal void ManageAutomationClients(){
        if(!RequireUnlocked())return;
        using var window=new Form{Text="CLI / MCP · Clients",ClientSize=new(760,440),StartPosition=FormStartPosition.CenterParent,Padding=new(16)};
        var table=DashboardWindow.Table(["Client","Berechtigungen","Gültig bis","Status"]);window.Controls.Add(table);
        void Reload(){table.Rows.Clear();foreach(var c in data.AutomationClients){int i=table.Rows.Add(c.Name,string.Join(", ",c.Scopes.Select(ScopeName)),c.ExpiresAt?.LocalDateTime.ToString("d")??"—",c.Revoked?"Widerrufen":c.ExpiresAt<DateTimeOffset.UtcNow?"Abgelaufen":"Aktiv");table.Rows[i].Tag=c;}}
        var actions=new FlowLayoutPanel{Dock=DockStyle.Bottom,AutoSize=true};
        actions.Controls.Add(DashboardWindow.ActionButton("Client hinzufügen",()=>{
            using var editor=new AutomationClientEditor(data.Accounts);if(editor.ShowDialog(window)!=DialogResult.OK||editor.Result is not {} result||!Unlocked)return;
            data.AutomationClients.Add(result.Client);try{Save();}catch{data.AutomationClients.Remove(result.Client);throw;}Reload();
            using var secret=new Form{Text="Client-Token · Einmalige Anzeige",ClientSize=new(650,280),StartPosition=FormStartPosition.CenterParent,Padding=new(18)};
            var text=new TextBox{Multiline=true,ReadOnly=true,Dock=DockStyle.Fill,Text="Token sicher beim MCP-Client hinterlegen. Er wird nicht erneut angezeigt.\r\n\r\nSIMPLEBANKING_TOKEN="+result.Token+"\r\n\r\nProgramm: "+Environment.ProcessPath+"\r\nArgument: --mcp"};secret.Controls.Add(text);
            secret.Controls.Add(new Label{Text="Einrichtung: Programm + --mcp, Umgebungsvariable SIMPLEBANKING_TOKEN",Dock=DockStyle.Top,AutoSize=true});
            var copy=DashboardWindow.ActionButton("Token kopieren",()=>Clipboard.SetText(result.Token));copy.Dock=DockStyle.Bottom;secret.Controls.Add(copy);secret.ShowDialog(window);
        }));
        actions.Controls.Add(DashboardWindow.ActionButton("Zugriff widerrufen",()=>{if(table.CurrentRow?.Tag is not AutomationClient c||!Unlocked)return;int i=data.AutomationClients.IndexOf(c);data.AutomationClients[i]=c with{Revoked=true};try{Save();}catch{data.AutomationClients[i]=c;throw;}Reload();}));
        window.Controls.Add(actions);Reload();window.ShowDialog(this);
    }
    internal static string ScopeName(string value)=>value switch{"accounts"=>"Konten und Salden","transactions"=>"Umsätze","analysis"=>"Auswertungen","drafts"=>"Überweisungsentwürfe",_=>value};
    internal void ReviewAutomationDrafts(){
        if(!RequireUnlocked())return;
        using var window=new Form{Text="MCP · Überweisungsentwürfe prüfen",ClientSize=new(800,460),StartPosition=FormStartPosition.CenterParent,Padding=new(16)};
        var table=DashboardWindow.Table(["Client","Empfänger","Betrag","Status"]);window.Controls.Add(table);
        void Reload(){table.Rows.Clear();foreach(var d in data.AutomationDrafts.OrderByDescending(d=>d.CreatedAt)){int row=table.Rows.Add(d.ClientName,d.Draft.Name,UiFormat.Money(d.Draft.Amount,"EUR"),d.State);table.Rows[row].Tag=d;}}
        void Review(){
            if(table.CurrentRow?.Tag is not AutomationDraft d||d.State!="Prüfung ausstehend"||!Unlocked)return;
            var account=data.Accounts.FirstOrDefault(a=>a.Id==d.Draft.AccountId);if(account==null)return;
            using var preview=new Form{Text="Entwurf von "+d.ClientName,ClientSize=new(590,320),StartPosition=FormStartPosition.CenterParent,Padding=new(20)};
            preview.Controls.Add(new TextBox{Multiline=true,ReadOnly=true,Dock=DockStyle.Fill,Text="Konto: "+account.Name+"\r\nEmpfänger: "+d.Draft.Name+"\r\nIBAN: "+d.Draft.Iban+"\r\nBetrag: "+UiFormat.Money(d.Draft.Amount,"EUR")+"\r\nZweck: "+d.Draft.Purpose+"\r\nTermin: "+(d.Draft.Date?.ToString("d")??"Sofort")+"\r\n\r\nFreigabe übernimmt nur den Entwurf. Die Banküberweisung wird separat geprüft und gesendet."});
            var buttons=new FlowLayoutPanel{Dock=DockStyle.Bottom,AutoSize=true};buttons.Controls.Add(DashboardWindow.ActionButton("Als Entwurf freigeben",()=>preview.DialogResult=DialogResult.OK));buttons.Controls.Add(DashboardWindow.ActionButton("Ablehnen",()=>preview.DialogResult=DialogResult.No));preview.Controls.Add(buttons);preview.Shown+=(_,_)=>preview.ActiveControl=buttons.Controls[0];
            var decision=preview.ShowDialog(window);if(!Unlocked||decision is not (DialogResult.OK or DialogResult.No))return;
            int index=data.AutomationDrafts.IndexOf(d);if(index<0)return;
            data.AutomationDrafts[index]=d with{State=decision==DialogResult.OK?"Als Entwurf freigegeben":"Abgelehnt"};if(decision==DialogResult.OK)data.Drafts.Add(d.Draft);
            try{Save();}catch{data.AutomationDrafts[index]=d;if(decision==DialogResult.OK)data.Drafts.RemoveAt(data.Drafts.Count-1);throw;}Reload();
        }
        table.CellDoubleClick+=(_,e)=>{if(e.RowIndex>=0)Review();};
        var actions=new FlowLayoutPanel{Dock=DockStyle.Bottom,AutoSize=true};actions.Controls.Add(DashboardWindow.ActionButton("Ausgewählten Entwurf prüfen",Review));
        actions.Controls.Add(DashboardWindow.ActionButton("Freigegebenen Entwurf öffnen",()=>{if(table.CurrentRow?.Tag is AutomationDraft d&&d.State=="Als Entwurf freigegeben"&&Unlocked){window.Hide();EditTransfer(d.Draft);if(!window.IsDisposed)window.Show();}}));
        window.Controls.Add(actions);Reload();window.ShowDialog(this);
    }
}
internal sealed class AutomationClientEditor : Form
{
    internal (AutomationClient Client,string Token)? Result {get;private set;}
    internal AutomationClientEditor(IReadOnlyList<Account> accounts){
        Text="MCP-Client hinzufügen";ClientSize=new(560,480);StartPosition=FormStartPosition.CenterParent;Padding=new(20);
        var layout=new FlowLayoutPanel{Dock=DockStyle.Fill,FlowDirection=FlowDirection.TopDown,WrapContents=false,AutoScroll=true};Controls.Add(layout);
        layout.Controls.Add(DashboardWindow.Label("Name des Clients",14,true));var name=new TextBox{Width=480,MaxLength=80,AccessibleName="Clientname"};layout.Controls.Add(name);
        layout.Controls.Add(DashboardWindow.Label("Berechtigungen",14,true));var scopes=new CheckedListBox{Width=480,Height=90,CheckOnClick=true};foreach(var scope in AutomationAccess.Scopes)scopes.Items.Add(MainWindow.ScopeName(scope),scope!="drafts");layout.Controls.Add(scopes);
        layout.Controls.Add(DashboardWindow.Label("Freigegebene Konten",14,true));var allowed=new CheckedListBox{Width=480,Height=90,CheckOnClick=true};foreach(var account in accounts)allowed.Items.Add(account.Name+" · "+account.Currency,false);layout.Controls.Add(allowed);
        var expiry=new NumericUpDown{Minimum=1,Maximum=365,Value=90,Width=100};layout.Controls.Add(DashboardWindow.Label("Gültigkeit in Tagen"));layout.Controls.Add(expiry);
        var error=new Label{AutoSize=true,MaximumSize=new(480,0),ForeColor=ExportDesign.Error};layout.Controls.Add(error);
        var save=DashboardWindow.ActionButton("Client anlegen",()=>{try{var selected=allowed.CheckedIndices.Cast<int>().Select(i=>accounts[i].Id).ToArray();if(selected.Length==0)throw new InvalidDataException("Bitte mindestens ein Konto auswählen.");Result=AutomationAccess.Create(name.Text,scopes.CheckedIndices.Cast<int>().Select(i=>AutomationAccess.Scopes[i]).ToArray(),selected,(int)expiry.Value);DialogResult=DialogResult.OK;}catch(InvalidDataException ex){error.Text=ex.Message;}});save.Dock=DockStyle.Bottom;Controls.Add(save);
    }
}
