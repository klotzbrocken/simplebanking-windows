using Microsoft.Identity.Client;
namespace SimpleBanking;

internal sealed partial class MainWindow
{
    readonly System.Windows.Forms.Timer todoTimer=new(){Interval=60000};
    CancellationTokenSource? todoCancellation;
    bool todoBusy;
    string todoStatus="Noch nicht synchronisiert.";
    void InitializeTodo(){todoTimer.Tick+=async(_,_)=>await SynchronizeTodo();todoTimer.Start();Disposed+=(_,_)=>{todoTimer.Dispose();todoCancellation?.Cancel();};}
    void ScheduleTodo(){if(!demo&&!todoBusy&&data.ToDo.Enabled)todoTimer.Interval=1000;}
    void SaveTodo(){if(!demo&&password!=null)store.Save(data,password);}
    static string TodoError(Exception e)=>e is MsalUiRequiredException?"Bitte Microsoft To Do erneut verbinden.":e is MsalException?"Microsoft-Anmeldung nicht abgeschlossen. Bitte erneut versuchen.":e is HttpRequestException?"Keine Verbindung zu Microsoft. Der Abgleich wird später wiederholt.":e is IOException?"Die Verbindung konnte nicht lokal gespeichert werden. Bitte freien Speicher prüfen.":e is InvalidOperationException or InvalidDataException?e.Message:"Microsoft To Do konnte nicht synchronisiert werden.";
    async Task SynchronizeTodo(){
        if(todoBusy||!Unlocked||!data.ToDo.Enabled||demo)return;
        todoBusy=true;todoTimer.Interval=60000;todoCancellation?.Dispose();todoCancellation=new();todoCancellation.CancelAfter(TimeSpan.FromMinutes(3));
        var token=todoCancellation.Token;var snapshot=data;int epoch=securityEpoch;
        void Guard(){token.ThrowIfCancellationRequested();if(IsDisposed||IsLocked||epoch!=securityEpoch||!ReferenceEquals(snapshot,data))throw new OperationCanceledException();}
        try{
            todoStatus="Erinnerungen werden abgeglichen …";
            var auth=await TodoIdentity.Authorize(data.ToDo,false,token);Guard();var previous=data.ToDo;data.ToDo=auth.Connection;try{SaveTodo();}catch{data.ToDo=previous;throw;}
            using var graph=new TodoGraph(auth.Token);await TodoSync.Run(data,graph,SaveTodo,Guard,token);Guard();
            var old=data.ToDo.LastSync;data.ToDo.LastSync=DateTimeOffset.Now;try{SaveTodo();}catch{data.ToDo.LastSync=old;throw;}
            todoStatus="Zuletzt abgeglichen: "+data.ToDo.LastSync.Value.ToLocalTime().ToString("g");RefreshEntries();
        }catch(OperationCanceledException){todoStatus="Abgleich unterbrochen. Lokale Änderungen bleiben gespeichert.";}catch(Exception e){todoStatus=TodoError(e);}finally{todoBusy=false;}
    }

    internal void OpenTodoSettings(){if(!RequireUnlocked())return;using var window=new TodoSettingsWindow(this);window.ShowDialog(this);}

