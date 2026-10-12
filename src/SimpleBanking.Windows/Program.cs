using System.Globalization;

namespace SimpleBanking;

internal static class Program
{
    [STAThread]
    static int Main(string[] args)
    {
        if(args.Contains("--mcp"))return LocalAutomation.RunMcp(args.Contains("--demo"));
        if(args.Contains("--cli"))return LocalAutomation.RunCli(args);
        if (args.Contains("--self-test")) return SelfTests.Run(args);
        ApplicationConfiguration.Initialize();
        if(args.Contains("--test-tray-lifecycle"))return TrayLifecycleTest.Run();
        if (args.Length == 2 && args[0] == "--render-ui") return UiReview.Render(args[1]);
        var instanceName = "Local\\Simplebanking.Windows." + (args.Contains("--demo") ? "Demo" : "Live");
        using var activation = new EventWaitHandle(false, EventResetMode.AutoReset, instanceName + ".Open");
        using var mutex = new Mutex(true, instanceName, out var created);
        if (!created) { activation.Set(); return 0; }
        Application.ThreadException += (_, e) => MessageBox.Show("Die Aktion konnte nicht abgeschlossen werden. " + e.Exception.GetType().Name, "simplebanking");
        var renderIndex = Array.IndexOf(args, "--render");
        using var context = new TrayContext(args.Contains("--demo"), args.Contains("--smoke-test"), renderIndex >= 0 && renderIndex + 1 < args.Length ? args[renderIndex + 1] : null, activation);
        Application.Run(context);
        return 0;
    }
}

internal sealed class TrayContext : ApplicationContext
{
    readonly NotifyIcon tray;
    Icon trayIcon = AppArtwork.CreateBankTrayIcon(null);
    readonly MainWindow window;
    readonly BalanceFlyout flyout;
    readonly System.Windows.Forms.Timer? smokeTimer;
    readonly System.Windows.Forms.Timer trayRepairTimer;
    readonly System.Windows.Forms.Timer activationTimer;
    readonly System.Windows.Forms.Timer reminderTimer=new(){Interval=30000};
    int shownReminders;
    bool bankApprovalNotification;
    long lastDoubleClick;
    bool exiting;
    readonly System.Windows.Forms.Timer clickTimer=new() {Interval=SystemInformation.DoubleClickTime};
    public TrayContext(bool demo, bool smoke, string? renderPath, EventWaitHandle activation)
    {
        window = new MainWindow(demo);
        flyout = new BalanceFlyout(window);
        var menu = new ContextMenuStrip();
        menu.Items.Add("simplebanking öffnen", null, (_, _) => Open());
        menu.Items.Add("Dashboard",null,(_,_)=>window.OpenDashboard());
        menu.Items.Add("Einstellungen", null, (_, _) => window.OpenSettings());
        var lockItem = menu.Items.Add("Entsperren", null, (_, _) => { if (window.IsLocked) window.RequestUnlock(); else window.Lock(); });
        menu.Opening += (_, _) => lockItem.Text = window.IsLocked ? window.UnlockActionText : "Sperren";
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("Beenden", null, (_, _) => ExitThread());
        tray = new NotifyIcon { Icon = trayIcon, Text = "simplebanking · Konten", ContextMenuStrip = menu, Visible = false };
        window.BalanceChanged+=UpdateTrayIcon;UpdateTrayIcon();
        window.BankApprovalNeeded+=bank=>{bankApprovalNotification=true;tray.ShowBalloonTip(6000,bank+": neue Freigabe nötig","Automatischer Abruf pausiert. Öffne simplebanking und wähle Aktualisieren, um die Bankfreigabe zu starten.",ToolTipIcon.Info);};
        clickTimer.Tick+=(_,_)=>{clickTimer.Stop();if(window.IsLocked)window.OpenMainWindow();else flyout.ShowAtTray();};
        tray.MouseClick += (_, e) => { if(e.Button==MouseButtons.Left && Environment.TickCount64-lastDoubleClick>SystemInformation.DoubleClickTime) {clickTimer.Stop();clickTimer.Start();} };
        tray.MouseDoubleClick+=(_,e)=>{if(e.Button==MouseButtons.Left){lastDoubleClick=Environment.TickCount64;clickTimer.Stop();if(!flyout.IsPinned)flyout.Hide();Open();}};
        window.FormClosing += (_, e) => { if (!exiting && e.CloseReason == CloseReason.UserClosing) { e.Cancel = true; window.Hide(); } };
        if(smoke)window.Show();else Open();
        // Explorer can create/recreate the notification area after the app starts.
        // Register twice so the icon is present even when the initial shell callback is missed.
        tray.Visible = true;
        reminderTimer.Tick+=(_,_)=>{int count=window.DueReminderCount();if(count>shownReminders&&!demo){bankApprovalNotification=false;tray.ShowBalloonTip(5000,"simplebanking",count+" fällige Erinnerungen",ToolTipIcon.Info);}shownReminders=count;};reminderTimer.Start();tray.BalloonTipClicked+=(_,_)=>{if(bankApprovalNotification)Open();else window.OpenAttentionInbox();};
        trayRepairTimer = new() { Interval = 1500 };
        trayRepairTimer.Tick += (_, _) => { tray.Visible = false; tray.Visible = true; trayRepairTimer.Stop(); };
        trayRepairTimer.Start();
        activationTimer = new() { Interval = 250 };
        activationTimer.Tick += (_, _) => { if (activation.WaitOne(0)) Open(); };
        activationTimer.Start();MainForm=window;flyout.RestoreWidget();
        if (smoke)
        {
            smokeTimer = new() { Interval = 2000 };
            smokeTimer.Tick += (_, _) => {
                smokeTimer.Stop();
                if (renderPath != null) { using var bitmap = new Bitmap(window.Width, window.Height); window.DrawToBitmap(bitmap, new Rectangle(0, 0, window.Width, window.Height)); bitmap.Save(renderPath); }
                ExitThread();
            };
            smokeTimer.Start();
        }
    }
    void UpdateTrayIcon(){
        if(window.IsLocked){tray.Text="simplebanking · Gesperrt";return;}
        var account=window.FlyoutAccounts().ElementAtOrDefault(window.SelectedAccountIndex);
        var next=AppArtwork.CreateBankTrayIcon(window.AllAccountsSelected?null:account);
        tray.Icon=next;var old=trayIcon;trayIcon=next;old.Dispose();
        string label="simplebanking · "+(window.IsLocked?"Gesperrt":window.AllAccountsSelected?"Alle Konten":account?.Name??"Konten");
        tray.Text=label[..Math.Min(63,label.Length)];
    }
    void Open() { if(exiting)return;if(window.IsDisposed||flyout.IsDisposed){ExitThread();return;}if(!flyout.IsPinned)flyout.Hide(); window.OpenMainWindow(); }
    protected override void ExitThreadCore()
    {
        if(exiting)return;exiting = true; MainForm=null;reminderTimer.Dispose();clickTimer.Dispose(); activationTimer.Dispose(); smokeTimer?.Dispose(); trayRepairTimer.Dispose(); tray.Visible = false; tray.Dispose(); trayIcon.Dispose(); flyout.Dispose(); window.Dispose(); base.ExitThreadCore();
    }
}

