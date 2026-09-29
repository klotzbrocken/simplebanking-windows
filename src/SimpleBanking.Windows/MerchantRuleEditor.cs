using System.Text.RegularExpressions;
namespace SimpleBanking;
internal sealed class MerchantRuleEditor : Form
{
    internal MerchantUserRule? Result {get;private set;}
    internal MerchantRuleEditor(MerchantUserRule? original,IEnumerable<Entry> entries){
        Text=original==null?"Neue Händlerregel":"Händlerregel bearbeiten";ClientSize=new Size(560,465);MinimumSize=Size;StartPosition=FormStartPosition.CenterParent;Font=new Font("Segoe UI",13,GraphicsUnit.Pixel);BackColor=Color.FromArgb(248,250,249);
        var form=new TableLayoutPanel{Dock=DockStyle.Top,Height=245,Padding=new Padding(18,16,18,4),ColumnCount=2,RowCount=6};form.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,125));form.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));for(int i=0;i<6;i++)form.RowStyles.Add(new RowStyle(SizeType.Percent,100));
        void Add(string title,Control control,int row){form.Controls.Add(new Label{Text=title,AutoSize=true,Anchor=AnchorStyles.Left},0,row);control.Dock=DockStyle.Fill;control.AccessibleName=title;form.Controls.Add(control,1,row);}
        ComboBox Choice(string[] choices,string? selected){var control=new ComboBox{DropDownStyle=ComboBoxStyle.DropDownList};control.Items.AddRange(choices);control.SelectedItem=selected??choices[0];return control;}
        var scope=Choice(["Volltext","Empfänger","Verwendungszweck","End-to-End-ID"],original?.Scope);var match=Choice(["Enthält","Ist","Regex"],original?.MatchType);var pattern=new TextBox{Text=original?.Pattern??"",MaxLength=500};var merchant=new TextBox{Text=original?.Merchant??"",MaxLength=120};var priority=new NumericUpDown{Minimum=0,Maximum=10000,Value=Math.Clamp(original?.Priority??100,0,10000)};var enabled=new CheckBox{Text="Regel aktiv",Checked=original?.Enabled??true,AutoSize=true};
        Add("Suchen in",scope,0);Add("Bedingung",match,1);Add("Suchmuster",pattern,2);Add("Anzeigen als",merchant,3);Add("Priorität",priority,4);Add("",enabled,5);
        var preview=DashboardWindow.Table(["Bisher","Nach Anwendung"]);preview.Dock=DockStyle.Fill;preview.TabStop=false;form.TabIndex=0;preview.ColumnHeadersDefaultCellStyle.SelectionBackColor=preview.ColumnHeadersDefaultCellStyle.BackColor;preview.RowTemplate.Height=30;preview.ColumnHeadersHeight=28;
        var status=new Label{Dock=DockStyle.Bottom,Height=32,Padding=new Padding(18,6,0,0)};
        var footer=new FlowLayoutPanel{Dock=DockStyle.Bottom,Height=52,Padding=new Padding(12,8,12,8),FlowDirection=FlowDirection.RightToLeft};
        MerchantUserRule Value()=>new(original?.Id??Guid.NewGuid().ToString("N"),enabled.Checked,(int)priority.Value,scope.Text,match.Text,pattern.Text.Trim(),merchant.Text.Trim());
        var apply=DashboardWindow.ActionButton("Speichern und anwenden",()=>{});footer.Controls.Add(apply);var cancel=DashboardWindow.ActionButton("Abbrechen",()=>DialogResult=DialogResult.Cancel);footer.Controls.Add(cancel);CancelButton=cancel;AcceptButton=apply;
        Controls.Add(preview);Controls.Add(form);Controls.Add(status);Controls.Add(footer);
        var sample=entries.OrderByDescending(e=>e.Date).Take(2000).ToArray();usingTimer=new(){Interval=180};
        void Preview(){preview.Rows.Clear();var rule=Value();string? error=rule.Pattern.Length==0?"Suchmuster eingeben.":rule.Merchant.Length==0?"Gewünschten Anzeigenamen eingeben.":null;if(rule.MatchType=="Regex")try{_=new Regex(rule.Pattern);}catch(ArgumentException){error="Der reguläre Ausdruck ist ungültig.";}apply.Enabled=error==null;if(error!=null){status.Text=error;return;}int count=0;var elapsed=System.Diagnostics.Stopwatch.StartNew();bool limited=false;foreach(var entry in sample){if(elapsed.ElapsedMilliseconds>200){limited=true;break;}if(UserMerchantRules.Matches(rule with{Enabled=true},entry)){count++;if(preview.Rows.Count<3)preview.Rows.Add(entry.Merchant,rule.Merchant);}}status.Text=(enabled.Checked?"":"Deaktiviert · ")+count+" Treffer in der Vorschau"+(limited?" · zeitlich begrenzt":sample.Length==2000?" · letzte 2.000 Umsätze":"");preview.CurrentCell=null;preview.ClearSelection();}
        apply.Click+=(_,_)=>{Preview();if(apply.Enabled){Result=Value();DialogResult=DialogResult.OK;}};
        usingTimer.Tick+=(_,_)=>{usingTimer.Stop();Preview();};void Queue(){usingTimer.Stop();usingTimer.Start();}
        scope.SelectedIndexChanged+=(_,_)=>Queue();match.SelectedIndexChanged+=(_,_)=>Queue();pattern.TextChanged+=(_,_)=>Queue();merchant.TextChanged+=(_,_)=>Queue();enabled.CheckedChanged+=(_,_)=>Queue();Preview();
    }
    readonly System.Windows.Forms.Timer usingTimer;
    protected override void Dispose(bool disposing){if(disposing)usingTimer.Dispose();base.Dispose(disposing);}
}
