namespace SimpleBanking;
internal static class UiReview
{
    internal static int Render(string folder)
    {
        Directory.CreateDirectory(folder);
        void Capture(Form form, string name) {
            form.Show(); if(form is not ThemePreview)ThemeStyling.Apply(form);form.PerformLayout(); Application.DoEvents();
            foreach(var card in Descendants(form).OfType<BalanceCard>())card.StopRipple();
            using var bitmap = new Bitmap(form.Width, form.Height);
            form.DrawToBitmap(bitmap, new Rectangle(Point.Empty, form.Size));
            bitmap.Save(Path.Combine(folder, name + ".png")); form.Hide();
        }
        using(var dialog=new UnlockDialog(false,(p,enroll)=>p=="correct"?null:"Falsches Master-Passwort.",()=>"Windows Hello abgebrochen.")) {
            dialog.Show();Application.DoEvents();
            var input=(TextBox)dialog.Controls.Find("UnlockPassword",true).Single();var submit=(Button)dialog.Controls.Find("SubmitUnlock",true).Single();var error=(Label)dialog.Controls.Find("UnlockError",true).Single();
            submit.PerformClick();if(!error.Text.StartsWith("Bitte gib"))throw new InvalidOperationException("Empty password lacks inline validation.");
            input.Text="wrong";submit.PerformClick();if(dialog.DialogResult==DialogResult.OK||error.Text.Length==0)throw new InvalidOperationException("Wrong password dismissed dialog.");
            Capture(dialog,"unlock-error");dialog.Show();
            ((Button)dialog.Controls.Find("RevealPassword",true).Single()).PerformClick();if(input.UseSystemPasswordChar)throw new InvalidOperationException("Password reveal failed.");
            input.Text="correct";submit.PerformClick();if(dialog.DialogResult!=DialogResult.OK)throw new InvalidOperationException("Correct password failed.");
        }
        using(var dialog=new UnlockDialog(true,(p,enroll)=>null,()=>"Windows Hello abgebrochen.")){Capture(dialog,"unlock-hello");}
        var vaultFolder = Path.Combine(Path.GetTempPath(), "simplebanking-unlock-" + Guid.NewGuid().ToString("N"));
        try {
            new EncryptedStore(vaultFolder).Save(DemoData.Create(), "Synthetic-unlock-password");
            using var helloKeys=new SelfTests.HelloFixtureKeys();
            using var locked = new MainWindow(false, vaultFolder,helloKeys);
            locked.Show(); Application.DoEvents();
            var action = locked.Controls.Find("UnlockButton", true).Single();
            if (!locked.IsLocked || !action.Visible || !action.Enabled || action.Text != "Entsperren") throw new InvalidOperationException("Unlock action is not accessible.");
            int prompts=0;
            using(var cancelPrompt=new System.Windows.Forms.Timer{Interval=30}) {
                cancelPrompt.Tick+=(_,_)=>{foreach(var form in Application.OpenForms.Cast<Form>().Where(f=>f is UnlockDialog).ToArray()){prompts++;form.DialogResult=DialogResult.Cancel;}};
                cancelPrompt.Start();locked.OpenMainWindow();locked.OpenMainWindow();
                Application.DoEvents();cancelPrompt.Stop();
            }
            if(prompts!=1||!locked.IsLocked)throw new InvalidOperationException("Automatic unlock prompt was missing or duplicated.");
            Capture(locked, "locked-main");
            using var lockedFlyout = new BalanceFlyout(locked); lockedFlyout.RefreshCards();
            Capture(lockedFlyout, "locked-flyout");
            try { locked.UnlockVault("wrong-password"); throw new InvalidOperationException("Wrong password accepted."); }
            catch (System.Security.Cryptography.CryptographicException) { }
            if (!locked.IsLocked || locked.FlyoutAccounts().Count != 0) throw new InvalidOperationException("Failed unlock exposed data.");
            locked.UnlockVault("Synthetic-unlock-password");
            if (locked.IsLocked || locked.FlyoutAccounts().Count == 0) throw new InvalidOperationException("Unlock failed.");
            var helloFixture=new HelloVault(vaultFolder,helloKeys);helloFixture.Enable("Synthetic-unlock-password",IntPtr.Zero);
            locked.Lock();locked.OpenMainWindow();Application.DoEvents();
            if(locked.IsLocked||helloKeys.Prompts!=2)throw new InvalidOperationException("Configured Hello did not unlock automatically.");
            locked.Preferences.LastTab=4;using(var helloSettings=new SettingsWindow(locked)){Capture(helloSettings,"settings-hello-enabled");}
            helloKeys.AfterUnwrap=locked.Lock;locked.Lock();locked.OpenMainWindow();Application.DoEvents();
            if(!locked.IsLocked)throw new InvalidOperationException("A lock during Hello was overridden by a late grant.");
            helloKeys.AfterUnwrap=null;
            helloKeys.Cancel=true;locked.Lock();
            using(var passwordFallback=new System.Windows.Forms.Timer{Interval=40}) {
                bool shown=false;passwordFallback.Tick+=(_,_)=>{var prompt=Application.OpenForms.Cast<Form>().FirstOrDefault(f=>f is UnlockDialog);if(prompt!=null){shown=true;prompt.DialogResult=DialogResult.Cancel;}};
                passwordFallback.Start();locked.OpenMainWindow();Application.DoEvents();passwordFallback.Stop();
                if(!shown||!locked.IsLocked)throw new InvalidOperationException("Canceled Hello did not offer a locked password fallback.");
            }
            helloKeys.Cancel=false;helloFixture.Disable();locked.UnlockVault("Synthetic-unlock-password");
            lockedFlyout.SetPinned(true);lockedFlyout.Show();
            using var ownedSettings=new SettingsWindow(locked);ownedSettings.Show(locked);
            using var ownedDetails=new TransactionDetails(DemoData.Create().Entries[0],false,_=>throw new IOException("Synthetic failure"));ownedDetails.Show(locked);Descendants(ownedDetails).OfType<TextBox>().Single(c=>c.Name=="BookingNote").Text="Unsaved fixture";
            locked.Lock();if(!ownedDetails.IsDisposed)throw new InvalidOperationException("Lock left booking details visible after failed save."); if(!ownedSettings.IsDisposed)throw new InvalidOperationException("Lock retained an account settings window."); locked.Show(); Application.DoEvents();
            if (!locked.IsLocked || !action.Visible || locked.FlyoutAccounts().Count != 0) throw new InvalidOperationException("Relock failed.");
            if(!lockedFlyout.Visible||!lockedFlyout.IsPinned||Descendants(lockedFlyout).OfType<BalanceCard>().Single().RippleRunning)throw new InvalidOperationException("Locked widget retained an animation or disappeared.");
            Capture(lockedFlyout,"locked-widget");lockedFlyout.SetPinned(false);
        } finally { Directory.Delete(vaultFolder, true); }
        var reportData=DemoData.Create(); MonthlyReport.Write(Path.Combine(folder,"simple.report-demo.pdf"),reportData.Accounts[0],reportData.Entries,reportData.Entries.Max(e=>e.Date));
        using var main = new MainWindow(true);
        if (main.CurrentBalance != 3284.52m) throw new InvalidOperationException("Initial account selection is missing.");
        main.SelectAllAccounts();
        if (main.CurrentBalance != main.FlyoutAccounts().Sum(a => a.Balance)) throw new InvalidOperationException("Unified account selection failed.");
        main.SelectFlyoutAccount(0);
        if (main.AllAccountsSelected || main.CurrentBalance != 3284.52m) throw new InvalidOperationException("Returning to a single account failed.");
        Capture(main, "transactions");
        main.Show();Application.DoEvents();
        Descendants(main).OfType<HeaderIcon>().Single(c=>c.AccessibleName=="Filter einblenden").PerformClick();
        var pills=Descendants(main).OfType<FilterPill>().ToArray();pills.Single(p=>p.Text=="Vorgemerkt").PerformClick();pills.Single(p=>p.Text=="Ausgaben").PerformClick();
        if(pills.Count(p=>p.Selected)!=2)throw new InvalidOperationException("Filter selection is not combinable.");Capture(main,"transactions-filter");
        main.Show();pills.Single(p=>p.Text=="Alle").PerformClick();if(pills.Count(p=>p.Selected)!=1)throw new InvalidOperationException("All did not reset filters.");Descendants(main).OfType<HeaderIcon>().Single(c=>c.AccessibleName=="Filter einblenden").PerformClick();

        using(var chatTimer=new System.Windows.Forms.Timer{Interval=80}) {
            bool reviewed=false;
            chatTimer.Tick+=async(_,_)=>{
                var chat=Application.OpenForms.Cast<Form>().FirstOrDefault(f=>f.Text.StartsWith("AI-Assistent ·"));if(chat==null)return;chatTimer.Stop();
                var input=Descendants(chat).OfType<TextBox>().Single(t=>!t.ReadOnly);
                var send=Descendants(chat).OfType<Button>().Single(b=>b.Text=="Fragen");
                input.Text="Wie hoch sind meine Ausgaben?";send.PerformClick();
                for(int n=0;!send.Enabled&&n<200;n++)await Task.Delay(25);
                input.Text="Und meine Einnahmen?";send.PerformClick();
                for(int n=0;!send.Enabled&&n<200;n++)await Task.Delay(25);
                var transcript=Descendants(chat).OfType<TextBox>().Single(t=>t.ReadOnly).Text;
                if(!transcript.Contains("Wie hoch")||!transcript.Contains("Und meine")||!transcript.Contains("Lokale Demo-Auswertung"))throw new InvalidOperationException("Demo conversation lost its history.");
                using var bitmap=new Bitmap(chat.Width,chat.Height);chat.DrawToBitmap(bitmap,new Rectangle(Point.Empty,chat.Size));bitmap.Save(Path.Combine(folder,"ai-conversation-demo.png"));reviewed=true;chat.Close();
            };
            chatTimer.Start();main.OpenAiChat();if(!reviewed)throw new InvalidOperationException("AI conversation review missing.");
        }
        bool categories = main.Preferences.ShowCategories;
        main.Preferences.ShowCategories = true; main.ApplyPreferences(); Capture(main,"transactions-categories");
        main.Show();Application.DoEvents();
        if(main.FormBorderStyle!=FormBorderStyle.FixedSingle||!main.MaximizeBox||main.Controls.Find("WindowTitleBar",true).Length!=0)throw new InvalidOperationException("Native fixed title bar was not restored.");
        main.ToggleWindowWidth();if(main.ClientSize.Width!=840||main.ClientSize.Height!=688||main.MinimumSize!=main.MaximumSize)throw new InvalidOperationException("Wide preset geometry failed.");
        Capture(main,"transactions-wide");main.ToggleWindowWidth();if(main.ClientSize.Width!=360)throw new InvalidOperationException("Narrow preset failed.");
        main.ClientSize = new Size(360,688); main.Preferences.ShowCategories = categories; main.ApplyPreferences();
        using var rowFixture = new Form { ClientSize = new Size(348,160), Text = "Buchungszustände" };
        var fixtureList = new TransactionList { Dock = DockStyle.Fill }; rowFixture.Controls.Add(fixtureList);
        var fixtureEntry = DemoData.Create().Entries[0] with { Pending = true, Note = "Beispielnotiz", Category = "Essen & Alltag" };
        fixtureList.Populate([fixtureEntry],false,true); fixtureList.SelectEntry(fixtureEntry);
        Capture(rowFixture,"transaction-pending-selected");
        rowFixture.Show();Application.DoEvents();
        var history=Enumerable.Range(0,100000).Select(i=>fixtureEntry with {Id="history-"+i,Date=DateOnly.FromDateTime(DateTime.Today).AddDays(-i/20)}).ToArray();
        fixtureList.Populate(history,false,true);
        if(fixtureList.TotalEntries!=100000||fixtureList.RealizedCount>30)throw new InvalidOperationException("History allocated offscreen controls.");
        if(fixtureList.ScrollbarShown)throw new InvalidOperationException("Scrollbar visible before scrolling.");
        var viewport=Descendants(fixtureList).OfType<Panel>().Single();
        typeof(Control).GetMethod("OnMouseWheel",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic)!.Invoke(viewport,[new MouseEventArgs(MouseButtons.None,0,10,10,-120)]);
        if(fixtureList.ScrollOffset==0)throw new InvalidOperationException("Mouse wheel did not scroll the virtual list.");
        fixtureList.ScrollTo(1500000);Application.DoEvents();
        if(!fixtureList.ScrollbarShown)throw new InvalidOperationException("Scrolling did not reveal the scrollbar.");
        var idleScroll=System.Diagnostics.Stopwatch.StartNew();while(idleScroll.ElapsedMilliseconds<1300){Application.DoEvents();System.Threading.Thread.Sleep(10);}
        if(fixtureList.ScrollbarShown)throw new InvalidOperationException("Scrollbar remained after scrolling ended.");
        if(fixtureList.ScrollOffset<1000000||fixtureList.RealizedCount>30||fixtureList.RealizedCount==0)throw new InvalidOperationException("Virtual history scrolling failed.");
        int historyOffset=fixtureList.ScrollOffset;fixtureList.Populate(history,false,true);
        if(fixtureList.ScrollOffset!=historyOffset)throw new InvalidOperationException("Refreshing history lost scroll position.");
        Capture(rowFixture,"history-100000");
        fixtureList.Populate([fixtureEntry],false,true);
        using var flyout = new BalanceFlyout(main); flyout.RefreshCards(); Capture(flyout, "flyout");
        flyout.Location=new Point(90,100);flyout.Show();
        var pin=Descendants(flyout).OfType<Button>().Single(b=>b.AccessibleName=="Im Vordergrund halten");pin.PerformClick();
        typeof(Form).GetMethod("OnDeactivate",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic)!.Invoke(flyout,[EventArgs.Empty]);
        if(!flyout.Visible||!flyout.IsPinned||!flyout.TopMost||!main.Preferences.WidgetEnabled)throw new InvalidOperationException("Pinned widget disappeared after focus loss.");
        flyout.SetWidgetHover(false);var widgetCard=Descendants(flyout).OfType<BalanceCard>().Single();
        if(!widgetCard.PrivacyHidden||main.BalancesHidden)throw new InvalidOperationException("Widget privacy affected the main window or failed.");
        using(var privateImage=widgetCard.CreateSurface())privateImage.Save(Path.Combine(folder,"widget-private.png"));
        flyout.SetWidgetHover(true);if(widgetCard.PrivacyHidden)throw new InvalidOperationException("Hover did not reveal widget balance.");
        using(var revealedImage=widgetCard.CreateSurface())revealedImage.Save(Path.Combine(folder,"widget-revealed.png"));
        if(!Descendants(flyout).OfType<Button>().Single(b=>b.AccessibleName=="Widget schließen").Visible)throw new InvalidOperationException("Widget close action missing.");
        Capture(flyout,"desktop-widget");
        using(var restoredWidget=new BalanceFlyout(main)){restoredWidget.RestoreWidget();if(!restoredWidget.IsPinned||restoredWidget.Location!=new Point(90,100))throw new InvalidOperationException("Widget position did not restore.");restoredWidget.Hide();}
        flyout.Show();pin.PerformClick();if(flyout.IsPinned||!flyout.Visible)throw new InvalidOperationException("Unpin must release the pin without closing the active flyout.");flyout.Hide();
        flyout.SetPinned(true);flyout.Show();Descendants(flyout).OfType<Button>().Single(b=>b.AccessibleName=="Widget schließen").PerformClick();
        if(flyout.Visible||flyout.IsPinned||main.Preferences.WidgetEnabled)throw new InvalidOperationException("Widget close did not clear its persistent state.");
        flyout.ToggleQuickSend();Capture(flyout,"quick-send");flyout.Show();
        var quick=Descendants(flyout).OfType<QuickSendPanel>().Single();
        Descendants(quick).OfType<Button>().Single(b=>b.AccessibleName=="Vorlage Gemeinsames Essen").PerformClick();
        if(!Descendants(quick).OfType<Button>().Single(b=>b.Text=="Prüfen").Enabled)throw new InvalidOperationException("Quick Send favorite did not fill a valid draft.");
        Capture(flyout,"quick-send-filled");flyout.Show();Application.DoEvents();
        var fixtureData=(BankData)typeof(MainWindow).GetField("data",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic)!.GetValue(main)!;
        int paymentCount=fixtureData.Payments.Count;
        Descendants(quick).OfType<Button>().Single(b=>b.Text=="Prüfen").PerformClick();
        if(fixtureData.Payments.Count!=paymentCount||!quick.Controls.Find("QuickSendReview",true).Any())throw new InvalidOperationException("Review must freeze the draft without sending.");
        Capture(flyout,"quick-send-review");flyout.Show();Application.DoEvents();
        Descendants(quick).OfType<Button>().Single(b=>b.Text=="Zurück").PerformClick();
        if(quick.Controls.Find("QuickSendReview",true).Any()||!Descendants(quick).OfType<Button>().Single(b=>b.Text=="Prüfen").Enabled)throw new InvalidOperationException("Back lost the draft.");
        Descendants(quick).OfType<Button>().Single(b=>b.Text=="Prüfen").PerformClick();main.SelectFlyoutAccount(1);
        if(quick.Controls.Find("QuickSendReview",true).Any()||fixtureData.Payments.Count!=paymentCount)throw new InvalidOperationException("Changing the source account retained a payment confirmation.");
        main.SelectFlyoutAccount(0);Descendants(quick).OfType<Button>().First(b=>b.AccessibleName?.StartsWith("Vorlage ")==true).PerformClick();
        Descendants(quick).OfType<Button>().Single(b=>b.Text=="Prüfen").PerformClick();
        Descendants(quick).OfType<Button>().Single(b=>b.Text=="Demo simulieren").PerformClick();Application.DoEvents();
        if(fixtureData.Payments.Count!=paymentCount+1||!fixtureData.Payments.Last().Demo||quick.Busy)throw new InvalidOperationException("Inline demo must record exactly one simulated payment.");
        Capture(flyout,"quick-send-result");main.SelectFlyoutAccount(1);
        if(Descendants(quick).OfType<TextBox>().Any(t=>t.AccessibleName is "Empfänger" or "IBAN" or "Verwendungszweck"&&t.Text.Length>0))throw new InvalidOperationException("Quick Send retained another account's recipient.");
        main.SelectAllAccounts();if(Descendants(flyout).OfType<QuickSendPanel>().Any())throw new InvalidOperationException("Quick Send remains available without a single source account.");main.SelectFlyoutAccount(0);
        var footer=main.Controls.Find("TransactionFooter",true).Single();
        foreach(var icon in Descendants(footer).OfType<HeaderIcon>())if(icon.Width!=24||icon.Height!=24||icon.Right>icon.Parent!.ClientSize.Width)throw new InvalidOperationException("Footer icons must stay visible at search/filter size.");
        foreach(string action in new[]{"Dashboard","Attention Inbox","Weitere Aktionen","Geld senden"})if(!Descendants(footer).Any(c=>c.AccessibleName==action))throw new InvalidOperationException("Missing footer action: "+action);
        using(var panelCard=new BalanceCard(main,false){Size=new Size(348,140)})
        using(var trayCard=new BalanceCard(main,true){Size=new Size(348,140)}) {
            int panelMode=main.Preferences.PanelSubtitleStyle,flyoutMode=main.Preferences.FlyoutSubtitleStyle;
            main.Preferences.PanelSubtitleStyle=main.Preferences.FlyoutSubtitleStyle=0;
            using var panelBitmap=panelCard.CreateSurface();using var trayBitmap=trayCard.CreateSurface();
            using var mood=new Bitmap(348,620);using(var graphics=Graphics.FromImage(mood))main.PaintMood(graphics,new Rectangle(0,0,348,620));
            if(mood.GetPixel(347,610).ToArgb()==main.Theme.Surface.ToArgb())throw new InvalidOperationException("Money Mood does not extend through the entire window.");
            foreach(float t in new[]{0f,.2f,.5f,1f,1.5f}) {using var frame=Ripple.Render(panelBitmap,new PointF(190,65),t);frame.Save(Path.Combine(folder,"ripple-"+t.ToString("0.0",System.Globalization.CultureInfo.InvariantCulture)+".png"));}
            main.Preferences.PanelSubtitleStyle=panelMode;main.Preferences.FlyoutSubtitleStyle=flyoutMode;
        }
        main.Show();Application.DoEvents();
        var animatedCard=Descendants(main).OfType<BalanceCard>().Single();animatedCard.StartRipple();
        if(!animatedCard.RippleRunning)throw new InvalidOperationException("Ripple did not start.");
        var elapsed=System.Diagnostics.Stopwatch.StartNew();while(elapsed.ElapsedMilliseconds<1800){Application.DoEvents();System.Threading.Thread.Sleep(10);}
        if(animatedCard.RippleRunning)throw new InvalidOperationException("Ripple timer stays active at idle.");main.Hide();
        using(var gallery=new Form{ClientSize=new Size(680,480),Text="Mitgelieferte Icons"}) {
            var grid=new FlowLayoutPanel{Dock=DockStyle.Fill,AutoScroll=true};gallery.Controls.Add(grid);
            foreach(var id in new[]{"sparkasse","volksbank","deutsche-bank","commerzbank","postbank","ing","dkb","bunq","n26","c24"}) {
                var logo=BankArtwork.LoadLogo(new Account("x",id,"","EUR",0,LogoId:id),40)??throw new InvalidOperationException("Missing bank logo: "+id);
                var item=new PictureBox{Image=logo,Size=new Size(52,52),SizeMode=PictureBoxSizeMode.CenterImage};item.Disposed+=(_,_)=>logo.Dispose();grid.Controls.Add(item);
            }
            foreach(var symbol in new[]{"settings","refresh","search","filter","categories","all","more","copy","close","subtitle","metrics","daily","bankfees"})grid.Controls.Add(new HeaderIcon(symbol,symbol,()=>{}));
            Capture(gallery,"icons");
        }
        main.Hide();flyout.Show();Application.DoEvents();
        var doubleCard=Descendants(flyout).OfType<BalanceCard>().Single();
        typeof(Control).GetMethod("OnDoubleClick",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic)!.Invoke(doubleCard,[EventArgs.Empty]);
        if(!main.Visible||flyout.Visible)throw new InvalidOperationException("Flyout double-click did not open transactions.");main.Hide();
        int oldMode=main.Preferences.FlyoutSubtitleStyle;
        foreach(int mode in new[]{1,2}) { main.Preferences.FlyoutSubtitleStyle=mode;flyout.RefreshCards();Capture(flyout,"flyout-subtitle-"+mode); }
        main.Preferences.FlyoutSubtitleStyle=oldMode;
        foreach(string themeId in new[]{"sunrise","gameboy","btx"}){main.Preferences.ThemeId=themeId;main.ApplyPreferences();Capture(main,"theme-"+themeId);flyout.RefreshCards();Capture(flyout,"theme-flyout-"+themeId);if(themeId=="btx"){flyout.Show();Descendants(flyout).OfType<BalanceCard>().Single().StartRipple();if(Descendants(flyout).OfType<BalanceCard>().Single().RippleRunning)throw new InvalidOperationException("BTX started a disabled ripple.");flyout.Hide();}}
        main.Preferences.ThemeId="default";main.Preferences.DarkAppearance=true;main.ApplyPreferences();Capture(main,"theme-dark");main.Preferences.DarkAppearance=false;main.ApplyPreferences();
        using(var dashboard=new DashboardWindow(main)) {
            for(int tab=0;tab<6;tab++){dashboard.ShowTab(tab);Capture(dashboard,"dashboard-"+tab);}dashboard.ShowSubscriptionMode("Statistik");Capture(dashboard,"subscriptions-statistics");dashboard.ShowSubscriptionMode("Kalender");Capture(dashboard,"subscriptions-calendar");
        }
        using(var assistant=new RuleAssistant(main,main.AnalysisSnapshot()))Capture(assistant,"rule-assistant");
        void Modal(Action open,string title,string filename){using var timer=new System.Windows.Forms.Timer{Interval=100};bool captured=false;timer.Tick+=(_,_)=>{var form=Application.OpenForms.Cast<Form>().FirstOrDefault(f=>f.Text==title);if(form==null)return;timer.Stop();Capture(form,filename);captured=true;form.Close();};timer.Start();open();timer.Stop();if(!captured)throw new InvalidOperationException("Modal review not captured: "+title);}
        Modal(main.OpenAttentionInbox,"Attention Inbox","attention-inbox");
        Modal(main.OpenPayments,"simplesend · Demo-Journal","payment-journal");
        Modal(main.OpenPaymentTemplates,"simplesend · Vorlagen","payment-templates");
        using(var merchantEditor=new MerchantRuleEditor(new("review",true,100,"Empfänger","Enthält","rewe","Mein Supermarkt"),DemoData.Create().Entries))Capture(merchantEditor,"merchant-rule-editor");
        Modal(main.OpenMerchantRules,"Händler-Regeln","merchant-rules");
        Modal(main.OpenRoundups,"Aufrunden · Spartopf","roundups");
        Modal(main.OpenAiChat,"AI-Assistent · DEMO","ai-demo");
        using(var updater=new UpdateWindow(true)){Capture(updater,"p2-updater");updater.Show();Descendants(updater).OfType<Button>().Single(b=>b.Text=="Nach Updates suchen").PerformClick();Capture(updater,"p2-updater-demo");}
        using(var clientEditor=new AutomationClientEditor(fixtureData.Accounts))Capture(clientEditor,"p2-mcp-client-editor");
        var automation=AutomationAccess.Create("Demo-Assistent",["accounts","drafts"],[fixtureData.Accounts[0].Id],90);fixtureData.AutomationClients.Add(automation.Client);
        Modal(main.ManageAutomationClients,"CLI / MCP · Clients","p2-mcp-clients");
        main.Preferences.AutomationEnabled=true;int oldDrafts=fixtureData.Drafts.Count,oldPayments=fixtureData.Payments.Count;
        var draftArgs=new System.Text.Json.Nodes.JsonObject{["account_id"]=fixtureData.Accounts[0].Id,["recipient"]="Demo-Empfänger",["iban"]="DE89370400440532013000",["amount"]=12.34m,["purpose"]="Synthetischer MCP-Test",["request_id"]="review"};
        var request=new System.Text.Json.Nodes.JsonObject{["name"]="prepare_transfer",["token"]=automation.Token,["arguments"]=draftArgs};
        main.DispatchAutomation(request);main.DispatchAutomation(request);
        if(fixtureData.AutomationDrafts.Count!=1||fixtureData.Drafts.Count!=oldDrafts||fixtureData.Payments.Count!=oldPayments)throw new InvalidOperationException("MCP request executed a payment or created duplicate drafts.");
        main.Preferences.AutomationEnabled=false;bool denied=false;try{main.DispatchAutomation(request);}catch(InvalidOperationException){denied=true;}if(!denied)throw new InvalidOperationException("Disabled automation accepted a request.");
        using(var reviewTimer=new System.Windows.Forms.Timer{Interval=80}){
            reviewTimer.Tick+=(_,_)=>{
                var list=Application.OpenForms.Cast<Form>().FirstOrDefault(f=>f.Text=="MCP · Überweisungsentwürfe prüfen");if(list==null)return;reviewTimer.Stop();
                Capture(list,"p2-mcp-drafts");list.Show();
                using var approveTimer=new System.Windows.Forms.Timer{Interval=80};approveTimer.Tick+=(_,_)=>{
                    var preview=Application.OpenForms.Cast<Form>().FirstOrDefault(f=>f.Text=="Entwurf von Demo-Assistent");if(preview==null)return;approveTimer.Stop();Capture(preview,"p2-mcp-draft-approval");preview.Show();Descendants(preview).OfType<Button>().Single(b=>b.Text=="Als Entwurf freigeben").PerformClick();
                };approveTimer.Start();Descendants(list).OfType<Button>().Single(b=>b.Text=="Ausgewählten Entwurf prüfen").PerformClick();list.Close();
            };reviewTimer.Start();main.ReviewAutomationDrafts();
        }
        if(fixtureData.Drafts.Count!=oldDrafts+1||fixtureData.Payments.Count!=oldPayments||fixtureData.AutomationDrafts.Single().State!="Als Entwurf freigegeben")throw new InvalidOperationException("Explicit MCP draft approval failed or executed a payment.");
        using(var templateEditor=new PaymentTemplateEditor(DemoData.Create().Accounts[0].Id,DemoData.Create().PaymentTemplates[0],0)){Capture(templateEditor,"template-editor");templateEditor.Show();Descendants(templateEditor).OfType<TextBox>().Single(t=>t.AccessibleName=="Symbol").Text="🏠";Descendants(templateEditor).OfType<Button>().Single(t=>t.Text=="Speichern").PerformClick();if(templateEditor.Result?.Symbol!="🏠"||templateEditor.Result.Draft.AccountId!=DemoData.Create().Accounts[0].Id)throw new InvalidOperationException("Template edit lost symbol or source account.");}
        using(var transfer=new TransferWindow(DemoData.Create().Accounts[0],DemoData.Create().PaymentTemplates[0].Draft,true)){
            Capture(transfer,"transfer-demo");transfer.Show();Descendants(transfer).OfType<Button>().Single(b=>b.Text=="Demo-Versand prüfen").PerformClick();if(!transfer.SendRequested||transfer.Draft?.Amount!=25)throw new InvalidOperationException("Demo transfer review did not preserve draft.");transfer.Hide();
        }
        string selectedTheme=main.Preferences.ThemeId;using var settings = new SettingsWindow(main); Capture(settings, "settings");
        settings.ShowReviewTab(3); Capture(settings,"settings-behavior");if(main.Preferences.ThemeId!=selectedTheme)throw new InvalidOperationException("Opening settings changed the selected theme.");
        settings.ShowReviewTab(1); Capture(settings,"settings-accounts");
        for(int tab=0;tab<8;tab++){settings.ShowReviewTab(tab);Capture(settings,"settings-tab-"+tab);}
        using var setup = new BankSetupWindow(new(false), true); Capture(setup, "welcome");
        setup.Show(); setup.VerifyNavigation(); setup.Hide();
        setup.Show(); setup.VerifyApproval(Capture); setup.Hide();
        setup.ShowReviewStep(1); Capture(setup, "master-password");
        setup.ShowReviewStep(3); Capture(setup, "credentials");
        using var bank = new BankSetupWindow(new(false)); Capture(bank, "bank-search");
        var original = DemoData.Create().Entries[0]; Entry? saved = null;
        using var details = new TransactionDetails(original, false, e => saved = e, (e,bytes)=>e with{MerchantLogo=bytes,CustomLogo=null}); Capture(details, "booking-details");
        static IEnumerable<Control> Descendants(Control root) => root.Controls.Cast<Control>().SelectMany(c => new[] { c }.Concat(Descendants(c)));
        details.Show();
        var datePanel=details.Controls.Find("BookingDates",true).Single();var valueDate=details.Controls.Find("ValueDate",true).Single();if(!valueDate.Visible||valueDate.Right>datePanel.Width)throw new InvalidOperationException("Value date is clipped.");
        var reminderButton=Descendants(details).OfType<Button>().Single(b=>b.AccessibleName=="Erinnerung bearbeiten");
        using(var reminderTimer=new System.Windows.Forms.Timer{Interval=30}){
            reminderTimer.Tick+=(_,_)=>{var dialog=Application.OpenForms.Cast<Form>().FirstOrDefault(f=>f.Text=="Erinnerung");if(dialog==null)return;reminderTimer.Stop();Descendants(dialog).OfType<DateTimePicker>().Single().Value=DateTime.Today.AddDays(2).AddHours(10);Capture(dialog,"booking-reminder");dialog.Show();Descendants(dialog).OfType<Button>().Single(b=>b.Text=="Speichern").PerformClick();};
            reminderTimer.Start();reminderButton.PerformClick();
        }
        if(saved?.ReminderAt!=DateTime.Today.AddDays(2).AddHours(10))throw new InvalidOperationException("Reminder icon did not save selected date.");
        if(!Descendants(details).OfType<Button>().Any(b=>b.AccessibleName=="Händlerlogo ändern")||Descendants(details).Any(c=>c.Text=="Bild auswählen …"))throw new InvalidOperationException("Logo action must live on the amount artwork.");
        var category = Descendants(details).OfType<ComboBox>().Single(c=>c.Name!="LogoScope"); category.SelectedItem = "Shopping";
        if (saved?.Category != "Shopping" || saved.Id != original.Id || saved.AccountId != original.AccountId) throw new InvalidOperationException("Detail category save changed the wrong booking.");
        var note = Descendants(details).OfType<TextBox>().Single(c => c.Name=="BookingNote"); note.Text = "Synthetische Prüfnotiz";
        var scroller = details.Controls.OfType<FlowLayoutPanel>().Single(); scroller.ScrollControlIntoView(note.Parent!); Application.DoEvents(); if(!details.FlushNote())throw new InvalidOperationException("Note flush failed.");
        if (saved?.Note != note.Text || saved.Category != "Shopping") throw new InvalidOperationException("Detail note save lost the category.");
        scroller.AutoScrollPosition = new Point(0,scroller.VerticalScroll.Maximum);
        Capture(details,"booking-details-note");
        bool failNote=true;Entry? recoveredNote=null;
        using(var failingDetails=new TransactionDetails(original,false,e=>{if(failNote)throw new IOException("Synthetic storage failure");recoveredNote=e;})) {
            failingDetails.Show();var input=Descendants(failingDetails).OfType<TextBox>().Single(c=>c.Name=="BookingNote");input.Text="Notiz trotz Speicherfehler erhalten";
            if(failingDetails.FlushNote())throw new InvalidOperationException("Failed note write reported success.");
            failingDetails.Close();if(failingDetails.IsDisposed||input.Text!="Notiz trotz Speicherfehler erhalten")throw new InvalidOperationException("Failed close lost the note draft.");
            Capture(failingDetails,"booking-note-save-error");failingDetails.Show();failNote=false;failingDetails.Close();
            if(recoveredNote?.Note!="Notiz trotz Speicherfehler erhalten")throw new InvalidOperationException("Note retry lost its draft.");
        }
        using(var empty=new Form{ClientSize=new Size(360,260)}){var list=new TransactionList{Dock=DockStyle.Fill};empty.Controls.Add(list);list.Populate([],false,true);Capture(empty,"transactions-empty");if(!Descendants(empty).Any(c=>c is EmptyBookings))throw new InvalidOperationException("Missing empty state.");}
        using(var enriched=new TransactionDetails(DemoData.Create().Entries.Single(e=>e.Id=="demo-reminder"),false,_=>{})){enriched.Show();var scroll=enriched.Controls.OfType<FlowLayoutPanel>().Single();scroll.AutoScrollPosition=new Point(0,scroll.VerticalScroll.Maximum);Capture(enriched,"booking-attachments");}
        using(var wallpaper=new Bitmap(24,24)){
            using(var graphics=Graphics.FromImage(wallpaper)){graphics.Clear(Color.FromArgb(235,244,255));using var pen=new Pen(Color.FromArgb(212,228,246));graphics.DrawLine(pen,0,0,24,24);}
            using var bytes=new MemoryStream();wallpaper.Save(bytes,System.Drawing.Imaging.ImageFormat.Png);
            var values=ThemeFiles.Read("id=preview\nname=Windows Blau\naccent=#2468A0\npositive=#236548\ncardLight=#EBF4FF\ncardDark=#203040\ninkLight=#17334A\ninkDark=#EAF3FA\nwallpaper=paper.png\nwallpaperDark=dark.png\n");
            using(var graphics=Graphics.FromImage(wallpaper))graphics.Clear(Color.FromArgb(32,48,64));using var darkBytes=new MemoryStream();wallpaper.Save(darkBytes,System.Drawing.Imaging.ImageFormat.Png);
            var package=new ThemePackage(values,new(){["paper.png"]=bytes.ToArray(),["dark.png"]=darkBytes.ToArray()});package.Write(Path.Combine(folder,"sample-theme.zip"));
            string selected=main.Preferences.ThemeId;using(var preview=new ThemePreview(package))Capture(preview,"theme-import-preview");if(main.Preferences.ThemeId!=selected)throw new InvalidOperationException("Preview changed the active theme.");
            using var themed=new MainWindow(true);themed.Preferences.ThemeId="custom";themed.Preferences.CustomTheme=package.Values;themed.Preferences.CustomThemeImages=package.Images;themed.ApplyPreferences();Capture(themed,"theme-package-transactions");
            themed.Preferences.ThemeId="default";themed.ApplyPreferences();Capture(themed,"theme-package-reset");
        }
        foreach(var themeId in new[]{"default","btx"}){
            main.Preferences.ThemeId=themeId;main.Preferences.DarkAppearance=true;main.ApplyPreferences();
            using var themedSettings=new SettingsWindow(main);themedSettings.ShowReviewTab(3);Capture(themedSettings,"p1-settings-"+themeId);
            using var themedDashboard=new DashboardWindow(main);themedDashboard.ShowFixedCosts();Capture(themedDashboard,"p1-fixed-"+themeId);themedDashboard.ShowTab(3);Capture(themedDashboard,"p1-age-"+themeId);themedDashboard.ShowTab(0);Capture(themedDashboard,"p1-mmi-"+themeId);
            using var rules=new RuleAssistant(main,main.AnalysisSnapshot());Capture(rules,"p1-rules-"+themeId);
            using var themedDetails=new TransactionDetails(DemoData.Create().Entries[0],false,_=>{});Capture(themedDetails,"p1-details-"+themeId);
        }
        main.Preferences.ThemeId="btx";main.Preferences.BtxCrtEnabled=true;main.ApplyPreferences();main.Show();ThemeStyling.Refresh();Application.DoEvents();
        var crt=main.OwnedForms.OfType<BtxCrtOverlay>().Single();if(!crt.Visible)throw new InvalidOperationException("BTX overlay did not open.");
        main.Hide();Application.DoEvents();if(crt.Visible)throw new InvalidOperationException("BTX overlay remained visible after hiding its owner.");
        main.Show();Application.DoEvents();if(!crt.Visible)throw new InvalidOperationException("BTX overlay did not follow its owner.");
        main.Preferences.ThemeId="default";main.Preferences.BtxCrtEnabled=false;main.Preferences.DarkAppearance=false;main.ApplyPreferences();
        if(!crt.IsDisposed)throw new InvalidOperationException("BTX overlay survived switching themes.");main.Hide();
        return 0;
    }
}