    internal sealed class TodoSettingsWindow:Form
    {
        readonly MainWindow main;
        readonly Label state=new(){AutoSize=true,MaximumSize=new(490,0),Margin=new(0,8,0,12)};
        readonly Label details=new(){AutoSize=true,MaximumSize=new(490,0),Margin=new(0,4,0,12)};
        readonly Button connect=DashboardWindow.ActionButton("Mit Microsoft verbinden",()=>{});
        readonly Button sync=DashboardWindow.ActionButton("Jetzt abgleichen",()=>{});
        readonly Button disconnect=DashboardWindow.ActionButton("Trennen",()=>{});
        readonly Button configure=DashboardWindow.ActionButton("App-ID hinterlegen …",()=>{});
        readonly Button review=DashboardWindow.ActionButton("Verbindung prüfen …",()=>{});
        readonly Button switchAccount=DashboardWindow.ActionButton("Anderes Konto …",()=>{});
        readonly CheckBox enabled=new(){AutoSize=true,Text="Erinnerungen automatisch synchronisieren",Margin=new(0,8,0,8)};
        readonly System.Windows.Forms.Timer refresh=new(){Interval=500};
        readonly CancellationTokenSource lifetime=new();
        bool working;
        internal TodoSettingsWindow(MainWindow owner){
            main=owner;Text="Microsoft To Do";ClientSize=new(540,340);FormBorderStyle=FormBorderStyle.FixedDialog;MaximizeBox=MinimizeBox=false;StartPosition=FormStartPosition.CenterParent;AutoScaleMode=AutoScaleMode.Dpi;AutoScaleDimensions=new(96,96);
            var panel=new FlowLayoutPanel{Dock=DockStyle.Fill,FlowDirection=FlowDirection.TopDown,WrapContents=false,Padding=new(22),AutoScroll=true};Controls.Add(panel);
            panel.Controls.Add(DashboardWindow.Label("Erinnerungen auch in Microsoft To Do",17,true));
            details.Text="Nach dem Aktivieren werden Händlername, Erinnerungszeit und Erledigt-Status in deine gewählte To-Do-Liste übertragen. Änderungen an Zeit und Status werden in beide Richtungen abgeglichen. Beträge, IBANs, Notizen und Belege bleiben lokal.";panel.Controls.Add(details);panel.Controls.Add(state);panel.Controls.Add(enabled);
            var actions=new FlowLayoutPanel{AutoSize=true,MaximumSize=new(490,0),WrapContents=true};actions.Controls.AddRange([connect,sync,disconnect,configure,review,switchAccount]);panel.Controls.Add(actions);
            connect.Click+=async(_,_)=>await Connect();sync.Click+=async(_,_)=>{await main.SynchronizeTodo();Render();};
            enabled.Click+=(_,_)=>{bool old=main.data.ToDo.Enabled;main.data.ToDo.Enabled=enabled.Checked;try{main.SaveTodo();main.ScheduleTodo();}catch{main.data.ToDo.Enabled=old;throw;}Render();};
            disconnect.Click+=(_,_)=>{
                if(MessageBox.Show(this,"Die Anmeldung wird entfernt. Vorhandene Aufgaben in Microsoft To Do und lokale Erinnerungen bleiben erhalten.","Verbindung trennen",MessageBoxButtons.OKCancel)!=DialogResult.OK)return;
                var old=main.data.ToDo;main.data.ToDo=new(){ClientId=old.ClientId,AccountId=old.AccountId,AccountName=old.AccountName,ListId=old.ListId,ListName=old.ListName,Id=old.Id};try{main.SaveTodo();}catch{main.data.ToDo=old;throw;}main.todoStatus="Verbindung getrennt.";Render();
            };
            configure.Click+=(_,_)=>Configure();review.Click+=(_,_)=>Review();refresh.Tick+=(_,_)=>Render();refresh.Start();FormClosed+=(_,_)=>lifetime.Cancel();Render();
            switchAccount.Click+=(_,_)=>{if(MessageBox.Show(this,"Die bisherigen Microsoft-Aufgaben bleiben erhalten. Nach der Anmeldung mit einem anderen Konto werden Erinnerungen dort neu angelegt.","Microsoft-Konto wechseln",MessageBoxButtons.OKCancel)!=DialogResult.OK)return;var old=main.data.ToDo;main.data.ToDo=new(){ClientId=old.ClientId};try{main.SaveTodo();}catch{main.data.ToDo=old;throw;}main.todoStatus="Bitte mit dem neuen Microsoft-Konto verbinden.";Render();};
        }
        protected override void Dispose(bool disposing){if(disposing){refresh.Dispose();lifetime.Cancel();}base.Dispose(disposing);}
        void Render(){if(IsDisposed)return;var c=main.data.ToDo;bool connected=c.TokenCache is {Length:>0};bool busy=working||main.todoBusy;
            state.Text=main.demo?"Demo · Die Microsoft-Anbindung bleibt offline.":!Guid.TryParse(c.ClientId.Length>0?c.ClientId:TodoIdentity.DefaultClientId,out _)?"Für diese Beta fehlt noch die einmalige Microsoft-App-Registrierung.":connected?c.AccountName+" · "+c.ListName+"\n"+main.todoStatus:"Nicht verbunden. "+main.todoStatus;
            enabled.Checked=c.Enabled;enabled.Enabled=connected&&c.ListId.Length>0&&!busy;connect.Enabled=!busy&&!main.demo;connect.Text=connected?"Erneut anmelden":"Mit Microsoft verbinden";sync.Enabled=connected&&c.Enabled&&!busy&&!main.demo;disconnect.Enabled=connected&&!busy;configure.Enabled=!connected&&!busy;review.Enabled=!busy;
            switchAccount.Visible=!connected&&c.AccountId.Length>0;switchAccount.Enabled=!busy&&!main.demo;
        }
        void Configure(){
            using var dialog=new Form{Text="Microsoft-App einrichten",ClientSize=new(470,190),StartPosition=FormStartPosition.CenterParent,FormBorderStyle=FormBorderStyle.FixedDialog,MaximizeBox=false,MinimizeBox=false};
            dialog.Controls.Add(new Label{Text="Öffentliche Anwendungs-ID der simplebanking-Registrierung.\nDiese einmalige Einrichtung übernimmt der Herausgeber.",AutoSize=true,Location=new(18,18)});
            var id=new TextBox{Text=main.data.ToDo.ClientId.Length>0?main.data.ToDo.ClientId:TodoIdentity.DefaultClientId,Bounds=new(18,70,434,28)};
            dialog.Controls.Add(id);var save=DashboardWindow.ActionButton("Speichern",()=>{});save.Location=new(330,128);dialog.Controls.Add(save);
            save.Click+=(_,_)=>{if(!Guid.TryParse(id.Text.Trim(),out var clientId)){MessageBox.Show(dialog,"Bitte eine gültige Anwendungs-ID eingeben.");return;}string old=main.data.ToDo.ClientId;main.data.ToDo.ClientId=clientId.ToString();try{main.SaveTodo();}catch{main.data.ToDo.ClientId=old;throw;}dialog.DialogResult=DialogResult.OK;};dialog.ShowDialog(this);Render();
        }
        async Task Connect(){
            if(main.demo||working||main.todoBusy)return;
            var old=main.data.ToDo;
            if(!Guid.TryParse(old.ClientId.Length>0?old.ClientId:TodoIdentity.DefaultClientId,out _)){Configure();return;}
            working=true;main.todoBusy=true;Render();var snapshot=main.data;int epoch=main.securityEpoch;
            using var cancellation=CancellationTokenSource.CreateLinkedTokenSource(lifetime.Token);cancellation.CancelAfter(TimeSpan.FromMinutes(3));
            void Guard(){cancellation.Token.ThrowIfCancellationRequested();if(main.IsLocked||main.securityEpoch!=epoch||!ReferenceEquals(snapshot,main.data))throw new OperationCanceledException();}
            try{
                var input=new TodoConnection{ClientId=old.ClientId.Length>0?old.ClientId:TodoIdentity.DefaultClientId,AccountId=old.AccountId,Id=old.Id,ListId=old.ListId,ListName=old.ListName,TokenCache=old.TokenCache};
                var auth=await TodoIdentity.Authorize(input,true,cancellation.Token);Guard();
                using var graph=new TodoGraph(auth.Token);var lists=await graph.Collection(TodoGraph.Lists,cancellation.Token);Guard();
                using var choose=new Form{Text="To-Do-Liste wählen",ClientSize=new(450,172),StartPosition=FormStartPosition.CenterParent,FormBorderStyle=FormBorderStyle.FixedDialog,MaximizeBox=false,MinimizeBox=false};
                choose.Controls.Add(new Label{Text="In dieser Liste werden Buchungserinnerungen angelegt.",AutoSize=true,Location=new(18,18)});
                var combo=new ComboBox{DropDownStyle=ComboBoxStyle.DropDownList,DisplayMember="Value",Bounds=new(18,52,414,28)};
                foreach(var list in lists)combo.Items.Add(new KeyValuePair<string,string>(list["id"]!.GetValue<string>(),list["displayName"]?.GetValue<string>()??"Liste"));
                if(combo.Items.Count>0)combo.SelectedIndex=Math.Max(0,lists.FindIndex(l=>l["id"]?.GetValue<string>()==old.ListId));choose.Controls.Add(combo);
                var select=DashboardWindow.ActionButton("Liste verwenden",()=>{if(combo.SelectedIndex>=0)choose.DialogResult=DialogResult.OK;});select.Location=new(280,112);choose.Controls.Add(select);
                var create=DashboardWindow.ActionButton("Neue Liste",()=>{});create.Location=new(18,112);choose.Controls.Add(create);
                create.Click+=async(_,_)=>{create.Enabled=select.Enabled=false;try{var list=await graph.Send(HttpMethod.Post,TodoGraph.Lists,new System.Text.Json.Nodes.JsonObject{["displayName"]="simplebanking"},cancellation.Token);Guard();combo.Items.Add(new KeyValuePair<string,string>(list!["id"]!.GetValue<string>(),"simplebanking"));combo.SelectedIndex=combo.Items.Count-1;select.Enabled=true;}catch(Exception e){if(!choose.IsDisposed){MessageBox.Show(choose,TodoError(e)+" Bitte die Listen vor einem erneuten Versuch neu laden.");choose.DialogResult=DialogResult.Cancel;}}};
                choose.FormClosed+=(_,_)=>{if(choose.DialogResult!=DialogResult.OK)cancellation.Cancel();};
                if(choose.ShowDialog(this)!=DialogResult.OK)return;Guard();var selection=(KeyValuePair<string,string>)combo.SelectedItem!;
                if(old.ListId.Length>0&&old.ListId!=selection.Key){if(MessageBox.Show(this,"Vorhandene Aufgaben bleiben in der alten Liste. Aktuelle Erinnerungen werden in der neuen Liste neu angelegt.","Liste wechseln",MessageBoxButtons.OKCancel)!=DialogResult.OK)return;auth.Connection.Id=Guid.NewGuid().ToString("N");}
                auth.Connection.ListId=selection.Key;auth.Connection.ListName=selection.Value;auth.Connection.Enabled=false;main.data.ToDo=auth.Connection;
                try{main.SaveTodo();}catch{main.data.ToDo=old;throw;}main.todoStatus="Verbunden. Aktiviere den automatischen Abgleich, um Erinnerungen zu übertragen.";
            }catch(OperationCanceledException){}catch(Exception e){if(!IsDisposed)MessageBox.Show(this,TodoError(e),"Microsoft To Do");}finally{working=false;main.todoBusy=false;Render();}
        }
        void Review(){
            var pending=main.data.Entries.Where(e=>e.ToDo is{CreateAttempted:true,TaskId:null} l&&l.Connection==main.data.ToDo.Id).ToArray();
            if(pending.Length==0){MessageBox.Show(this,"Keine unklaren Aufgabenerstellungen.\n"+main.todoStatus,"Microsoft To Do");return;}
            if(MessageBox.Show(this,pending.Length+" Erstellung(en) ohne bestätigte Aufgaben-ID. Prüfe zuerst die gewählte Liste in Microsoft To Do. Ein erneuter Abgleich sucht dort nach den Aufgaben.\n\nNur wenn dort keine zugehörigen Aufgaben existieren: Verknüpfungen zurücksetzen und später neu erstellen?", "Unklaren Ausgang prüfen",MessageBoxButtons.YesNo,MessageBoxIcon.Warning,MessageBoxDefaultButton.Button2)!=DialogResult.Yes)return;
            var old=main.data.Entries.ToList();foreach(var entry in pending){int index=main.data.Entries.IndexOf(entry);main.data.Entries[index]=entry with{ToDo=null};}try{main.SaveTodo();}catch{main.data.Entries=old;throw;}
        }
    }
}
