namespace SimpleBanking;
internal sealed class QuickSendPanel : TableLayoutPanel
{
    readonly MainWindow owner;
    readonly ComboBox recipient=new(){MaxLength=70,Dock=DockStyle.Fill,DropDownStyle=ComboBoxStyle.DropDown,AutoCompleteMode=AutoCompleteMode.SuggestAppend,AutoCompleteSource=AutoCompleteSource.ListItems,DropDownWidth=320};
    readonly TextBox iban=new(){PlaceholderText="IBAN",Dock=DockStyle.Fill};
    readonly TextBox purpose=new(){PlaceholderText="Verwendungszweck (optional)",MaxLength=140,Dock=DockStyle.Fill};
    readonly NumericUpDown amount=new(){DecimalPlaces=2,Maximum=100000,ThousandsSeparator=true,Dock=DockStyle.Fill};
    readonly FlowLayoutPanel favorites=new(){Dock=DockStyle.Fill,WrapContents=false};
    readonly Button review;
    readonly ToolTip hints=new();
    string? accountId;
    Panel? confirmation;
    internal bool Busy {get;private set;}
    internal int PreferredHeight {get;private set;}=156;
    internal event Action? LayoutRequested;
    readonly Func<TransferDraft,Task<string>> submit;
    internal QuickSendPanel(MainWindow owner,Func<TransferDraft,Task<string>> submit){
        this.submit=submit;
        this.owner=owner;ColumnCount=2;RowCount=4;Padding=new Padding(12,8,12,8);Height=156;BackColor=Color.White;
        ColumnStyles.Add(new ColumnStyle(SizeType.Percent,62));ColumnStyles.Add(new ColumnStyle(SizeType.Percent,38));
        for(int i=0;i<4;i++)RowStyles.Add(new RowStyle(SizeType.Percent,25));
        recipient.AccessibleName="Empfänger";iban.AccessibleName="IBAN";amount.AccessibleName="Betrag in Euro";purpose.AccessibleName="Verwendungszweck";
        Controls.Add(recipient,0,0);Controls.Add(amount,1,0);Controls.Add(iban,0,1);SetColumnSpan(iban,2);Controls.Add(purpose,0,2);SetColumnSpan(purpose,2);
        review=DashboardWindow.ActionButton("Prüfen",()=>{if(Draft() is {} draft&&BankingRules.Validate(draft)==null)ShowReview(draft);});review.Dock=DockStyle.Fill;
        Controls.Add(favorites,0,3);Controls.Add(review,1,3);
        recipient.TextChanged+=(_,_)=>ValidateFields();iban.TextChanged+=(_,_)=>ValidateFields();amount.ValueChanged+=(_,_)=>ValidateFields();
        recipient.SelectionChangeCommitted+=(_,_)=>{if(recipient.SelectedItem is RecipientCandidate candidate&&accountId!=null)Fill(candidate.Draft(accountId));};
        RefreshAccount();
    }
    internal static string TemplateSymbol(PaymentTemplate template)=>template.Symbol.Length>0?template.Symbol:new System.Globalization.StringInfo(template.Name).SubstringByTextElements(0,Math.Min(2,new System.Globalization.StringInfo(template.Name).LengthInTextElements)).ToUpperInvariant();
    protected override void Dispose(bool disposing){if(disposing)hints.Dispose();base.Dispose(disposing);}
    TransferDraft? Draft()=>accountId==null?null:new(accountId,recipient.Text.Trim(),BankingRules.NormalizeIban(iban.Text),amount.Value,purpose.Text.Trim());
    void ValidateFields()=>review.Enabled=Draft() is {} draft&&BankingRules.Validate(draft)==null;
    void Fill(TransferDraft draft){recipient.Text=draft.Name;iban.Text=draft.Iban;amount.Value=Math.Clamp(draft.Amount,0,amount.Maximum);purpose.Text=draft.Purpose;}
    void Edit(){confirmation?.Dispose();confirmation=null;foreach(Control control in Controls)control.Visible=true;PreferredHeight=156;LayoutRequested?.Invoke();ValidateFields();}
    void ShowReview(TransferDraft draft){
        foreach(Control control in Controls)control.Visible=false;
        confirmation=new Panel{Dock=DockStyle.Fill,Margin=Padding.Empty,Name="QuickSendReview",BackColor=Color.White};Controls.Add(confirmation,0,0);SetColumnSpan(confirmation,2);SetRowSpan(confirmation,4);
        var summary=new Label{Dock=DockStyle.Fill,Padding=new Padding(0,0,0,4),Font=new Font("Segoe UI",14,GraphicsUnit.Pixel),Text=owner.QuickSendSourceName+" → "+draft.Name+"\n"+draft.Iban+"\n"+UiFormat.Money(draft.Amount,"EUR")+" · sofort\n"+(draft.Purpose.Length==0?"Ohne Verwendungszweck":draft.Purpose)};confirmation.Controls.Add(summary);
        void Fit(){PreferredHeight=Math.Max(96,TextRenderer.MeasureText(summary.Text,summary.Font,new Size(300,0),TextFormatFlags.WordBreak).Height+62);LayoutRequested?.Invoke();}
        var actions=new FlowLayoutPanel{Dock=DockStyle.Bottom,Height=32,WrapContents=false};confirmation.Controls.Add(actions);
        var back=DashboardWindow.ActionButton("Zurück",()=>{if(Busy){owner.CancelQuickSend();summary.Text="Abbruch angefordert. Bankausgang wird noch geprüft …";}else Edit();});actions.Controls.Add(back);
        var send=DashboardWindow.ActionButton(owner.QuickSendDemo?"Demo simulieren":"Jetzt senden",()=>{});actions.Controls.Add(send);
        Fit();
        send.Click+=async(_,_)=>{
            if(Busy||owner.QuickSendAccountId!=draft.AccountId)return;
            Busy=true;send.Enabled=false;back.Text="Warten beenden";summary.Text="Bankfreigabe wird vorbereitet …\n"+draft.Name+" · "+UiFormat.Money(draft.Amount,"EUR");
            string result;try{result=await submit(draft);}catch(Exception){result="Ausgang nicht bestätigt. Zahlungsjournal und Bank prüfen, bevor erneut gesendet wird.";}finally{Busy=false;}
            if(IsDisposed||confirmation==null)return;
            summary.Text=result;actions.Controls.Remove(send);send.Dispose();back.Text="Schließen";back.Click+=(_,_)=>{recipient.Text="";iban.Clear();purpose.Clear();amount.Value=0;};Fit();
        };
    }
    internal void RefreshAccount(){
        string? next=owner.QuickSendAccountId;if(accountId!=next){accountId=next;if(confirmation!=null)Edit();recipient.Items.Clear();if(next!=null)recipient.Items.AddRange(owner.QuickSendRecipients.Cast<object>().ToArray());recipient.SelectedIndex=-1;recipient.Text="";iban.Clear();purpose.Clear();amount.Value=0;}
        Enabled=next!=null;foreach(Control control in favorites.Controls.Cast<Control>().ToArray())control.Dispose();
        foreach(var template in owner.QuickSendFavorites){var button=DashboardWindow.ActionButton(TemplateSymbol(template),()=>Fill(template.Draft));button.AutoSize=false;button.Size=new Size(36,28);button.Padding=Padding.Empty;button.Margin=new Padding(0,0,4,0);hints.SetToolTip(button,template.Name);button.AccessibleName="Vorlage "+template.Name;favorites.Controls.Add(button);}
        ValidateFields();
    }
}
internal sealed partial class MainWindow
{
    internal string? QuickSendAccountId=>!IsLocked&&!AllAccountsSelected&&SelectedAccount?.Currency=="EUR"?SelectedAccount.Id:null;
    internal RecipientCandidate[] QuickSendRecipients=>QuickSendAccountId is {} id?RecipientHistory.Candidates(data.Entries,id):[];
    internal IEnumerable<PaymentTemplate> QuickSendFavorites=>data.PaymentTemplates.Where(p=>p.Favorite&&p.Draft.AccountId==QuickSendAccountId).Take(4);
    internal bool QuickSendDemo=>demo;
    internal string QuickSendSourceName=>SelectedAccount?.Name??"";
    internal void CancelQuickSend()=>bankCancellation?.Cancel();
    internal Task<string> ReviewQuickSend(TransferDraft draft)=>QuickSendAccountId!=draft.AccountId?Task.FromResult("Nicht gesendet: Quellkonto wurde geändert."):SubmitPayment(draft,true);
}
