using System.Globalization;
namespace SimpleBanking;
internal sealed class PaymentTemplateEditor : Form
{
    internal PaymentTemplate? Result {get;private set;}
    internal PaymentTemplateEditor(string accountId,PaymentTemplate? original,int otherFavorites){
        Text=original==null?"Neue Überweisungsvorlage":"Vorlage bearbeiten";ClientSize=new Size(430,390);FormBorderStyle=FormBorderStyle.FixedDialog;MaximizeBox=false;MinimizeBox=false;StartPosition=FormStartPosition.CenterParent;Font=new Font("Segoe UI",13,GraphicsUnit.Pixel);BackColor=Color.FromArgb(248,250,249);
        var form=new TableLayoutPanel{Dock=DockStyle.Fill,Padding=new Padding(20,16,20,12),ColumnCount=2,RowCount=8};form.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,112));form.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));for(int i=0;i<7;i++)form.RowStyles.Add(new RowStyle(SizeType.Absolute,38));form.RowStyles.Add(new RowStyle(SizeType.Percent,100));Controls.Add(form);
        TextBox Field(string title,string text,int row,int max){var field=new TextBox{Text=text,MaxLength=max,Dock=DockStyle.Fill,AccessibleName=title};form.Controls.Add(new Label{Text=title,AutoSize=true,Anchor=AnchorStyles.Left},0,row);form.Controls.Add(field,1,row);return field;}
        var title=Field("Vorlagenname",original?.Name??"",0,40);var symbol=Field("Symbol",original?.Symbol??"",1,12);symbol.PlaceholderText="z. B. 🏠 oder GE";
        var name=Field("Empfänger",original?.Draft.Name??"",2,70);var iban=Field("IBAN",original?.Draft.Iban??"",3,40);
        var amount=new NumericUpDown{DecimalPlaces=2,Maximum=100000,ThousandsSeparator=true,Value=Math.Clamp(original?.Draft.Amount??0,0,100000),Dock=DockStyle.Fill,AccessibleName="Betrag"};form.Controls.Add(new Label{Text="Betrag in Euro",AutoSize=true,Anchor=AnchorStyles.Left},0,4);form.Controls.Add(amount,1,4);
        var purpose=Field("Zweck",original?.Draft.Purpose??"",5,140);var favorite=new CheckBox{Text="Im Flyout als Favorit",Checked=original?.Favorite??false,AutoSize=true};form.Controls.Add(favorite,1,6);
        var error=new Label{AutoSize=true,ForeColor=Color.FromArgb(170,45,45),MaximumSize=new Size(380,0)};form.Controls.Add(error,0,7);form.SetColumnSpan(error,2);
        var footer=new FlowLayoutPanel{Dock=DockStyle.Bottom,Height=54,FlowDirection=FlowDirection.RightToLeft,Padding=new Padding(12,8,12,8)};Controls.Add(footer);
        var save=DashboardWindow.ActionButton("Speichern",()=>{Result=new(original?.Id??Guid.NewGuid().ToString("N"),title.Text.Trim(),new(accountId,name.Text.Trim(),BankingRules.NormalizeIban(iban.Text),amount.Value,purpose.Text.Trim()),favorite.Checked,symbol.Text.Trim());DialogResult=DialogResult.OK;});var cancel=DashboardWindow.ActionButton("Abbrechen",()=>DialogResult=DialogResult.Cancel);footer.Controls.Add(save);footer.Controls.Add(cancel);AcceptButton=save;CancelButton=cancel;
        void Validate(){string? message=title.Text.Trim().Length==0?"Bitte einen Vorlagennamen eingeben.":new StringInfo(symbol.Text.Trim()).LengthInTextElements>2?"Bitte höchstens zwei Zeichen als Symbol verwenden.":favorite.Checked&&otherFavorites>=4?"Bereits vier Favoriten. Bitte zuerst einen Favoriten lösen.":BankingRules.Validate(new(accountId,name.Text.Trim(),BankingRules.NormalizeIban(iban.Text),amount.Value,purpose.Text.Trim()));error.Text=message??"";save.Enabled=message==null;}
        foreach(var field in new[]{title,symbol,name,iban,purpose})field.TextChanged+=(_,_)=>Validate();amount.ValueChanged+=(_,_)=>Validate();favorite.CheckedChanged+=(_,_)=>Validate();Validate();
    }
}
