using System.Text.Json;
namespace SimpleBanking;
internal sealed class AppPreferences
{
    [System.Text.Json.Serialization.JsonIgnore] public bool Ephemeral {get;set;}
    public bool RemoteMerchantLogos {get;set;}=true;
    public bool AutomationEnabled {get;set;}
    public string ThemeId {get;set;}="default";
    public Dictionary<string,string>? CustomTheme {get;set;}
    public Dictionary<string,byte[]>? CustomThemeImages {get;set;}
    public bool DarkAppearance {get;set;}
    public bool BtxCrtEnabled {get;set;}
    public bool WidgetEnabled {get;set;}
    public int WidgetX {get;set;}=80;
    public int WidgetY {get;set;}=80;
    public bool LaunchAtLogin {get;set;}
    public bool GlobalHotkey {get;set;} = true;
    public bool RefreshHotkey {get;set;}
    public bool LockOnSessionLock {get;set;} = true;
    public int AutoLockMinutes {get;set;}
    public int RefreshMinutes { get; set; } = 240;
    public bool LoadTransactionsOnStart { get; set; }
    public bool ShowCategories { get; set; }
    public bool RippleAlwaysOn { get; set; } = true;
    public bool MonthRingEnabled { get; set; } = true;
    public bool MmiIncludeSavings { get; set; } = true;
    public bool MoodEmoji { get; set; }
    public bool PendingAsPill { get; set; } = true;
    public int FlyoutSubtitleStyle { get; set; }
    public int PanelSubtitleStyle { get; set; }
    public Dictionary<string,AccountFinanceSettings> AccountFinance { get; set; } = new();
    internal AccountFinanceSettings FinanceFor(string accountId) {
        if(!AccountFinance.TryGetValue(accountId,out var settings))AccountFinance[accountId]=settings=new();
        return settings;
    }
    public int LastTab { get; set; }
    public int Deep { get; set; } = -1000;
    public int Low { get; set; } = 500;
    public int Medium { get; set; } = 2000;
    public int VeryGood { get; set; } = 5000;
    static string FilePath => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "SimplebankingWindows", "preferences.json");
    public static AppPreferences Load()
    {
        try { return File.Exists(FilePath) ? JsonSerializer.Deserialize<AppPreferences>(File.ReadAllText(FilePath)) ?? new() : new(); }
        catch (Exception e) when (e is IOException or JsonException) { return new(); }
    }
    public void Save() { if(Ephemeral)return; Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!); File.WriteAllText(FilePath + ".tmp", JsonSerializer.Serialize(this)); File.Move(FilePath + ".tmp", FilePath, true); }
}
internal sealed partial class SettingsWindow : Form
{
    readonly MainWindow owner;
    readonly AppPreferences prefs;
    readonly FlowLayoutPanel body = new() { Dock = DockStyle.Fill, Padding = new Padding(20), FlowDirection = FlowDirection.TopDown, WrapContents = false, AutoScroll = true };
    readonly string[] names = ["Allgemein", "Konten", "Finanzen", "Verhalten", "Sicherheit", "Extras", "Labs", "Über"];
    readonly List<SettingsTab> tabButtons = [];
    public SettingsWindow(MainWindow owner)
    {
        this.owner = owner; prefs = owner.Preferences;
        Icon=AppArtwork.CreateTrayIcon();Disposed+=(_,_)=>Icon?.Dispose();
        AutoScaleMode=AutoScaleMode.Dpi;AutoScaleDimensions=new SizeF(96,96);
        Text = "simplebanking · Einstellungen"; ClientSize = new Size(860, 600); Font = new Font("Segoe UI", 13, FontStyle.Regular, GraphicsUnit.Pixel);
        StartPosition = FormStartPosition.CenterParent; MinimumSize = new Size(680,480); MaximizeBox = true;
        var tabs = new TableLayoutPanel { Dock=DockStyle.Left,Width=240,ColumnCount=1,RowCount=9,Padding=new Padding(12,24,12,12),BackColor=ExportDesign.Surface };
        for(int i=0;i<8;i++)tabs.RowStyles.Add(new RowStyle(SizeType.Absolute,48));tabs.RowStyles.Add(new RowStyle(SizeType.Percent,100));
        for (int i = 0; i < names.Length; i++) {
            int index = i;
            var button = new SettingsTab(index,names[index],()=>prefs.LastTab==index) { Dock=DockStyle.Fill,Margin=Padding.Empty,AccessibleName=names[index],FlatStyle=FlatStyle.Flat };
            button.FlatAppearance.BorderSize=0;
            button.Click += (_, _) => { ShowTab(index); prefs.Save(); };
            tabs.Controls.Add(button,0,i);tabButtons.Add(button);
        }
        body.BackColor=ExportDesign.Surface;BackColor=body.BackColor;
        body.SizeChanged+=(_,_)=>FitRows();
        Controls.Add(body); Controls.Add(tabs); ShowTab(Math.Clamp(prefs.LastTab, 0, names.Length - 1));
    }
    void Save() { prefs.Save(); owner.ApplyPreferences(); }
    void FitRows() {
        int width=Math.Max(180,body.ClientSize.Width-body.Padding.Horizontal-1);
        foreach(Control c in body.Controls) { c.Width=width;if(c is TableLayoutPanel or RoundedCard)c.MinimumSize=new Size(width,c.MinimumSize.Height);if(c is Label label)label.MaximumSize=new Size(width,0); }
    }
    void TextLine(string text,bool heading=false) => body.Controls.Add(new Label {Text=text,AutoSize=true,MaximumSize=new Size(455,0),Font=new Font("Segoe UI",heading?14:13,heading?FontStyle.Bold:FontStyle.Regular,GraphicsUnit.Pixel),Margin=new Padding(0,0,0,heading?12:16)});
    FluentButton ActionControl(string text,Action action,string? icon=null) {
        var button=new FluentButton{Text=text,Chevron=true,Height=48,Width=500,TextAlign=ContentAlignment.MiddleLeft,Font=new Font("Segoe UI",14,GraphicsUnit.Pixel),Margin=Padding.Empty,Image=icon==null?null:ExportDesign.Icon(icon)};
        button.Disposed+=(_,_)=>button.Image?.Dispose();button.Click+=(_,_)=>action();return button;
    }
    void Action(string text,Action action) {var button=ActionControl(text,action);button.Margin=new Padding(0,0,0,6);button.ExternalLink=text.StartsWith("YAXI ·")||text=="simplebanking.de";body.Controls.Add(button);}
    void ActionGroup(params (string Text,Action Run,string Icon)[] actions) {
        var card=new RoundedCard{Width=550,Padding=new Padding(8),Margin=new Padding(0,0,0,16)};
        foreach(var item in actions)card.Controls.Add(ActionControl(item.Text,item.Run,item.Icon));
        card.SizeChanged+=(_,_)=>{foreach(Control control in card.Controls)control.Width=Math.Max(100,card.ClientSize.Width-card.Padding.Horizontal);};body.Controls.Add(card);
    }
    void Toggle(string text,bool value,Action<bool> changed,string? subtitle=null,string? icon=null,string[]? keys=null) {
        var row=new TableLayoutPanel {AutoSize=true,ColumnCount=4,RowCount=1,Margin=new Padding(0,0,0,8),Padding=new Padding(16),MinimumSize=new Size(0,64),BackColor=Color.FromArgb(251,251,251)};
        row.Paint+=(_,e)=>{e.Graphics.SmoothingMode=System.Drawing.Drawing2D.SmoothingMode.AntiAlias;using var path=ExportDesign.Round(new RectangleF(.5f,.5f,row.Width-1,row.Height-1),ThemeStyling.Current.SquareControls?0:6);using var pen=new Pen(ThemePalette.Blend(ThemeStyling.Current.Ink,ThemeStyling.Current.Surface,.18));e.Graphics.DrawPath(pen,path);};
        row.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,icon==null?0:36));row.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));row.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,keys==null?0:150));row.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,88));
        if(icon!=null){var image=ExportDesign.Icon(icon);var picture=new PictureBox{Image=image,SizeMode=PictureBoxSizeMode.CenterImage,Size=new Size(24,28),Anchor=AnchorStyles.Left,Margin=Padding.Empty};picture.Disposed+=(_,_)=>image?.Dispose();row.Controls.Add(picture,0,0);}
        var labels=new FlowLayoutPanel {AutoSize=true,Anchor=AnchorStyles.Left|AnchorStyles.Right,FlowDirection=FlowDirection.TopDown,WrapContents=false,Margin=Padding.Empty};
        var title=new Label {Text=text,AutoSize=true,Font=new Font("Segoe UI",14,GraphicsUnit.Pixel),Margin=new Padding(0,0,0,subtitle==null?0:4)};labels.Controls.Add(title);
        Label? help=null;if(subtitle!=null){help=new Label{Text=subtitle,AutoSize=true,ForeColor=Color.DimGray,Font=new Font("Segoe UI",12,GraphicsUnit.Pixel),Margin=Padding.Empty};labels.Controls.Add(help);}
        labels.SizeChanged+=(_,_)=>{title.MaximumSize=new Size(Math.Max(80,labels.Width),0);if(help!=null)help.MaximumSize=new Size(Math.Max(80,labels.Width),0);};
        var toggle=new SettingsSwitch{Checked=value,AccessibleName=text+(keys==null?"":" · "+string.Join(" + ",keys)),Anchor=AnchorStyles.Right,Margin=Padding.Empty};
        toggle.CheckedChanged+=(_,_)=>{changed(toggle.Checked);Save();};row.Controls.Add(labels,1,0);if(keys!=null)row.Controls.Add(new KeyCaps(keys){Anchor=AnchorStyles.Right,Margin=Padding.Empty},2,0);row.Controls.Add(toggle,3,0);body.Controls.Add(row);
    }
    void Threshold(string text,int value,Action<int> changed,int minimum=-1000000,int maximum=10000000) {
        var row=new TableLayoutPanel{AutoSize=true,ColumnCount=2,Margin=new Padding(0,0,0,14)};
        row.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));row.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,130));
        row.Controls.Add(new Label{Text=text,AutoSize=true,Anchor=AnchorStyles.Left},0,0);
        var number=new NumericUpDown{Minimum=minimum,Maximum=maximum,Value=Math.Clamp(value,minimum,maximum),Dock=DockStyle.Fill};number.ValueChanged+=(_,_)=>{changed((int)number.Value);Save();};row.Controls.Add(number,1,0);body.Controls.Add(row);
    }
    void SalarySettings() {
        var accounts=owner.FlyoutAccounts();if(accounts.Count==0)return;
        TextLine("Gehaltseingang",true);
        var accountPicker=new ComboBox {DropDownStyle=ComboBoxStyle.DropDownList,Width=320,DisplayMember=nameof(Account.Name)};
        accountPicker.Items.AddRange(accounts.Cast<object>().ToArray());accountPicker.SelectedIndex=Math.Clamp(owner.SelectedAccountIndex,0,accounts.Count-1);body.Controls.Add(accountPicker);
        var presets=new FlowLayoutPanel {Width=455,Height=34,WrapContents=false,Margin=new Padding(0,6,0,6)};body.Controls.Add(presets);
        var day=new ComboBox {DropDownStyle=ComboBoxStyle.DropDownList,Width=180,Margin=new Padding(0,0,0,20)};
        day.Items.AddRange(Enumerable.Range(1,31).Select(i=>(object)(i+". des Monats")).ToArray());body.Controls.Add(day);
        TextLine("Gehalt / Kontoring (0 = automatisch erkennen)");var salary=new NumericUpDown{DecimalPlaces=2,Maximum=10000000,ThousandsSeparator=true};body.Controls.Add(salary);
        TextLine("Dispokredit / Kontoring");var overdraft=new NumericUpDown{DecimalPlaces=2,Maximum=10000000,ThousandsSeparator=true};body.Controls.Add(overdraft);
        TextLine("Toleranz beim Gehaltseingang (Tage)");var tolerance=new NumericUpDown{Maximum=15};body.Controls.Add(tolerance);
        bool loading=false;
        AccountFinanceSettings Current()=>prefs.FinanceFor(((Account)accountPicker.SelectedItem!).Id);
        void Reload() {
            loading=true;var settings=Current();day.SelectedIndex=Math.Clamp(settings.SalaryDay-1,0,30);day.Visible=settings.SalaryPreset==2;salary.Value=Math.Clamp(settings.SalaryAmount,0,salary.Maximum);overdraft.Value=Math.Clamp(settings.DispoLimit,0,overdraft.Maximum);tolerance.Value=Math.Clamp(settings.SalaryTolerance,0,15);
            for(int i=0;i<presets.Controls.Count;i++)((RadioButton)presets.Controls[i]).Checked=i==settings.SalaryPreset;
            loading=false;
        }
        foreach(var (title,index) in new[]{("Anfang",0),("Mitte",1),("Individuell",2)}) {
            var button=new RadioButton {Text=title,Appearance=Appearance.Button,AutoSize=true,FlatStyle=FlatStyle.Flat};
            button.CheckedChanged+=(_,_)=>{if(loading || !button.Checked)return;Current().SalaryPreset=index;day.Visible=index==2;Save();};presets.Controls.Add(button);
        }
        accountPicker.SelectedIndexChanged+=(_,_)=>Reload();
        salary.ValueChanged+=(_,_)=>{if(!loading){Current().SalaryAmount=salary.Value;Save();}};overdraft.ValueChanged+=(_,_)=>{if(!loading){Current().DispoLimit=overdraft.Value;Save();}};tolerance.ValueChanged+=(_,_)=>{if(!loading){Current().SalaryTolerance=(int)tolerance.Value;Save();}};
        day.SelectedIndexChanged+=(_,_)=>{if(loading)return;Current().SalaryDay=day.SelectedIndex+1;Save();};Reload();
    }
    internal void ShowReviewTab(int index) => ShowTab(index);
    void ShowTab(int index)
    {
        body.SuspendLayout();
        foreach (Control c in body.Controls.Cast<Control>().ToArray()) c.Dispose(); body.AutoScrollPosition = Point.Empty; prefs.LastTab = index;
        foreach(var button in tabButtons) button.Invalidate();

        body.Controls.Add(new Label{Text=names[index],AutoSize=true,Font=new Font("Segoe UI",28,FontStyle.Bold,GraphicsUnit.Pixel),Margin=new Padding(0,0,0,24)});
        switch (index) {
            case 0:
                TextLine("Start",true);
                Toggle("Bei Windows-Anmeldung starten",prefs.LaunchAtLogin,owner.SetLaunchAtLogin,icon:"power_20_regular-444");
                Toggle("Umsätze beim Start laden",prefs.LoadTransactionsOnStart,v=>prefs.LoadTransactionsOnStart=v,"Umsatzliste automatisch beim Entsperren laden",icon:"arrow_sync_20_regular-444");
                TextLine("Tastenkürzel",true);
                Toggle("Fenster öffnen",prefs.GlobalHotkey,v=>prefs.GlobalHotkey=v,icon:"keyboard_20_regular-444",keys:["Strg","Alt","S"]);
                Toggle("Aktualisieren",prefs.RefreshHotkey,v=>prefs.RefreshHotkey=v,icon:"arrow_clockwise_20_regular-444",keys:["Strg","Alt","R"]);
                TextLine("Daten",true);
                ActionGroup(("Sicherung erstellen …",owner.BackupFromSettings,"arrow_upload_20_regular-444"),("Sicherung einspielen …",owner.RestoreFromSettings,"arrow_download_20_regular-444"),("Nicht zugeordnete Buchungsdetails",owner.ReviewUnmatchedEnrichments,"document_20_regular-444"));
                break;
            case 1:
                TextLine("Verbundene Konten", true);
                foreach (var account in owner.FlyoutAccounts()) {
                    var row=new Panel{Width=455,Height=52,Margin=new Padding(0,0,0,12)};
                    var logo=BankArtwork.LoadLogo(account,28);
                    var picture=new PictureBox{Location=new Point(0,7),Size=new Size(28,28),Image=logo,SizeMode=PictureBoxSizeMode.Zoom};picture.Disposed+=(_,_)=>logo?.Dispose();
                    row.Controls.Add(picture);row.Controls.Add(new Label{Text=account.Name+"\n"+account.Iban,Location=new Point(40,4),Size=new Size(405,44)});body.Controls.Add(row);
                }
                Action("Konto hinzufügen", owner.AddBankFromSettings);
                SalarySettings();
                TextLine("Automatische Aktualisierung");
                int[] minutes = [0,60,120,180,240,360];
                var refresh = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 180 };
                refresh.Items.AddRange(new object[] { "Manuell", "1 Stunde", "2 Stunden", "3 Stunden", "4 Stunden", "6 Stunden" }); refresh.SelectedIndex = Math.Max(0, Array.IndexOf(minutes, prefs.RefreshMinutes));
                refresh.SelectedIndexChanged += (_, _) => { prefs.RefreshMinutes = minutes[refresh.SelectedIndex]; Save(); }; body.Controls.Add(refresh);
                break;
            case 2:
                TextLine("Money Mass Index (MMI)",true);
                Toggle("Sparbewegungen einbeziehen",prefs.MmiIncludeSavings,v=>prefs.MmiIncludeSavings=v);
                Action("Financial Health öffnen",()=>owner.OpenDashboard());
                TextLine("Money-Mood", true);
                Threshold("Tief im Dispo", prefs.Deep, v => prefs.Deep = Math.Min(-1,v));
                Threshold("Niedriger Stand", prefs.Low, v => prefs.Low = Math.Max(0,v));
                Threshold("Mittlerer Stand", prefs.Medium, v => prefs.Medium = Math.Max(prefs.Low+1,v));
                Threshold("Sehr wohlhabend", prefs.VeryGood, v => prefs.VeryGood = Math.Max(prefs.Medium+1,v));
                break;
            case 3:
                TextLine("Darstellung",true);var themes=new ComboBox{DropDownStyle=ComboBoxStyle.DropDownList,DisplayMember="Value",ValueMember="Key"};var choices=ThemePalette.BuiltIns.Select(p=>new KeyValuePair<string,string>(p.Key,p.Value["name"])).ToList();if(prefs.CustomTheme is {} custom)choices.Add(new("custom",custom.GetValueOrDefault("name")??"Importiertes Theme"));themes.Items.AddRange(choices.Cast<object>().ToArray());themes.SelectedIndex=Math.Max(0,choices.FindIndex(c=>c.Key==prefs.ThemeId));themes.SelectionChangeCommitted+=(_,_)=>{if(themes.SelectedItem is KeyValuePair<string,string> choice){prefs.ThemeId=choice.Key;Save();}};body.Controls.Add(themes);ThemeTools();
                Toggle("Dunkle Darstellung",prefs.DarkAppearance,v=>prefs.DarkAppearance=v,"Gilt für Konten, Auswertungen, Einstellungen und Dialoge.");
                Toggle("Händlerlogos aus dem Internet",prefs.RemoteMerchantLogos,v=>prefs.RemoteMerchantLogos=v,"Bekannte Händlerdomains an Logo.dev senden. Lokale Logos zuerst; 30 Tage Cache, höchstens drei neue Abrufe pro Tag wie auf dem Mac.");
                TextLine("Kontostand-Anzeige", true);
                if(owner.Theme.HasRingImage)TextLine("Das Theme verwendet ein eigenes Ringbild.");
                else Toggle("Kontoring anzeigen",prefs.MonthRingEnabled,v=>prefs.MonthRingEnabled=v);
                Toggle("Ripple bei jedem Öffnen", prefs.RippleAlwaysOn, v => prefs.RippleAlwaysOn = v);
                Toggle("„Vorgemerkt“ als Pille anzeigen", prefs.PendingAsPill, v => prefs.PendingAsPill = v,"Markiert vorgemerkte Buchungen mit einer eigenen Kapsel. Aus: Die Zeile bleibt gedimmt, aber ohne Kapsel.");
                Toggle("Kategorien anzeigen", prefs.ShowCategories, v => prefs.ShowCategories = v);
                break;
            case 4:
                TextLine("Automatisch sperren",true);
                Toggle("Beim Sperren von Windows",prefs.LockOnSessionLock,v=>prefs.LockOnSessionLock=v);
                Threshold("Inaktivität (Minuten, 0 = aus)",prefs.AutoLockMinutes,v=>prefs.AutoLockMinutes=v,0,240);
                TextLine("Windows Hello",true);
                TextLine("Mit Windows-PIN, Fingerabdruck oder Gesicht entsperren. Das Masterpasswort bleibt verfügbar.");
                Action(owner.HelloEnabled?"Windows Hello deaktivieren":"Windows Hello einrichten …",()=>{owner.ConfigureHello(this);if(!IsDisposed)ShowTab(4);});
                TextLine("App-Passwort", true); Action("Passwort ändern …",()=>{owner.ChangeMasterPassword();if(!IsDisposed)ShowTab(4);}); Action("Sperren", owner.Lock); Action("Entsperren", owner.UnlockFromSettings);
                TextLine("Sicherung", true); Action("Sicherung erstellen …", owner.BackupFromSettings); break;
            case 5:
                TextLine("simplesend", true); Action("Lizenz aktivieren …",owner.ActivateSimpleSend); Action("Zahlungsjournal",owner.OpenPayments);Action("Vorlagen",owner.OpenPaymentTemplates);TextLine("CLI / MCP",true);Toggle("Lokalen Lesezugriff erlauben",prefs.AutomationEnabled,v=>prefs.AutomationEnabled=v,"Konten und Umsätze für lokale Programme desselben Windows-Benutzers freigeben. Nur bei entsperrter App; keine Zahlungen und keine Bank-Zugangsdaten.");break;
            case 6:
                BuildAiSettings(); break;
            case 7: BuildAbout();break;
        }
        FitRows();body.ResumeLayout(true);body.AutoScrollPosition=Point.Empty;
    }
}