internal sealed partial class MainWindow : Form
{
    bool demo;
    bool unlockQueued, unlocking;
    readonly EncryptedStore store;
    BankData data = new();
    string? password;
    readonly ComboBox accounts = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 300 };
    readonly TextBox search = new() { PlaceholderText = "Umsätze durchsuchen …", Width = 270 };
    readonly Label balance = new() { AutoSize = true, Font = new Font("Segoe UI", 27, FontStyle.Bold), Margin = new Padding(0, 16, 0, 12) };
    readonly Label status = new() { Dock = DockStyle.Bottom, Height = 30, Padding = new Padding(16, 5, 8, 0), Visible=false };
    bool hiddenBalance;
    readonly TransactionList transactionList = new() { Dock = DockStyle.Fill };
    bool showCategories;
    HeaderLayers headerLayers;
    readonly HashSet<BookingFilter> bookingFilters=[];
    readonly Label filterResult=new(){AutoSize=false,Width=182,Height=30,ForeColor=ExportDesign.Secondary,TextAlign=ContentAlignment.MiddleLeft};
    readonly FlowLayoutPanel filterSummary=new(){Width=320,Height=34,Visible=false,WrapContents=false};
    readonly FlowLayoutPanel filterPills = new() { AutoSize = true, MaximumSize = new Size(320,0), Visible = false };
    bool unifiedAccounts;
    readonly Dictionary<string,RecurringSummary> recurringCache = new();
    DateOnly? recurringDate;
    internal AppPreferences Preferences { get; private set; }
    internal ThemePalette Theme=>ThemePalette.For(Preferences);
    readonly System.Windows.Forms.Timer refreshTimer = new();
    public MainWindow(bool demo, string? storageDirectory = null,IHelloKeys? helloKeys=null)
    {
        this.demo = demo;this.helloKeys=helloKeys;
        Preferences=demo||storageDirectory!=null?new AppPreferences{Ephemeral=true}:AppPreferences.Load();
        Icon=AppArtwork.CreateTrayIcon();Disposed+=(_,_)=>Icon?.Dispose();
        Paint+=(_,e)=>{if(Theme.Border is {} border){using var pen=new Pen(border,3);e.Graphics.DrawRectangle(pen,1,1,ClientSize.Width-3,ClientSize.Height-3);}};
        store = new EncryptedStore(storageDirectory ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "SimplebankingWindows", demo ? "Demo" : "Live"));
        Text = "simplebanking" + (demo ? " · DEMO" : ""); FormBorderStyle=FormBorderStyle.FixedSingle;MaximizeBox=true;ClientSize = new Size(360, 688);
        StartPosition = FormStartPosition.CenterScreen; Font = new Font("Segoe UI", 13, FontStyle.Regular, GraphicsUnit.Pixel); BackColor = Color.FromArgb(248, 250, 249);
        var header = new Panel { Dock=DockStyle.Top, Height=178,BackColor=Color.Transparent };
        var card = new BalanceCard(this,false) { Dock=DockStyle.Top,Height=140 };
        var accountStrip = new AccountStrip {Location=new Point(12,140),Width=184};
        accountStrip.AccountSelected+=SelectFlyoutAccount;accountStrip.AllSelected+=SelectAllAccounts;
        header.Controls.Add(card);header.Controls.Add(accountStrip);
        BalanceChanged+=()=>{
            accountStrip.Visible=!IsLocked && data.Accounts.Count>1;
            accountStrip.UpdateAccounts(data.Accounts,accounts.SelectedIndex,unifiedAccounts,BalancePresentation.Create(CurrentBalance,Preferences),Theme.BankLogos);
            transactionList.Visible=!IsLocked;
            if(IsLocked)status.Text=NeedsSetup?"Richte simplebanking ein, um dein Konto zu verbinden.":"Mit deinem Masterpasswort entsperren.";
        };
        var toolbar = new FlowLayoutPanel { Name="HeaderTools",Visible=false, Dock = DockStyle.Top, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, Padding = new Padding(12, 5, 12, 5) };
        search.Visible = false; search.PlaceholderText = "Händler, Betrag, Monat …"; search.Width = 300;
        var controlIcons = new FlowLayoutPanel { Location = new Point(268,140), Width = 76, Height = 30, BackColor = Color.Transparent, WrapContents = false };
        controlIcons.Controls.Add(new HeaderIcon("search","Suchen", () => SetHeaderLayers(headerLayers.ToggleSearch()), () => headerLayers.Search));
        controlIcons.Controls.Add(new HeaderIcon("filter","Filter einblenden", () => SetHeaderLayers(headerLayers.ToggleFilter()), () => headerLayers.Filter || bookingFilters.Count>0));
        controlIcons.Controls.Add(new HeaderIcon("categories","Kategorien anzeigen", () => { Preferences.ShowCategories = !Preferences.ShowCategories; Preferences.Save(); ApplyPreferences(); }, () => showCategories));
        foreach(Control icon in controlIcons.Controls){icon.Size=new Size(24,24);icon.Margin=new Padding(0,0,1,0);}
        header.Controls.Add(controlIcons);
        header.SizeChanged += (_,_) => {
            controlIcons.Left = header.ClientSize.Width-controlIcons.Width-4;
            accountStrip.Width = Math.Max(70,controlIcons.Left-28);
        };
        BalanceChanged += () => { controlIcons.Visible = true; controlIcons.Invalidate(true); };
        foreach (var (title, filter) in new[] { ("Alle",BookingFilter.All), ("Einnahmen",BookingFilter.Income), ("Ausgaben",BookingFilter.Expense), ("Fixkosten",BookingFilter.FixedCosts), ("Unkategorisiert",BookingFilter.Uncategorized), ("Vorgemerkt",BookingFilter.Pending), ("Anhänge",BookingFilter.Attachments), ("Erinnerungen",BookingFilter.Reminders), ("Markiert",BookingFilter.Flagged) }) {
            var pill = new FilterPill(title) { Selected=filter==BookingFilter.All, Tag=filter };
            pill.Click+=(_,_)=>{if(filter==BookingFilter.All)bookingFilters.Clear();else if(!bookingFilters.Add(filter))bookingFilters.Remove(filter);RefreshFilterPills();RefreshEntries();};filterPills.Controls.Add(pill);if(filter is BookingFilter.Expense or BookingFilter.Pending)filterPills.SetFlowBreak(pill,true);
        }
        toolbar.Controls.Add(search);
        var more = new ContextMenuStrip();
        foreach (var item in new (string Title, Action Run)[] { ("Entsperren / Einrichten", Unlock), ("Aktualisieren", RefreshBank), ("Einstellungen", OpenSettings), ("Dashboard / Money Mass Index", Dashboard), ("Fixkosten", () => OpenDashboard(1,true)), ("Abos & Verträge", () => OpenDashboard(1)), ("Ausgabenkalender", () => OpenDashboard(2)), ("Geld-Alter", () => OpenDashboard(3)), ("Regeln & Zuordnungen", () => OpenDashboard(4)), ("Regel-Assistent", OpenRuleAssistant), ("Attention Inbox / Erinnerungen", OpenAttentionInbox), ("AI-Assistent", OpenAiChat), ("Bank hinzufügen", AddBank), ("Überweisung", Transfer), ("Zahlungsjournal", OpenPayments), ("Aufrunden / Spartopf", OpenRoundups), ("Überweisungsvorlagen", OpenPaymentTemplates), ("Umsätze importieren …", ImportStatements), ("Ältere Umsätze laden …", LoadHistory), ("simple.report · Monatsübersicht", ExportMonthlyReport), ("Export CSV", ExportCsv), ("Export OFX", ExportOfx), ("Sicherung", Backup), ("Wiederherstellen", Restore), ("Saldo verbergen", () => { hiddenBalance = !hiddenBalance; RefreshEntries(); }), ("Sperren", Lock), ("Demo zurücksetzen", ResetDemo) })
            more.Items.Add(item.Title, null, (_, _) => item.Run());
        var footer=new Panel{Dock=DockStyle.Bottom,Height=46,Padding=new Padding(10,8,10,8),BackColor=Color.FromArgb(248,250,249),Name="TransactionFooter"};
        var footerLeft=new FlowLayoutPanel{Dock=DockStyle.Left,Width=200,WrapContents=false};
        footerLeft.Controls.Add(new HeaderIcon("dashboard","Dashboard",Dashboard));var inboxButton=new HeaderIcon("inbox","Attention Inbox",OpenAttentionInbox);footerLeft.Controls.Add(inboxButton);
        var moreButton = new HeaderIcon("more","Weitere Aktionen", () => more.Show(Cursor.Position));footerLeft.Controls.Add(moreButton);
        foreach(Control icon in footerLeft.Controls){icon.Size=new Size(24,24);icon.Margin=new Padding(0,0,4,0);}
        var sendButton=new HeaderIcon("send","Geld senden",Transfer){Anchor=AnchorStyles.Top|AnchorStyles.Right,Size=new Size(24,24),Location=new Point(314,10)};footer.Controls.Add(footerLeft);footer.Controls.Add(sendButton);
        var sendLabel=new Label{Text="Senden",AutoSize=false,Size=new Size(56,24),TextAlign=ContentAlignment.MiddleLeft,Font=new Font("Segoe UI",14,GraphicsUnit.Pixel),Cursor=Cursors.Hand};sendLabel.Click+=(_,_)=>{if(sendButton.Enabled)Transfer();};footer.Controls.Add(sendLabel);
        void LayoutSend(){sendButton.Location=new Point(footer.ClientSize.Width-96,10);sendLabel.Location=new Point(footer.ClientSize.Width-64,10);}footer.SizeChanged+=(_,_)=>LayoutSend();LayoutSend();BalanceChanged+=()=>sendLabel.Enabled=!IsLocked&&SelectedAccount?.Currency=="EUR";
        BalanceChanged+=()=>{footerLeft.Enabled=!IsLocked;inboxButton.Badge=AttentionCount();inboxButton.Invalidate();sendButton.Enabled=!IsLocked&&SelectedAccount?.Currency=="EUR";};Disposed+=(_,_)=>more.Dispose();toolbar.Controls.Add(filterPills);
        var resetFilters=new LinkLabel{Text="Filter zurücksetzen",AutoSize=true,Margin=new Padding(0,7,0,0),LinkColor=ExportDesign.Accent,LinkBehavior=LinkBehavior.HoverUnderline};resetFilters.LinkClicked+=(_,_)=>{bookingFilters.Clear();RefreshFilterPills();RefreshEntries();};filterSummary.Controls.Add(filterResult);filterSummary.Controls.Add(resetFilters);toolbar.Controls.Add(filterSummary);
        BalanceChanged += () => Invalidate(true);
        transactionList.OpenEntry += OpenEntryDetail;transactionList.RenameMerchant+=RenameMerchant;transactionList.CreateMerchantRule+=SaveMerchantRuleFromEntry;transactionList.ResetMerchant+=entry=>SetMerchantOverride(entry,null);
        transactionList.ToggleRead += entry => {
            var index = data.Entries.FindIndex(e => e.Id == entry.Id && e.AccountId == entry.AccountId);
            if (index < 0 || !Unlocked) return;
            data.Entries[index] = entry with { Read = !entry.Read }; Save(); RefreshEntries();
        };
        status.TextChanged+=(_,_)=>status.Visible=!string.IsNullOrWhiteSpace(status.Text)&&status.Text is not ("Entsperrt" or "Gesperrt" or "DEMO · Beispieldaten")&&!status.Text.StartsWith("Mit deinem")&&!status.Text.StartsWith("Gesperrt ·");
        toolbar.BackColor=footer.BackColor=footerLeft.BackColor=Color.Transparent;
        Controls.Add(transactionList); Controls.Add(toolbar); Controls.Add(header); Controls.Add(status);Controls.Add(footer);
        accounts.SelectedIndexChanged += (_, _) => { unifiedAccounts = false; RefreshEntries(); }; search.TextChanged += (_, _) => RefreshEntries();
        if (demo) { data = DemoData.Create(); password = null; Populate(); status.Text = "DEMO · Beispieldaten"; }
        else { status.Text = "Gesperrt · Zum Entsperren auf … klicken"; balance.Text = "Willkommen"; }
        refreshTimer.Tick += (_, _) => { if (Unlocked && !bankBusy) RefreshBankInBackground(); };
        Disposed += (_, _) => {refreshTimer.Dispose();logoCancellation?.Cancel();};
        Disposed += (_, _) => themeWallpaper?.Dispose();
        ApplyPreferences();
        MinimumSize=MaximumSize=Size;
        if(storageDirectory==null&&!demo){InitializeWindowsIntegration();InitializeAutomation();}
    }
    static Button Button(string text, Action action)
    { var b = new Button { Text = text, AutoSize = true, MinimumSize = new Size(28,28), Height = 28, FlatStyle = FlatStyle.Flat, Margin = new Padding(0,0,4,0) }; b.FlatAppearance.BorderSize = 0; b.Click += (_, _) => action(); return b; }
    Account? SelectedAccount => accounts.SelectedItem as Account;
    bool Unlocked => !forceLockPending && (demo || password != null);
    internal bool IsLocked => !Unlocked;
    internal string UnlockActionText => NeedsSetup ? "Einrichten" : "Entsperren";
    internal void RequestUnlock() { OpenMainWindow(); }
    internal void UnlockVault(string value) { var loaded = store.Load(value); data = loaded; password = value; Populate(); }
    bool RequireUnlocked()
    { if (bankBusy) { MessageBox.Show(this, "Bitte den laufenden Bankvorgang abschließen."); return false; } if (Unlocked) return true; Unlock(); return Unlocked; }
    void Populate()
    {
        ApplyMerchantRules();recurringCache.Clear();
        for(int i=0;i<data.Accounts.Count;i++) {
            var account=data.Accounts[i];
            if(string.IsNullOrEmpty(account.BankName))data.Accounts[i]=account with { BankName=data.Connections.FirstOrDefault(c=>c.Id==account.ConnectionId)?.Name };
        }
        var id = SelectedAccount?.Id; accounts.DataSource = null; accounts.Items.Clear(); accounts.DisplayMember = nameof(Account.Name);
        accounts.Items.AddRange(data.Accounts.Cast<object>().ToArray());
        if (data.Accounts.Count > 0) accounts.SelectedIndex = Math.Max(0, data.Accounts.FindIndex(a => a.Id == id));
        RefreshEntries();
    }
    internal IReadOnlyList<Account> FlyoutAccounts() => Unlocked ? data.Accounts : Array.Empty<Account>();
    internal bool AllAccountsSelected => unifiedAccounts;
    internal int SelectedAccountIndex => accounts.SelectedIndex;
    internal decimal? CurrentBalance => IsLocked ? null : unifiedAccounts ? BalancePresentation.Aggregate(data.Accounts) : SelectedAccount?.BalanceKnown == true ? SelectedAccount.Balance : null;
    [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
    internal RecurringSummary CurrentRecurring { get; private set; } = RecurringSummary.Unknown;
    internal bool NeedsSetup => !demo && !store.Exists;
    internal bool BalancesHidden => hiddenBalance || !Unlocked;
    internal event Action? BalanceChanged;
    internal event Action? NewBookings;
    internal void SelectFlyoutAccount(int index) { if (bankBusy) return; unifiedAccounts = false; if (index >= 0 && index < accounts.Items.Count) accounts.SelectedIndex = index; RefreshEntries(); }
    internal void SelectAllAccounts() { if (bankBusy) return; unifiedAccounts = true; RefreshEntries(); }
    internal void OpenSettings() { if(!RequireUnlocked())return; using var settings = new SettingsWindow(this); settings.ShowDialog(this); }
    internal void BackupFromSettings() => Backup();
    internal void RestoreFromSettings() => Restore();
    internal void AddBankFromSettings() => AddBank();
    internal void UnlockFromSettings() => Unlock();
    internal void ApplyPreferences()
    {
        ThemeStyling.Use(Preferences);
        var theme=Theme;themeWallpaper?.Dispose();themeWallpaper=theme.Image(ClientSize.Width/(DeviceDpi/96f)>=594?"wallpaperWide":"wallpaper")??theme.Image("wallpaper");BackColor=theme.Surface;ForeColor=theme.Ink;Padding=theme.Border!=null?new Padding(3):Padding.Empty;
        void Style(Control root){foreach(Control control in root.Controls){if(control is not BalanceCard){control.BackColor=control is Panel or UserControl or HeaderIcon or FilterPill or Label or BookingRow or DateHeading?Color.Transparent:theme.Surface;control.ForeColor=theme.Ink;if(control is HeaderIcon icon){icon.Tint=theme.Secondary;icon.TextMode=!theme.GlyphControls;}}Style(control);}}
        Style(this);
        recurringCache.Clear();
        ConfigureWindowsIntegration();
        showCategories = Preferences.ShowCategories;
        refreshTimer.Stop();
        if (Preferences.RefreshMinutes is > 0 and <= 360) { refreshTimer.Interval = Preferences.RefreshMinutes * 60000; refreshTimer.Start(); }
        RefreshEntries();
    }
    internal void OpenMainWindow() {
        Show(); WindowState=FormWindowState.Normal;Activate();
        if(IsLocked && !unlockQueued && !unlocking) {
            unlockQueued=true;BeginInvoke(new Action(()=>{unlockQueued=false;if(IsLocked&&!unlocking)Unlock();}));
        }
    }
    internal void RefreshFromFlyout() { RefreshBank(); }
    internal void OpenTransferFromFlyout() { OpenMainWindow(); Transfer(); }
    void RefreshFilterPills(){foreach(var pill in filterPills.Controls.OfType<FilterPill>()){pill.Selected=pill.Tag is BookingFilter f&&(f==BookingFilter.All?bookingFilters.Count==0:bookingFilters.Contains(f));pill.Invalidate();}Invalidate(true);}
    void SetHeaderLayers(HeaderLayers value)
    {
        bool closingSearch = headerLayers.Search && !value.Search;
        Controls.Find("HeaderTools",true).Single().Visible=value.Search||value.Filter;
        headerLayers = value; search.Visible = value.Search; filterPills.Visible = value.Filter; filterSummary.Visible=value.Filter;
        if (closingSearch) search.Clear();
        if (value.Search) search.Focus();
        Invalidate(true);
    }
    void RefreshEntries()
    {
        var account = SelectedAccount;
        var today = DateOnly.FromDateTime(DateTime.Today);
        if(recurringDate!=today) {recurringCache.Clear();recurringDate=today;}
        RecurringSummary Summary(Account a) {
            if(!recurringCache.TryGetValue(a.Id,out var summary))recurringCache[a.Id]=summary=RecurringSummary.Calculate(data.Entries,a,Preferences.FinanceFor(a.Id),today,data.RecurringAssignments,data.Rules);
            return summary;
        }
        var perAccount=data.Accounts.Where(a=>unifiedAccounts || a.Id==account?.Id)
            .ToDictionary(a=>a.Id,Summary);
        if(unifiedAccounts && BalancePresentation.Aggregate(data.Accounts)!=null) {
            var summaries=perAccount.Values.ToList();
            // An account with no history is unknown; do not treat it as zero obligations.
            CurrentRecurring=summaries.Count>0 && summaries.All(s=>s.Outstanding.HasValue)
                ? new(summaries.Sum(s=>s.Outstanding!.Value),summaries.Any(s=>s.BankFee.HasValue)?summaries.Sum(s=>s.BankFee??0):null,null,summaries.SelectMany(s=>s.Payments).ToArray())
                : RecurringSummary.Unknown;
        } else CurrentRecurring=!unifiedAccounts && account!=null?perAccount[account.Id]:RecurringSummary.Unknown;
        balance.Text = account == null ? "Kein Konto eingerichtet" : hiddenBalance ? "••••••" : !account.BalanceKnown ? "Saldo noch nicht abgerufen" : UiFormat.Money(account.Balance, account.Currency);
        if (unifiedAccounts) balance.Text = hiddenBalance ? "••••••" : data.Accounts.Count > 0 && data.Accounts.All(a => a.BalanceKnown) && data.Accounts.Select(a => a.Currency).Distinct().Count() == 1 ? UiFormat.Money(data.Accounts.Sum(a => a.Balance), data.Accounts[0].Currency) : "--,--";
        if (IsLocked) balance.Text = NeedsSetup ? "Willkommen" : "Gesperrt";
        balance.ForeColor = BalancePresentation.Create(CurrentBalance, Preferences).Ink;
        var fixedMerchants=perAccount.SelectMany(pair=>pair.Value.Payments.Select(p=>(pair.Key,p.Merchant))).ToHashSet();
        var entries = BookingFilters.Select(data.Entries,account?.Id,unifiedAccounts,bookingFilters,search.Text,fixedMerchants);
        var currencies=entries.Select(e=>e.Currency).Distinct().ToArray();
        filterResult.Text=$"{entries.Count} Umsätze"+(BalancesHidden||currencies.Length!=1?"":" · "+UiFormat.Money(entries.Sum(e=>e.Amount),currencies[0]));
        transactionList.Populate(entries, BalancesHidden, showCategories, Preferences.PendingAsPill,Theme);
        if(!Preferences.RemoteMerchantLogos)logoCancellation?.Cancel();else LoadRemoteLogos(entries);
        BalanceChanged?.Invoke();
    }
    void Unlock() {
        if(unlocking)return;
        unlocking=true;try { UnlockCore(); } finally {unlocking=false;}
    }
    void UnlockCore()
    {
        if (bankBusy) return;
        if (demo) { MessageBox.Show(this, "Der Demo-Modus verwendet nur Beispieldaten im Arbeitsspeicher."); return; }
        if (!store.Exists) { RunFirstSetup(); return; }
        int epoch=Volatile.Read(ref securityEpoch);

        if(HelloEnabled) {
            try {
                string helloPassword=Hello.Unlock(Handle);
                if(epoch!=Volatile.Read(ref securityEpoch))return;
                UnlockVault(helloPassword);status.Text="";
                if(Preferences.LoadTransactionsOnStart)RefreshBankInBackground();
                return;
            } catch(Exception error) when(error is System.Security.Cryptography.CryptographicException or IOException or UnauthorizedAccessException or System.Text.Json.JsonException or InvalidDataException or OperationCanceledException) {
                if(epoch!=Volatile.Read(ref securityEpoch))return;

            }
        }
        string? TryUnlock(string value,bool enroll) {
            if(epoch!=Volatile.Read(ref securityEpoch))return "Die Sitzung wurde gesperrt. Bitte erneut öffnen.";
            try { UnlockVault(value); if(epoch!=Volatile.Read(ref securityEpoch)){Lock();return "Die Sitzung wurde gesperrt.";} }
            catch(System.Security.Cryptography.CryptographicException){return "Falsches Master-Passwort oder beschädigter Tresor.";}
            catch(Exception e) when(e is IOException or UnauthorizedAccessException or System.Text.Json.JsonException){return "Der Tresor konnte nicht gelesen werden.";}
            if(enroll&&!HelloEnabled) {
                try{Hello.Enable(value,Handle);if(epoch!=Volatile.Read(ref securityEpoch)){Hello.Disable();return "Die Sitzung wurde gesperrt.";}}
                catch(Exception e) when(e is System.Security.Cryptography.CryptographicException or IOException or UnauthorizedAccessException or System.Text.Json.JsonException or OperationCanceledException){if(!HelloVault.Canceled(e))MessageBox.Show(this,"Entsperrt. Windows Hello konnte nicht eingerichtet werden. Du kannst es später unter Einstellungen → Sicherheit erneut versuchen.","Windows Hello");}
            }
            return null;
        }
        string? TryHello(){try{var value=Hello.Unlock(Handle);return TryUnlock(value,false);}catch(Exception e) when(e is System.Security.Cryptography.CryptographicException or IOException or UnauthorizedAccessException or System.Text.Json.JsonException or OperationCanceledException){return HelloVault.Canceled(e)?"Windows Hello abgebrochen. Nutze dein Passwort.":"Windows Hello nicht verfügbar. Nutze dein Passwort.";}}
        using var dialog=new UnlockDialog(HelloEnabled,TryUnlock,TryHello);
        var result=dialog.ShowDialog(this);
        if(dialog.ResetRequested) {
            try {
                Hello.Disable();
                File.Move(store.FilePath,store.FilePath+".reset-"+DateTime.UtcNow.ToString("yyyyMMdd-HHmmss")+"-"+Guid.NewGuid().ToString("N"));
                Lock();RunFirstSetup();
            }catch(Exception e) when(e is IOException or UnauthorizedAccessException or System.Security.Cryptography.CryptographicException){MessageBox.Show(this,"Der Tresor konnte nicht zurückgesetzt werden. Die Einrichtung wurde nicht geändert.");}
            return;
        }
        if(result==DialogResult.OK&&epoch==Volatile.Read(ref securityEpoch)&&Unlocked){status.Text="";if(Preferences.LoadTransactionsOnStart)RefreshBankInBackground();}
    }

    internal void RunFirstSetup()
    {
        using var wizard = new BankSetupWindow(new YaxiSettings(false), true);
        if (wizard.ShowDialog(this) != DialogResult.OK) return;
        if (wizard.DemoRequested) { demo = true; Preferences=new(){Ephemeral=true};data = DemoData.Create(); Populate(); status.Text = "DEMO · Beispieldaten"; return; }
        if (wizard.RestoreRequested) { RestoreInitialBackup(); return; }
        if (wizard.Connection == null || wizard.MasterPassword is not { } masterPassword) return;
        var nextData = new BankData { Yaxi = new YaxiSettings(false) };
        nextData.Connections.Add(wizard.Connection); nextData.Accounts.AddRange(wizard.Accounts);
        store.Save(nextData, masterPassword);
        data = nextData; password = masterPassword; Populate(); RefreshBank();
    }
    public void Lock()
    {
        Interlocked.Increment(ref securityEpoch);
        logoCancellation?.Cancel();
        if (bankBusy) { MessageBox.Show(this, "Bitte den laufenden Bankvorgang zuerst abschließen."); return; }
        if (demo) { Hide(); return; }
        foreach(var form in OwnedForms.ToArray()){form.Close();if(!form.IsDisposed&&form is TransactionDetails)form.Dispose();}
        bankReadAccess.Clear();password = null; data = new(); Populate(); balance.Text = "Gesperrt"; status.Text = "Gesperrt";
    }
    void Save() { if (!demo && password != null) store.Save(data, password); }
    void Transfer() => EditTransfer(SelectedAccount is {} account?data.Drafts.LastOrDefault(d=>d.AccountId==account.Id):null);
    void OpenEntryDetail(Entry entry)
    {
        using var detail = new TransactionDetails(entry, BalancesHidden, changed => {
            var index = data.Entries.FindIndex(e => e.Id == changed.Id && e.AccountId == changed.AccountId);
            if (index < 0 || !Unlocked) return;
            var previous=data.Entries[index];
            data.Entries[index] = changed with {CategoryManual=changed.Category!=entry.Category || changed.CategoryManual};
            try{Save();}catch{data.Entries[index]=previous;throw;}RefreshEntries();
        }, SetMerchantLogo,SaveMerchantRuleFromEntry);
        detail.ShowDialog(this);
    }
    void Dashboard() => OpenDashboard();
    void ExportCsv()
    {
        if (!RequireUnlocked() || SelectedAccount is not { } a) return;
        using var dialog = new SaveFileDialog { Filter = "CSV-Datei|*.csv", FileName = "umsaetze.csv" }; if (dialog.ShowDialog(this) != DialogResult.OK) return;
        static string Field(string s) => "\"" + ((s.Length > 0 && "=+-@\t\r".Contains(s[0])) ? "'" + s : s).Replace("\"", "\"\"") + "\"";
        var lines = new List<string> { "Datum;Empfänger;Verwendungszweck;Kategorie;Betrag;Währung;Notiz" };
        lines.AddRange(data.Entries.Where(e => e.AccountId == a.Id).OrderByDescending(e => e.Date).Select(e => string.Join(";", new[] {
            Field(e.Date.ToString("yyyy-MM-dd")), Field(e.Merchant), Field(e.Purpose), Field(e.Category), e.Amount.ToString(CultureInfo.GetCultureInfo("de-DE")), Field(e.Currency), Field(e.Note) })));
        File.WriteAllLines(dialog.FileName, lines, new System.Text.UTF8Encoding(true)); status.Text = "CSV exportiert · Die Exportdatei ist unverschlüsselt.";
    }
    void Backup()
    {
        if (!RequireUnlocked()) return;
        var p = password ?? Prompt("Sicherungspasswort", "Passwort für die Demo-Sicherung (mindestens 6 Zeichen)", true); if (p == null || p.Length < 6) return;
        using var dialog = new SaveFileDialog { Filter = "Verschlüsselte Sicherung|*.sbw", FileName = "simplebanking-backup.sbw" }; if (dialog.ShowDialog(this) != DialogResult.OK) return;
        try { EncryptedStore.WriteBackup(dialog.FileName,data,p); status.Text = "Verschlüsselte Sicherung erstellt."; }
        catch(Exception e) when(e is IOException or UnauthorizedAccessException) { MessageBox.Show(this,"Sicherung konnte nicht vollständig gespeichert werden. Eine bestehende Sicherung wurde nicht ersetzt."); }
    }
    void Restore()
    {
        if (!RequireUnlocked()) return;
        using var dialog = new OpenFileDialog { Filter = "Verschlüsselte Sicherung|*.sbw" }; if (dialog.ShowDialog(this) != DialogResult.OK) return;
        var p = Prompt("Sicherung öffnen", "Passwort der Sicherung", true); if (p == null) return;
        try
        {
            var restored = EncryptedStore.Decrypt(File.ReadAllBytes(dialog.FileName), p);
            if (MessageBox.Show(this, $"{restored.Accounts.Count} Konten und {restored.Entries.Count} Buchungen ersetzen den aktuellen Bestand. Fortfahren?", "Wiederherstellen", MessageBoxButtons.YesNo) != DialogResult.Yes) return;
            if (!demo && password != null) store.Save(restored, password);
            data = restored; Populate(); status.Text = "Sicherung wiederhergestellt.";
        }
        catch (Exception e) when (e is System.Security.Cryptography.CryptographicException or IOException or System.Text.Json.JsonException)
        { MessageBox.Show(this, "Sicherung ungültig, beschädigt oder Passwort falsch. Vorhandene Daten wurden nicht ersetzt."); }
    }
    void RestoreInitialBackup()
    {
        if (store.Exists || bankBusy) return;
        using var dialog = new OpenFileDialog { Filter = "Verschlüsselte Sicherung|*.sbw" };
        if (dialog.ShowDialog(this) != DialogResult.OK) return;
        var p = Prompt("Sicherung öffnen", "Passwort der Sicherung", true); if (p == null) return;
        try {
            var restored = EncryptedStore.Decrypt(File.ReadAllBytes(dialog.FileName), p);
            store.Save(restored, p); data = restored; password = p; Populate(); status.Text = "Sicherung wiederhergestellt.";
        } catch (Exception e) when (e is System.Security.Cryptography.CryptographicException or IOException or System.Text.Json.JsonException) {
            MessageBox.Show(this, "Sicherung ungültig, beschädigt oder Passwort falsch.");
        }
    }
    internal string? Prompt(string title, string label, bool secret = false, string value = "")
    {
        using var f = new Form { Text = title, Width = 490, Height = 195, StartPosition = FormStartPosition.CenterParent, FormBorderStyle = FormBorderStyle.FixedDialog, MinimizeBox = false, MaximizeBox = false };
        var box = new TextBox { Left = 18, Top = 50, Width = 435, UseSystemPasswordChar = secret, Text = value };
        var ok = new Button { Text = "OK", DialogResult = DialogResult.OK, Left = 270, Top = 95, Width = 85 };
        var cancel = new Button { Text = "Abbrechen", DialogResult = DialogResult.Cancel, Left = 365, Top = 95, Width = 88 };
        f.Controls.AddRange([new Label { Text = label, Left = 18, Top = 18, Width = 435 }, box, ok, cancel]); f.AcceptButton = ok; f.CancelButton = cancel;
        return f.ShowDialog(this) == DialogResult.OK ? box.Text : null;
    }
}
