namespace SimpleBanking;
internal sealed class RuleAssistant : Form
{
    readonly MainWindow owner;readonly AnalysisSnapshot snapshot;readonly AssignmentRule? original;
    readonly FlowLayoutPanel conditions=new(){Dock=DockStyle.Top,AutoSize=true,FlowDirection=FlowDirection.TopDown,WrapContents=false};
    readonly ComboBox category=Choice(["Keine Kategorie","Einkommen","Essen & Alltag","Abos & Digital","Shopping","Versicherungen","Mobilität","Wohnen & Kredit","Sonstiges","Gastronomie","Sparen","Freizeit","Gehalt","Gesundheit","Umbuchungen"]);
    readonly ComboBox recurring=Choice(["Keine Markierung","Abos","Verträge","Verbindlichkeiten","Sparen","Ausschließen"]);
    readonly ComboBox scope=Choice(["Aktuelles Konto","Alle Konten"]);
    readonly NumericUpDown priority=new(){Minimum=0,Maximum=100000,Value=100,Width=95};
    readonly DataGridView preview=DashboardWindow.Table(["Datum","Händler","Betrag","Bisher","Nach Anwendung"]);
    readonly Label sentence=new(){AutoSize=true,MaximumSize=new Size(760,0),ForeColor=Color.FromArgb(30,95,160),Padding=new Padding(0,8,0,8)};
    readonly List<(ComboBox Join,ComboBox Field,ComboBox Op,ComboBox Value)> editors=[];
    bool loading;
    internal RuleAssistant(MainWindow owner,AnalysisSnapshot snapshot,AssignmentRule? original=null){
        this.owner=owner;this.snapshot=snapshot;this.original=original;Text="Regel-Assistent";ClientSize=new Size(820,660);MinimumSize=new Size(760,620);StartPosition=FormStartPosition.CenterParent;Font=new Font("Segoe UI",13,GraphicsUnit.Pixel);BackColor=Color.FromArgb(248,250,249);
        var top=new FlowLayoutPanel{Dock=DockStyle.Top,Height=300,AutoScroll=true,FlowDirection=FlowDirection.TopDown,WrapContents=false,Padding=new Padding(20)};
        top.Controls.Add(DashboardWindow.Label("Wenn … dann …",20,true));top.Controls.Add(conditions);top.Controls.Add(DashboardWindow.ActionButton("Bedingung hinzufügen",()=>AddCondition(new("Volltext","enthält",""))));
        var actions=new FlowLayoutPanel{AutoSize=true,WrapContents=false};actions.Controls.Add(DashboardWindow.Label("Dann Kategorie"));actions.Controls.Add(category);actions.Controls.Add(DashboardWindow.Label("Markierung"));actions.Controls.Add(recurring);actions.Controls.Add(priority);top.Controls.Add(actions);top.Controls.Add(sentence);
        var bottom=new FlowLayoutPanel{Dock=DockStyle.Bottom,AutoSize=true,FlowDirection=FlowDirection.RightToLeft,Padding=new Padding(16)};
        bottom.Controls.Add(DashboardWindow.ActionButton("Speichern & anwenden",()=>Commit(true)));bottom.Controls.Add(DashboardWindow.ActionButton("Nur speichern",()=>Commit(false)));bottom.Controls.Add(DashboardWindow.ActionButton("Abbrechen",()=>Close()));bottom.Controls.Add(scope);
        Controls.Add(preview);Controls.Add(top);Controls.Add(bottom);
        loading=true;if(original!=null){category.SelectedItem=original.Category??"Keine Kategorie";recurring.SelectedItem=original.Recurring??"Keine Markierung";priority.Value=original.Priority;foreach(var c in original.Conditions)AddCondition(c);}else AddCondition(new("Volltext","enthält",""));loading=false;
        scope.SelectedIndexChanged+=(_,_)=>UpdatePreview();category.SelectedIndexChanged+=(_,_)=>UpdatePreview();recurring.SelectedIndexChanged+=(_,_)=>UpdatePreview();UpdatePreview();
    }
    static ComboBox Choice(string[] items){var box=new ComboBox{DropDownStyle=ComboBoxStyle.DropDownList,Width=155,Margin=new Padding(0,0,8,0)};box.Items.AddRange(items);box.SelectedIndex=0;return box;}
    void AddCondition(RuleCondition condition){
        var row=new FlowLayoutPanel{AutoSize=true,WrapContents=false};var join=Choice(["UND","ODER"]);join.Width=65;join.SelectedIndex=condition.Or?1:0;
        var field=Choice(["Händler","Empfänger","Absender","Zweck","Volltext","E2E-ID","Betrag","Buchungsart","Intervall"]);field.Width=120;field.SelectedItem=condition.Field;
        var op=Choice(["enthält","enthält nicht","ist","ist nicht","=",">","<"]);op.Width=115;op.SelectedItem=condition.Operator;
        var value=new ComboBox{Text=condition.Value,Width=250,DropDownStyle=ComboBoxStyle.DropDown};var editor=(join,field,op,value);editors.Add(editor);
        foreach(Control c in new Control[]{join,field,op,value})row.Controls.Add(c);
        row.Controls.Add(DashboardWindow.ActionButton("−",()=>{editors.Remove(editor);row.Dispose();UpdatePreview();}));conditions.Controls.Add(row);
                void Configure(){
            string previous=op.Text,text=value.Text;var options=field.Text=="Betrag"?new[]{"=",">","<"}:field.Text=="Intervall"?new[]{"ist"}:field.Text=="Buchungsart"?new[]{"ist","ist nicht"}:new[]{"enthält","enthält nicht","ist","ist nicht"};
            op.Items.Clear();op.Items.AddRange(options);op.SelectedItem=options.Contains(previous)?previous:options[0];
            value.Items.Clear();string[] fixedValues=field.Text=="Intervall"?["Monatlich","Vierteljährlich","Jährlich"]:field.Text=="Buchungsart"?["Zahlung","Eingang"]:[];
            value.DropDownStyle=fixedValues.Length>0?ComboBoxStyle.DropDownList:ComboBoxStyle.DropDown;value.Items.AddRange(fixedValues);
            if(fixedValues.Length>0)value.SelectedItem=fixedValues.Contains(text)?text:fixedValues[0];else value.Text=text;
        }
        Configure();join.Visible=editors.Count>1;
        join.SelectedIndexChanged+=(_,_)=>UpdatePreview();field.SelectedIndexChanged+=(_,_)=>{Configure();UpdatePreview();};op.SelectedIndexChanged+=(_,_)=>UpdatePreview();value.TextChanged+=(_,_)=>UpdatePreview();UpdatePreview();
    }
    AssignmentRule Current()=>new(original?.Id??Guid.NewGuid().ToString("N"),original?.Enabled??true,(int)priority.Value,editors.Select(e=>new RuleCondition(e.Field.Text,e.Op.Text,e.Value.Text.Trim(),e.Join.SelectedIndex==1)).ToList(),category.SelectedIndex>0?category.Text:null,recurring.SelectedIndex>0?recurring.Text:null);
    void UpdatePreview(){if(loading)return;var rule=Current();sentence.Text=AssignmentRules.Summary(rule);preview.Rows.Clear();foreach(var e in owner.MatchRule(rule,owner.RulePreviewEntries(scope.SelectedIndex==1)))preview.Rows.Add(e.Date.ToString("dd.MM.yyyy"),e.Merchant,UiFormat.Money(e.Amount,e.Currency),e.Category,rule.Category??e.Category);}
    void Commit(bool apply){var rule=Current();if(rule.Conditions.Count==0||rule.Conditions.Any(c=>string.IsNullOrWhiteSpace(c.Value))||(rule.Category==null&&rule.Recurring==null)){MessageBox.Show(this,"Bitte mindestens eine vollständige Bedingung und eine Aktion angeben.");return;}owner.SaveAnalysisRule(rule,apply,scope.SelectedIndex==1?null:snapshot.Accounts.Select(a=>a.Id).ToHashSet());DialogResult=DialogResult.OK;}
}
