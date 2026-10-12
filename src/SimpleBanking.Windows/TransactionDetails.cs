namespace SimpleBanking;
internal sealed partial class TransactionDetails : Form
{
    Entry entry;
    readonly Action<Entry> save;
    Func<bool> flushNote=()=>true;
    internal bool FlushNote()=>flushNote();
    static readonly string[] categories = ["Einkommen","Essen & Alltag","Abos & Digital","Shopping","Versicherungen","Mobilität","Wohnen & Kredit","Sonstiges","Gastronomie","Sparen","Freizeit","Gehalt","Gesundheit","Umbuchungen"];
    public TransactionDetails(Entry entry, bool hidden, Action<Entry> save, Func<Entry,byte[]?,Entry>? saveMerchantLogo = null,Action<Entry>? createMerchantRule = null)
    {
        this.entry=entry;this.save=save;
        Text="Buchungsdetails";ClientSize=new Size(400,688);StartPosition=FormStartPosition.CenterParent;
        FormBorderStyle=FormBorderStyle.FixedDialog;MaximizeBox=false;MinimizeBox=false;
        AutoScaleMode=AutoScaleMode.Dpi;AutoScaleDimensions=new SizeF(96,96);Icon=AppArtwork.CreateTrayIcon();Disposed+=(_,_)=>Icon.Dispose();
        Font=new Font("Segoe UI",14,FontStyle.Regular,GraphicsUnit.Pixel);BackColor=ExportDesign.Surface;
        var bell=new HeaderIcon("bell","Erinnerung bearbeiten",()=>{},()=>this.entry.ReminderAt!=null&&!this.entry.ReminderDone){Location=new Point(286,20)};
        bell.Click+=(_,_)=>{EditReminder();bell.Invalidate();};
        var read=new HeaderIcon("read",entry.Read?"Als ungelesen markieren":"Als gelesen markieren",()=>{},()=>!this.entry.Read){Location=new Point(324,20)};
        read.Click+=(_,_)=>{var changed=this.entry with {Read=!this.entry.Read};save(changed);this.entry=changed;read.SetLabel(this.entry.Read?"Als ungelesen markieren":"Als gelesen markieren");read.Invalidate();};
        var content=new FlowLayoutPanel {Dock=DockStyle.Fill,AutoScroll=true,FlowDirection=FlowDirection.TopDown,WrapContents=false,Padding=new Padding(16,8,0,16)};
        RoundedCard Card() {var card=new RoundedCard {Width=352};content.Controls.Add(card);return card;}
        void Label(Control parent,string text,int size=13,bool bold=false) => parent.Controls.Add(new Label {Text=text,AutoSize=true,MaximumSize=new Size(320,0),ForeColor=size==11?Color.DimGray:Color.FromArgb(29,29,31),BackColor=Color.Transparent,Font=new Font("Segoe UI",size,bold?FontStyle.Bold:FontStyle.Regular,GraphicsUnit.Pixel),Margin=new Padding(0,0,0,6)});
        void Field(Control parent,string title,string value,bool copy=false) {
            Label(parent,title,11);
            var row=new Panel {Width=320,Height=28,Margin=new Padding(0,0,0,12),BackColor=Color.Transparent};
            var text=new TextBox {Text=string.IsNullOrWhiteSpace(value)?"—":value,ReadOnly=true,TabStop=false,BorderStyle=BorderStyle.None,BackColor=Color.White,Location=new Point(0,4),Width=copy?280:320};
            row.Controls.Add(text);
            if(copy && value.Length>0)row.Controls.Add(new HeaderIcon("copy",title+" kopieren",()=>Clipboard.SetText(value)){Location=new Point(292,0)});
            parent.Controls.Add(row);
        }
        var amountCard=Card();amountCard.AutoSize=false;amountCard.Height=136;amountCard.Padding=new Padding(16);
        var merchantHeader=new Panel{Width=320,Height=52,Margin=new Padding(0,0,0,8),BackColor=Color.Transparent};amountCard.Controls.Add(merchantHeader);
        merchantHeader.Controls.Add(new Label{Text=entry.Merchant,Bounds=new Rectangle(56,0,192,25),Font=new Font("Segoe UI",16,FontStyle.Bold,GraphicsUnit.Pixel),AutoEllipsis=true});
        merchantHeader.Controls.Add(new Label{Text="Gebucht am "+entry.Date.ToString("dd.MM.yyyy"),Bounds=new Rectangle(56,28,205,20),ForeColor=ExportDesign.Secondary,Font=new Font("Segoe UI",12,GraphicsUnit.Pixel)});
        bell.Parent=merchantHeader;bell.Location=new Point(256,0);read.Parent=merchantHeader;read.Location=new Point(288,0);
        var logo=MerchantArtwork.Load(entry);
        var artwork=new Button {Width=48,Height=48,FlatStyle=FlatStyle.Flat,BackColor=Color.White,Cursor=Cursors.Hand,AccessibleName="Händlerlogo ändern",Margin=new Padding(0,0,16,0)};artwork.FlatAppearance.BorderSize=0;
        artwork.Paint+=(_,e)=>{if(logo!=null){ArtworkDrawing.Draw(e.Graphics,logo,new Rectangle(0,0,48,48));return;}e.Graphics.ScaleTransform(2,2);BookingRow.DrawCategory(e.Graphics,new Point(2,2),this.entry.Category,Color.Gray);};
        artwork.Disposed+=(_,_)=>logo?.Dispose();merchantHeader.Controls.Add(artwork);
        amountCard.Controls.Add(new Label {Text=hidden?"••••••":UiFormat.Money(entry.Amount,entry.Currency),Width=320,Height=48,Font=new Font("Segoe UI",36,FontStyle.Bold,GraphicsUnit.Pixel),ForeColor=entry.Amount>0?Color.FromArgb(23,112,70):Color.FromArgb(29,29,31),BackColor=Color.Transparent,Margin=Padding.Empty});
        var details=Card();
        Field(details,entry.Amount<0?"Begünstigter":"Zahlungspflichtiger",entry.Merchant);
        var dates=new Panel {Name="BookingDates",Width=320,Height=56,Margin=new Padding(0,0,0,8),BackColor=Color.Transparent};
        var dateValues=new[]{("Buchungstag",entry.Date.ToString("dd.MM.yyyy")),("Valutadatum",entry.ValueDate?.ToString("dd.MM.yyyy")??"—")};
        for(int i=0;i<dateValues.Length;i++){
            dates.Controls.Add(new Label{Text=dateValues[i].Item1,Bounds=new Rectangle(i*160,0,156,22),ForeColor=ExportDesign.Secondary,BackColor=Color.Transparent,Font=new Font("Segoe UI",12,GraphicsUnit.Pixel)});
            dates.Controls.Add(new Label{Name=i==1?"ValueDate":"BookingDate",Text=dateValues[i].Item2,Bounds=new Rectangle(i*160,25,156,25),BackColor=Color.Transparent,Font=new Font("Segoe UI",14,GraphicsUnit.Pixel)});
        }
        details.Controls.Add(dates);
        Label(details,"Verwendungszweck",12);
        string purpose=string.IsNullOrWhiteSpace(entry.Purpose)?"—":entry.Purpose;
        int purposeHeight=Math.Clamp(TextRenderer.MeasureText(purpose,Font,new Size(320,0),TextFormatFlags.WordBreak|TextFormatFlags.TextBoxControl).Height+8,28,100);
        details.Controls.Add(new TextBox{Name="BookingPurpose",Text=purpose,Multiline=true,ReadOnly=true,BorderStyle=BorderStyle.None,BackColor=Color.White,Width=320,Height=purposeHeight,ScrollBars=purposeHeight==100?ScrollBars.Vertical:ScrollBars.None,Margin=Padding.Empty});
        if(!string.IsNullOrWhiteSpace(entry.AdditionalInformation))Field(details,"Buchungstext",entry.AdditionalInformation);
        var categoryCard=Card();categoryCard.FlowDirection=FlowDirection.LeftToRight;categoryCard.Padding=new Padding(16,12,16,12);categoryCard.Controls.Add(new Label{Text="Kategorie",BackColor=Color.Transparent,Width=104,Height=28,TextAlign=ContentAlignment.MiddleLeft,Margin=Padding.Empty});
        var category=new ComboBox {DropDownStyle=ComboBoxStyle.DropDownList,Width=212,Margin=Padding.Empty};
        category.Items.AddRange(categories);if(!categories.Contains(entry.Category))category.Items.Add(entry.Category);category.SelectedItem=entry.Category;
        category.SelectedIndexChanged+=(_,_)=>{var changed=this.entry with {Category=category.SelectedItem?.ToString()??entry.Category};save(changed);this.entry=changed;};categoryCard.Controls.Add(category);
        if(entry.PurposeCode.Length>0)Label(details,"Zweckcode: "+entry.PurposeCode,11);if(entry.BankTransactionCode.Length>0)Field(details,"Buchungsart der Bank",entry.BankTransactionCode);
        if(!string.IsNullOrWhiteSpace(entry.CounterpartyIban))Field(details,"IBAN",entry.CounterpartyIban,true);if(!string.IsNullOrWhiteSpace(entry.CounterpartyBic))Field(details,"BIC (SWIFT)",entry.CounterpartyBic,true);
        void ChangeLogo(byte[]? bytes,bool shared) {
            try {
                if(shared)this.entry=saveMerchantLogo!(this.entry,bytes);
                else {var changed=this.entry with{CustomLogo=bytes};save(changed);this.entry=changed;}
                logo?.Dispose();logo=MerchantArtwork.Load(this.entry);artwork.Invalidate();
            }catch(Exception ex) when(ex is IOException or InvalidDataException or InvalidOperationException or UnauthorizedAccessException){MessageBox.Show(this,ex.Message,"Logo konnte nicht gespeichert werden");}
        }
        void ChooseLogo(bool shared){
            using var file=new OpenFileDialog{Filter="Bilder|*.png;*.jpg;*.jpeg;*.gif;*.bmp"};if(file.ShowDialog(this)!=DialogResult.OK)return;
            try{var attachment=BookingEnrichment.ReadAttachment(file.FileName);using var stream=new MemoryStream(attachment.Content);using var source=Image.FromStream(stream);ChangeLogo(MerchantLogoStore.Normalize(source),shared);}catch(Exception ex) when(ex is IOException or InvalidDataException or ArgumentException){MessageBox.Show(this,ex.Message);}
        }
        var logoMenu=new ContextMenuStrip();logoMenu.Items.Add("Bild für diese Buchung auswählen …",null,(_,_)=>ChooseLogo(false));logoMenu.Items.Add("Buchungslogo zurücksetzen",null,(_,_)=>ChangeLogo(null,false));
        if(saveMerchantLogo!=null&&MerchantLogoStore.Key(entry)!=null){logoMenu.Items.Add(new ToolStripSeparator());logoMenu.Items.Add("Bild für diesen Händler im Konto …",null,(_,_)=>ChooseLogo(true));logoMenu.Items.Add("Händlerlogo zurücksetzen",null,(_,_)=>ChangeLogo(null,true));}
        if(createMerchantRule!=null){logoMenu.Items.Add(new ToolStripSeparator());logoMenu.Items.Add("Als Händlerregel speichern …",null,(_,_)=>createMerchantRule(this.entry));}
        artwork.Click+=(_,_)=>logoMenu.Show(artwork,new Point(0,artwork.Height));artwork.ContextMenuStrip=logoMenu;Disposed+=(_,_)=>logoMenu.Dispose();
        var notes=Card();Label(notes,"Notiz",14,true);
        var noteFrame=new Panel{Width=320,Height=104,BackColor=Color.White,Margin=Padding.Empty};
        var note=new TextBox{Name="BookingNote",Text=entry.Note,Multiline=true,BorderStyle=BorderStyle.None,Bounds=new Rectangle(10,8,300,86),ScrollBars=ScrollBars.None,PlaceholderText="Notiz zu dieser Buchung …"};noteFrame.Controls.Add(note);notes.Controls.Add(noteFrame);
        var noteStatus=new Label{AutoSize=true,MaximumSize=new Size(320,0),Visible=false,BackColor=Color.Transparent,ForeColor=ExportDesign.Error,Margin=new Padding(0,6,0,0)};
        var retry=new FluentButton{Text="Erneut speichern",Width=146,Height=32,Visible=false,Margin=new Padding(0,6,0,0)};notes.Controls.Add(noteStatus);notes.Controls.Add(retry);
        flushNote=()=>{string value=note.Text.Trim();if(value==this.entry.Note)return true;try{var changed=this.entry with{Note=value};save(changed);this.entry=changed;noteStatus.Visible=retry.Visible=false;return true;}catch(Exception ex) when(ex is IOException or UnauthorizedAccessException or InvalidOperationException or System.Security.Cryptography.CryptographicException){noteStatus.Text="Notiz konnte nicht gespeichert werden. Bitte erneut versuchen.";noteStatus.Visible=retry.Visible=true;return false;}};
        retry.Click+=(_,_)=>flushNote();note.Leave+=(_,_)=>flushNote();FormClosing+=(_,e)=>{if(!flushNote())e.Cancel=true;};
        note.Enter+=(_,_)=>noteFrame.Invalidate();note.Leave+=(_,_)=>noteFrame.Invalidate();
        noteFrame.Paint+=(_,e)=>{using var outline=new Pen(Color.FromArgb(218,218,218));using var path=ExportDesign.Round(new RectangleF(.5f,.5f,noteFrame.Width-1,noteFrame.Height-1),4);e.Graphics.DrawPath(outline,path);using var focus=new Pen(note.Focused?ExportDesign.Accent:Color.Gray,note.Focused?2:1);e.Graphics.DrawLine(focus,2,noteFrame.Height-1,noteFrame.Width-2,noteFrame.Height-1);};
        AddEnrichmentCards(content); Controls.Add(content);
    }
}
internal sealed class RoundedCard : FlowLayoutPanel
{
    internal RoundedCard(){AutoSize=true;AutoSizeMode=AutoSizeMode.GrowAndShrink;FlowDirection=FlowDirection.TopDown;WrapContents=false;Padding=new Padding(16);Margin=new Padding(0,0,0,8);}
    protected override void OnPaintBackground(PaintEventArgs e) {
        base.OnPaintBackground(e);e.Graphics.SmoothingMode=System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
        using var p=new System.Drawing.Drawing2D.GraphicsPath();const int d=16;
        p.AddArc(0,0,d,d,180,90);p.AddArc(Width-d,0,d,d,270,90);p.AddArc(Width-d,Height-d,d,d,0,90);p.AddArc(0,Height-d,d,d,90,90);p.CloseFigure();
        using var fill=new SolidBrush(ThemeStyling.Current.Surface);if(ThemeStyling.Current.SquareControls)e.Graphics.FillRectangle(fill,ClientRectangle);else e.Graphics.FillPath(fill,p);using var border=new Pen(ThemePalette.Blend(ThemeStyling.Current.Ink,ThemeStyling.Current.Surface,.15));e.Graphics.DrawPath(border,p);
    }
}
