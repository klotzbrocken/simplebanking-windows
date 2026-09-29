using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace SimpleBanking;

internal sealed class BankSetupWindow : Form
{
    readonly YaxiSettings settings;
    readonly TextBox query = new() { PlaceholderText = "Bankname oder IBAN", Dock = DockStyle.Fill };
    readonly ListBox banks = new() { Dock = DockStyle.Fill, DisplayMember = "Name" };
    readonly TextBox user = new() { Dock = DockStyle.Fill };
    readonly TextBox pin = new() { Dock = DockStyle.Fill, UseSystemPasswordChar = true };
    readonly Label userLabel = new() { Text = "Benutzerkennung", AutoSize = true };
    readonly Label pinLabel = new() { Text = "Bank-PIN", AutoSize = true };
    readonly Label message = new() { AutoSize = true, MaximumSize = new Size(580, 0), ForeColor = Color.FromArgb(105,108,115) };
    readonly Label stepTitle = new() { Text = "Bank verbinden", AutoSize = true, Font = new Font("Segoe UI", 18, FontStyle.Bold), ForeColor = Color.FromArgb(35,37,43) };
    readonly Label stepSubtitle = new() { Text = "Suche deine Bank und verbinde dein Konto", AutoSize = true, ForeColor = Color.FromArgb(105,108,115) };
    readonly ProgressBar progress = new() { Minimum = 0, Maximum = 5, Value = 1, Height = 6, Dock = DockStyle.Fill };
    readonly Button search = new() { Text = "Bank suchen", AutoSize = true };
    readonly Button connect = new() { Text = "Konten abrufen", AutoSize = true };
    readonly CancellationTokenSource lifetime = new();
    readonly System.Windows.Forms.Timer searchDebounce = new() { Interval = 350 };
    bool busy;
    public BankConnection? Connection { get; private set; }
    public List<Account> Accounts { get; private set; } = [];
    sealed record BankChoice(string Name, JsonObject Model) { public override string ToString() => Name; }
    readonly Panel page = new() { Dock = DockStyle.Fill };
    readonly FlowLayoutPanel footer = new() { Dock = DockStyle.Bottom, Height = 48, FlowDirection = FlowDirection.RightToLeft };
    readonly Button next = new() { Text = "Weiter", Width = 120, Height = 34 };
    readonly Button back = new() { Text = "Zurück", Width = 100, Height = 34 };
    readonly TextBox master = new() { UseSystemPasswordChar = true, PlaceholderText = "Mindestens 6 Zeichen" };
    readonly TextBox confirm = new() { UseSystemPasswordChar = true, PlaceholderText = "Passwort erneut eingeben" };
    readonly TextBox nickname = new() { PlaceholderText = "z.B. Privat, Reisen, USD…" };
    readonly bool firstRun;
    int step;
    TaskCompletionSource<List<Account>?>? accountSelection;
    readonly CheckedListBox accountChoices = new() { CheckOnClick = true };
    List<Account> offeredAccounts = [];
    bool closeRequested;
    Panel? strength;
    public string? MasterPassword => firstRun ? master.Text : null;
    public bool DemoRequested { get; private set; }
    public bool RestoreRequested { get; private set; }
    public BankSetupWindow(YaxiSettings settings, bool firstRun = false)
    {
        this.settings = settings; this.firstRun = firstRun;
        Icon=AppArtwork.CreateTrayIcon();Disposed+=(_,_)=>Icon?.Dispose();
        banks.DrawMode=DrawMode.OwnerDrawFixed;banks.ItemHeight=(int)(36*DeviceDpi/96f);
        banks.DrawItem+=(_,e)=>{
            e.DrawBackground();if(e.Index<0 || banks.Items[e.Index] is not BankChoice bank)return;
            int side=(int)(20*DeviceDpi/96f),gap=(int)(8*DeviceDpi/96f);
            using var logo=BankArtwork.LoadLogo(new Account("",bank.Name,"","EUR",0,LogoId:bank.Model["logoId"]?.GetValue<string>()),side);
            var imageRect=new Rectangle(e.Bounds.Left+gap,e.Bounds.Top+(e.Bounds.Height-side)/2,side,side);
            if(logo!=null)e.Graphics.DrawImage(logo,imageRect);
            else {using var pen=new Pen(e.ForeColor);e.Graphics.DrawRectangle(pen,imageRect);}
            TextRenderer.DrawText(e.Graphics,bank.Name,banks.Font,new Rectangle(imageRect.Right+gap,e.Bounds.Top,e.Bounds.Width-side-3*gap,e.Bounds.Height),e.ForeColor,TextFormatFlags.VerticalCenter|TextFormatFlags.EndEllipsis|TextFormatFlags.NoPrefix);e.DrawFocusRectangle();
        };
        Text = "simplebanking"; ClientSize = new Size(460, 520);
        FormBorderStyle = FormBorderStyle.FixedDialog; MaximizeBox = false; MinimizeBox = false;
        StartPosition = FormStartPosition.CenterParent; BackColor = SystemColors.Window;
        Font = new Font("Segoe UI", 13, FontStyle.Regular, GraphicsUnit.Pixel); Padding = new Padding(40, 28, 40, 20);
        footer.Controls.Add(next); footer.Controls.Add(back);
        Controls.Add(page); Controls.Add(footer);
        next.Click += async (_, _) => {
            if (step == 0) RenderStep(1);
            else if (step == 1 && ValidMaster) RenderStep(2);
            else if (step == 2 && banks.SelectedItem != null) RenderStep(3);
            else if (step == 3) await Perform(Connect);
            else if (step == 5) {
                var picked = accountChoices.CheckedIndices.Cast<int>().Select(i => offeredAccounts[i]).ToList();
                if (picked.Count > 0) accountSelection?.TrySetResult(picked);
            } else if (step == 6) {
                if (nickname.Text.Trim().Length > 0 && Accounts.Count == 1) Accounts[0] = Accounts[0] with { Name = nickname.Text.Trim() };
                if (firstRun) RenderStep(7); else { DialogResult = DialogResult.OK; Close(); }
            } else if (step == 7) RenderStep(8);
            else if (step == 8) { DialogResult = DialogResult.OK; Close(); }
        };
        back.Click += (_, _) => { if (step == 2 && !firstRun) Close(); else RenderStep(Math.Max(0, step - 1)); };
        master.TextChanged += (_, _) => UpdateValidation(); confirm.TextChanged += (_, _) => UpdateValidation();
        search.Click += async (_, _) => await Perform(Search);
        query.TextChanged += (_, _) => { banks.DataSource = null; next.Enabled = false; searchDebounce.Stop(); if (query.Text.Trim().Length >= 2) searchDebounce.Start(); };
        searchDebounce.Tick += async (_, _) => { if (busy) return; searchDebounce.Stop(); if (step == 2) await Perform(Search); };
        query.KeyDown += async (_, e) => { if (e.KeyCode == Keys.Enter) { e.SuppressKeyPress = true; await Perform(Search); } };
        banks.SelectedIndexChanged += (_, _) => {
            next.Enabled = banks.SelectedItem != null;
            if (banks.SelectedItem is not BankChoice b) return;
            userLabel.Text = b.Model["userIdLabel"]?.GetValue<string>() ?? "Anmeldename / Leg.-ID";
            pinLabel.Text = b.Model["passwordLabel"]?.GetValue<string>() ?? "PIN / Passwort";
            var model = b.Model["credentials"];
            user.Enabled = model?["full"]?.GetValue<bool>() == true || model?["userId"]?.GetValue<bool>() == true;
            pin.Enabled = model?["full"]?.GetValue<bool>() == true;
            user.Clear(); pin.Clear();
        };
        accountChoices.ItemCheck += (_, e) => {
            if (step == 5) next.Enabled = accountChoices.CheckedItems.Count + (e.NewValue == CheckState.Checked ? 1 : -1) > 0;
        };
        FormClosing += (_, e) => { if (busy) { closeRequested = true; accountSelection?.TrySetResult(null); e.Cancel = true; lifetime.Cancel(); message.Text = "Einrichtung wird abgebrochen…"; } };
        FormClosed += (_, _) => { searchDebounce.Dispose(); lifetime.Dispose(); };
        RenderStep(firstRun ? 0 : 2);
    }
    bool ValidMaster => master.Text.Length >= 6 && master.Text == confirm.Text;
    void ClearPage()
    {
        // Preserve entered values while disposing the controls owned by each page.
        Control[] retained = [query, banks, user, pin, userLabel, pinLabel, message, progress, search, master, confirm, nickname, accountChoices];
        foreach (var control in retained) control.Parent?.Controls.Remove(control);
        foreach (Control control in page.Controls.Cast<Control>().ToArray()) control.Dispose();
    }
    async Task<BankReply> PromptInline(YaxiClient client, string ticket, LoopbackCallback callback, JsonObject result)
    {
        lifetime.Token.ThrowIfCancellationRequested();
        string? url = result["url"]?.GetValue<string>();
        if (result["kind"]?.GetValue<string>() == "redirectHandle") {
            var registered = await client.CallAsync(new JsonObject { ["op"] = "registerRedirect", ["ticket"] = ticket, ["handle"] = result["handle"]!.DeepClone(), ["redirectUri"] = callback.Uri }, lifetime.Token);
            url = registered["url"]!.GetValue<string>();
        }
        Uri? redirect = null;
        if (url != null && (!Uri.TryCreate(url, UriKind.Absolute, out redirect) || redirect.Scheme != "https"))
            throw new InvalidDataException("Bank lieferte keine HTTPS-Weiterleitung.");
        ClearPage(); step = 9; back.Visible = next.Visible = false; AcceptButton = null;
        var panel = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown, WrapContents = false, AutoScroll = true };
        page.Controls.Add(panel);
        void Add(Control control) { control.Width = 374; control.Margin = new Padding(0, 0, 0, 14); panel.Controls.Add(control); }
        var input = result["input"]; var kind = input?["kind"]?.GetValue<string>();
        Add(new Label { Text = kind == "field" ? "Code eingeben" : "Freigabe angefordert…", AutoSize = true,
            Font = new Font("Segoe UI", 19, FontStyle.Bold, GraphicsUnit.Pixel) });
        Add(new Label { Text = result["message"]?.GetValue<string>() ?? (kind == "field" ? "Bitte gib den von der Bank angeforderten Code in das Feld ein" : "Banking-App öffnen und Freigabe bestätigen"), AutoSize = true, MaximumSize = new Size(374, 0) });
        Image? challengeImage = null;
        try {
            if (result["image"]?["data"] is JsonArray bytes && bytes.Count < 8_000_000) {
                using var stream = new MemoryStream(bytes.Select(b => checked((byte)b!.GetValue<int>())).ToArray());
                using var decoded = Image.FromStream(stream); challengeImage = new Bitmap(decoded);
                Add(new PictureBox { Image = challengeImage, SizeMode = PictureBoxSizeMode.Zoom, Height = 150 });
            }
            var field = new TextBox { UseSystemPasswordChar = (input?["secrecy"]?.GetValue<int>() ?? 0) > 0,
                MaxLength = Math.Clamp(input?["maxLength"]?.GetValue<int>() ?? 1024, 1, 10000) };
            var selection = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList };
            if (kind == "field") Add(field);
            if (kind == "selection") {
                foreach (var option in input!["options"]!.AsArray()) selection.Items.Add(option!["label"]!.GetValue<string>() + " " + option["explanation"]?.GetValue<string>());
                Add(selection);
            }
            if (redirect != null) {
                var open = new Button { Text = "Bankfreigabe im Browser öffnen", Height = 36 };
                open.Click += (_, _) => Process.Start(new ProcessStartInfo(redirect.AbsoluteUri) { UseShellExecute = true }); Add(open);
            }
            var completion = new TaskCompletionSource<BankReply>(TaskCreationOptions.RunContinuationsAsynchronously);
            var accept = new Button { Text = redirect != null ? "Bankstatus abfragen" : "Bestätigen", Height = 36 };
            void Validate() => accept.Enabled = kind == "selection" ? selection.SelectedIndex >= 0 : kind != "field" || field.Text.Length >= (input?["minLength"]?.GetValue<int>() ?? 0);
            field.TextChanged += (_, _) => Validate(); selection.SelectedIndexChanged += (_, _) => Validate(); Validate();
            accept.Click += (_, _) => completion.TrySetResult(new(false, kind == "selection" ? input!["options"]![selection.SelectedIndex]!["key"]!.GetValue<string>() : field.Text));
            Add(accept); AcceptButton = accept;
            var cancel = new Button { Text = "Abbrechen", Height = 32 }; cancel.Click += (_, _) => completion.TrySetResult(new(true)); Add(cancel);
            using var registration = lifetime.Token.Register(() => completion.TrySetCanceled(lifetime.Token));
            if (kind == "field") field.Focus();
            return await completion.Task;
        } finally {
            AcceptButton = null; ClearPage(); challengeImage?.Dispose();
            if (!lifetime.IsCancellationRequested) RenderStep(4);
        }
    }
    internal void VerifyNavigation()
    {
        RenderStep(0); next.PerformClick();
        if (step != 0) throw new InvalidOperationException("Hidden next advanced welcome.");
        RenderStep(1); master.Text = "abc"; confirm.Text = "abc";
        if (next.Enabled) throw new InvalidOperationException("Short master password accepted.");
        master.Text = "abcdef"; confirm.Text = "different";
        if (next.Enabled) throw new InvalidOperationException("Mismatched master password accepted.");
        confirm.Text = "abcdef";
        if (!next.Enabled) throw new InvalidOperationException("Matching password rejected.");
        next.PerformClick();
        if (step != 2 || next.Enabled) throw new InvalidOperationException("Bank selection guard failed.");
        back.PerformClick();
        if (step != 1 || master.Text != "abcdef") throw new InvalidOperationException("Back navigation lost form state.");
        master.Clear(); confirm.Clear(); RenderStep(0);
    }
    internal void ShowReviewStep(int value) => RenderStep(value);
    internal void VerifyApproval(Action<Form, string> capture)
    {
        using var client = new YaxiClient(settings.Integration); using var callback = new LoopbackCallback();
        var prompt = PromptInline(client, "", callback, JsonNode.Parse("""{"kind":"challenge","message":"Bitte gib den sechsstelligen Code ein.","input":{"kind":"field","minLength":6,"maxLength":6,"secrecy":1}}""")!.AsObject());
        var panel = (FlowLayoutPanel)page.Controls[0];
        var field = panel.Controls.OfType<TextBox>().Single(); var accept = panel.Controls.OfType<Button>().First();
        if (accept.Enabled) throw new InvalidOperationException("Empty bank code accepted.");
        field.Text = "123"; if (accept.Enabled) throw new InvalidOperationException("Short bank code accepted.");
        field.Text = "123456"; if (!accept.Enabled || !field.UseSystemPasswordChar) throw new InvalidOperationException("Bank code validation or masking failed.");
        capture(this, "bank-approval"); Show(); accept.PerformClick();
        while (!prompt.IsCompleted) Application.DoEvents();
        if (prompt.GetAwaiter().GetResult() != new BankReply(false, "123456")) throw new InvalidOperationException("Bank code reply changed.");
        prompt = PromptInline(client, "", callback, JsonNode.Parse("""{"kind":"challenge","input":{"kind":"selection","options":[{"key":"phone","label":"Banking-App"}]}}""")!.AsObject());
        panel = (FlowLayoutPanel)page.Controls[0]; accept = panel.Controls.OfType<Button>().First();
        if (accept.Enabled) throw new InvalidOperationException("Missing approval selection accepted.");
        panel.Controls.OfType<ComboBox>().Single().SelectedIndex = 0;
        if (!accept.Enabled) throw new InvalidOperationException("Approval selection rejected.");
        accept.PerformClick(); while (!prompt.IsCompleted) Application.DoEvents();
        if (prompt.GetAwaiter().GetResult() != new BankReply(false, "phone")) throw new InvalidOperationException("Approval option key changed.");
        RenderStep(0);
    }
    void UpdateValidation()
    {
        if (step != 1) return;
        strength?.Invalidate();
        next.Enabled = ValidMaster;
        message.Text = confirm.Text.Length == 0 ? "" : master.Text == confirm.Text ? "Passwörter stimmen überein" : "Passwörter stimmen nicht überein";
    }
    void RenderStep(int value)
    {
        ClearPage(); step = value; message.Text = "";
        var stack = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown, WrapContents = false, AutoScroll = true };
        page.Controls.Add(stack);
        void Add(Control control, int height = 0) {
            control.Dock = DockStyle.None; control.Width = 374; control.Margin = new Padding(0, 0, 0, 12);
            if (height > 0) control.Height = height;
            stack.Controls.Add(control);
        }
        void TextLine(string text, int size = 13, bool bold = false) => Add(new Label { Text = text, AutoSize = true, MaximumSize = new Size(374, 0), Font = new Font("Segoe UI", size, bold ? FontStyle.Bold : FontStyle.Regular, GraphicsUnit.Pixel) });
        footer.Visible = value != 0; back.Visible = value > 0; next.Visible = value > 0; next.Enabled = true; next.Text = value == 3 ? "Verbinden" : "Weiter";
        AcceptButton = next; progress.Style = ProgressBarStyle.Blocks;
        if (value == 0) {
            stack.Dispose(); RenderWelcome();
        } else if (value == 1) {
            Add(new SetupSymbol(false) { Size = new Size(40,40) }, 40);
            TextLine("Schütze deine Daten.", 19, true);
            TextLine("Dein Master-Passwort verschlüsselt deine Bank-Zugangsdaten im lokalen Tresor.");
            Add(master, 32);
            strength = new Panel { Height = 3 };
            strength.Paint += (_, e) => {
                int count = master.Text.Length; int filled = count == 0 ? 0 : count < 4 ? 1 : count < 6 ? 2 : count < 8 ? 3 : 4;
                Color color = filled == 1 ? Color.FromArgb(255,59,48) : filled == 2 ? Color.FromArgb(255,149,0) : filled == 3 ? Color.FromArgb(255,204,0) : Color.FromArgb(52,199,89);
                float width = (strength.Width - 18) / 4f;
                for (int i = 0; i < 4; i++) { using var brush = new SolidBrush(i < filled ? color : Color.FromArgb(220,220,224)); e.Graphics.FillRectangle(brush, i * (width + 6), 0, width, 3); }
            };
            Add(strength, 3); Add(confirm, 32); Add(message);
            TextLine("Wir speichern dein Passwort nicht – also gut merken. Bei Verlust müssen die Daten neu eingerichtet werden.");
            UpdateValidation(); master.Focus();
        } else if (value == 2) {
            TextLine("Welche Bank nutzt du?", 19, true); TextLine("Tippe den Namen deiner Bank ein.");
            Add(query, 32); Add(search, 32); Add(banks, 145); Add(message);
            TextLine("Kontoabfragen laufen verschlüsselt über YAXI Open Banking. Zugangsdaten werden lokal verschlüsselt gespeichert.");
            next.Enabled = banks.SelectedItem != null; query.Focus();
        } else if (value == 3) {
            TextLine("Fast geschafft.", 19, true); TextLine("Deine Online-Banking Zugangsdaten");
            if (banks.SelectedItem is BankChoice b) { TextLine(b.Name, 12, true); message.Text = b.Model["advice"]?.GetValue<string>() ?? ""; }
            if (user.Enabled) { Add(userLabel); user.PlaceholderText = "Dein Banking-Benutzername"; Add(user, 32); }
            if (pin.Enabled) { Add(pinLabel); pin.PlaceholderText = "Deine Online-Banking PIN"; Add(pin, 32); }
            TextLine("Zugangsdaten werden verschlüsselt gespeichert (AES-256)"); Add(message);
        } else if (value == 5) {
            TextLine("Konten auswählen", 19, true);
            TextLine("Wähle die Konten, die in simplebanking angezeigt werden sollen");
            Add(accountChoices, 230); back.Visible = false;
            next.Enabled = accountChoices.CheckedItems.Count > 0;
        } else if (value == 6) {
            TextLine("Geschafft.", 19, true); TextLine((Connection?.Name ?? "Bank") + " verbunden", 14, true);
            TextLine("Ab jetzt siehst du deinen Kontostand direkt im Infobereich der Taskleiste.");
            TextLine("Tipp: Doppelklick auf den Kontostand im Flyout, um deine Umsätze zu durchsuchen.");
            TextLine("Kurzname (optional)"); Add(nickname, 32);
            back.Visible = false; next.Text = firstRun ? "Weiter" : "Fertig";
        } else if (value == 7) {
            TextLine("Alles im Überblick.", 19, true);
            TextLine("So holst du das Beste aus simplebanking heraus.");
            TextLine("Ein Klick auf deinen Kontostand", 14, true); TextLine("Alle Umsätze sofort durchsuchbar.");
            TextLine("Automatische Updates", 14, true); TextLine("Immer aktuell, ohne dass du etwas tun musst.");
            TextLine("Rechtsklick für mehr", 14, true); TextLine("Einstellungen, Sperre – alles erreichbar.");
        } else if (value == 8) {
            TextLine("Deine Daten gehören dir.", 19, true);
            TextLine("Bank-Zugangsdaten verschlüsselt", 14, true); TextLine("Login, Passwort und Umsätze werden im lokalen Tresor verschlüsselt gespeichert.");
            TextLine("Kein Cloudkonto", 14, true); TextLine("Es gibt kein simplebanking-Backend. Bankabfragen laufen verschlüsselt über YAXI Open Banking. Alles andere bleibt auf diesem PC.");
            next.Text = "Los geht's!";
        } else {
            TextLine("Sicherheitsfreigabe…", 19, true); TextLine("Bitte folge den Anweisungen deiner Bank");
            progress.Style = ProgressBarStyle.Marquee; Add(progress, 6); Add(message);
            back.Visible = next.Visible = false;
        }
    }
    void RenderWelcome()
    {
        var header = new Panel { Dock = DockStyle.Top, Height = 230 };
        var icon = new SetupSymbol(true) { Size = new Size(64,64), Location = new Point(158,0) }; header.Controls.Add(icon);
        void Label(string text, int y, int height, int size, bool bold, Color color) => header.Controls.Add(new Label {
            Text = text, Bounds = new Rectangle(30,y,320,height), TextAlign = ContentAlignment.MiddleCenter,
            Font = new Font("Segoe UI",size,bold ? FontStyle.Bold : FontStyle.Regular,GraphicsUnit.Pixel), ForeColor = color });
        Label("simplebanking",80,28,22,true,Color.FromArgb(29,29,31));
        Label("Dein Kontostand. Immer sichtbar.",112,22,14,true,Color.FromArgb(72,72,74));
        Label("Keine App öffnen, kein Login – einfach hingucken. Dein Kontostand lebt im Infobereich der Taskleiste.",140,60,13,false,Color.FromArgb(110,110,115));
        var actions = new Panel { Dock = DockStyle.Bottom, Height = 120 };
        var start = new Button { Text = "Jetzt verbinden", Bounds = new Rectangle(0,0,380,36), FlatStyle = FlatStyle.Flat, BackColor = Color.FromArgb(0,122,255), ForeColor = Color.White };
        start.FlatAppearance.BorderSize = 0; start.Click += (_, _) => RenderStep(1);
        var demo = new Button { Text = "Demo-Modus starten", Bounds = new Rectangle(0,44,380,36), FlatStyle = FlatStyle.Flat };
        demo.FlatAppearance.BorderColor = Color.FromArgb(205,205,210); demo.Click += (_, _) => { DemoRequested = true; DialogResult = DialogResult.OK; };
        var restore = new LinkLabel { Text = "Sicherung einspielen …", Bounds = new Rectangle(0,90,380,22), TextAlign = ContentAlignment.MiddleCenter, LinkBehavior = LinkBehavior.HoverUnderline };
        restore.LinkClicked += (_, _) => { RestoreRequested = true; DialogResult = DialogResult.OK; };
        actions.Controls.AddRange([start,demo,restore]); page.Controls.Add(header); page.Controls.Add(actions);
    }
    async Task Perform(Func<Task> action)
    {
        if (busy || lifetime.IsCancellationRequested) return;
        busy = true; search.Enabled = connect.Enabled = next.Enabled = back.Enabled = false;
        try { await action(); }
        catch (OperationCanceledException) { if (!closeRequested && step is 4 or 9) RenderStep(3); message.Text = "Einrichtung abgebrochen. Keine Kontodaten gespeichert."; }
        catch (Exception e) { if (step is 4 or 9) RenderStep(3); message.Text = e is YaxiException or InvalidOperationException ? e.Message : "Bankaufruf fehlgeschlagen: " + e.GetType().Name; }
        finally { busy = false; search.Enabled = connect.Enabled = !lifetime.IsCancellationRequested; back.Enabled = !lifetime.IsCancellationRequested; next.Enabled = !lifetime.IsCancellationRequested && (step != 2 || banks.SelectedItem != null); }
        if (closeRequested) { Close(); return; }
        if (Connection != null && Accounts.Count > 0) RenderStep(6);
    }
    async Task Search()
    {
        if (query.Text.Trim().Length < 2) { message.Text = "Mindestens zwei Zeichen eingeben."; return; }
        var term = query.Text.Trim();
        stepTitle.Text = "Bank wird gesucht …"; stepSubtitle.Text = "Suche nach deiner Bank oder IBAN"; progress.Value = 1; message.Text = "Banksuche läuft …";
        using var tickets = new TicketClient(settings); using var client = new YaxiClient(settings.Integration);
        var ticket = await tickets.IssueAsync("Accounts", null, lifetime.Token);
        var result = await client.CallAsync(new JsonObject { ["op"] = "search", ["ticket"] = ticket, ["term"] = term }, lifetime.Token);
        if (query.Text.Trim() != term) return;
        banks.DataSource = result["items"]!.AsArray().Select(n => new BankChoice(n!["name"]!.GetValue<string>(), n.AsObject())).ToList();
        stepTitle.Text = "Bank auswählen"; stepSubtitle.Text = "Wähle deine Bank aus der Liste"; progress.Value = 2; message.Text = $"{banks.Items.Count} Banken gefunden.";
    }
    async Task Connect()
    {
        if (banks.SelectedItem is not BankChoice b) return;
        RenderStep(4); stepTitle.Text = "Zugangsdaten"; stepSubtitle.Text = "Gib deine Bankdaten ein und bestätige die Sicherheitsfreigabe"; progress.Value = 3; message.Text = "Konten werden angefordert …";
        using var tickets = new TicketClient(settings); using var client = new YaxiClient(settings.Integration);
        var ticket = await tickets.IssueAsync("Accounts", null, lifetime.Token);
        var credentials = new JsonObject { ["connectionId"] = b.Model["id"]!.DeepClone(), ["userId"] = user.Enabled ? user.Text : null, ["password"] = pin.Enabled ? pin.Text : null };
        using var callback = new LoopbackCallback();
        var result = await new BankFlow(client).RunAsync(new JsonObject { ["op"] = "accounts", ["ticket"] = ticket, ["credentials"] = credentials }, "Accounts",
            r => PromptInline(client, ticket, callback, r), lifetime.Token);
        lifetime.Token.ThrowIfCancellationRequested();
        var payload = TicketClient.DecodeJwt(result["jwt"]!.GetValue<string>())["data"]?["data"];
        var array = payload as JsonArray ?? payload?["accounts"] as JsonArray ?? throw new InvalidDataException("Kontenliste fehlt.");
        var localId = Guid.NewGuid().ToString("N");
        var options = array.Select(n => {
            var iban = n?["iban"]?.GetValue<string>() ?? ""; var currency = n?["currency"]?.GetValue<string>();
            if (string.IsNullOrEmpty(iban) || string.IsNullOrEmpty(currency)) return null;
            return new Account(Guid.NewGuid().ToString("N"), n?["displayName"]?.GetValue<string>() ?? n?["name"]?.GetValue<string>() ?? b.Name,
                BankingRules.NormalizeIban(iban), currency, 0, localId, false, b.Model["logoId"]?.GetValue<string>(), BankName: b.Name,OwnerName:n?["ownerName"]?.GetValue<string>());
        }).Where(a => a != null).Cast<Account>().ToList();
        if (options.Count == 0) throw new InvalidDataException("Keine Konten mit IBAN und Währung erhalten.");
        offeredAccounts = options; accountChoices.Items.Clear();
        foreach (var account in options) accountChoices.Items.Add(account.Name + " · " + account.Iban, options.Count == 1);
        accountSelection = new(TaskCreationOptions.RunContinuationsAsynchronously);
        RenderStep(5); next.Enabled = accountChoices.CheckedItems.Count > 0;
        var selectedAccounts = await accountSelection.Task;
        lifetime.Token.ThrowIfCancellationRequested();
        if (selectedAccounts == null) { RenderStep(3); return; }
        Accounts = selectedAccounts;
        Connection = new(localId, b.Model["id"]!.GetValue<string>(), b.Name, user.Text, pin.Text, result["connectionData"]?.Deserialize<int[]>());
        pin.Clear();
    }
}

internal sealed class LoopbackCallback : IDisposable
{
    readonly TcpListener listener = new(IPAddress.Loopback, 0);
    readonly CancellationTokenSource stopped = new();
    readonly string path = "/yaxi/" + Guid.NewGuid().ToString("N");
    public string Uri { get; }
    public LoopbackCallback()
    {
        listener.Start(); Uri = $"http://127.0.0.1:{((IPEndPoint)listener.LocalEndpoint).Port}{path}"; _ = Serve();
    }
    async Task Serve()
    {
        try {
            while (!stopped.IsCancellationRequested) {
                using var client = await listener.AcceptTcpClientAsync(stopped.Token);
                using var deadline = CancellationTokenSource.CreateLinkedTokenSource(stopped.Token); deadline.CancelAfter(TimeSpan.FromSeconds(3));
                using var stream = client.GetStream(); using var reader = new StreamReader(stream, Encoding.ASCII, false, 1024, true);
                var line = await reader.ReadLineAsync(deadline.Token);
                bool valid = line != null && line.Length < 4096 && line.StartsWith("GET " + path + " ", StringComparison.Ordinal);
                // A callback is merely a return signal. Only the SDK can report banking success.
                var body = valid ? "Bitte zu simplebanking zurueckkehren und den Bankstatus abfragen." : "Not found";
                var bytes = Encoding.UTF8.GetBytes($"HTTP/1.1 {(valid ? "200 OK" : "404 Not Found")}\r\nContent-Type: text/plain; charset=utf-8\r\nContent-Length: {Encoding.UTF8.GetByteCount(body)}\r\nConnection: close\r\n\r\n{body}");
                await stream.WriteAsync(bytes, deadline.Token);
            }
        } catch (Exception e) when (e is OperationCanceledException or SocketException or IOException) { }
    }
    public void Dispose() { stopped.Cancel(); listener.Stop(); }
}

internal static class BankDialogs
{
    public static async Task<BankReply> Prompt(IWin32Window owner, YaxiClient client, string ticket, LoopbackCallback callback, JsonObject result, CancellationToken cancellation)
    {
        cancellation.ThrowIfCancellationRequested();
        var kind = result["kind"]!.GetValue<string>();
        string? url = result["url"]?.GetValue<string>();
        if (kind == "redirectHandle")
        {
            var registered = await client.CallAsync(new JsonObject { ["op"] = "registerRedirect", ["ticket"] = ticket, ["handle"] = result["handle"]!.DeepClone(), ["redirectUri"] = callback.Uri }, cancellation);
            url = registered["url"]!.GetValue<string>();
        }
        using var form = new Form { Text = "Bankfreigabe", Width = 630, Height = 560, StartPosition = FormStartPosition.CenterParent };
        var panel = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown, WrapContents = false, AutoScroll = true, Padding = new Padding(20) };
        panel.Controls.Add(new Label { Text = result["message"]?.GetValue<string>() ?? (url != null ? "Freigabe im Browser durchführen. Anschließend Bankstatus abfragen." : "Bitte die Anforderung Ihrer Bank prüfen und bestätigen."), AutoSize = true, MaximumSize = new Size(550, 0) });
        MemoryStream? imageStream = null; Image? image = null;
        try {
            if (result["image"]?["data"] is JsonArray bytes && bytes.Count < 8_000_000) {
                imageStream = new(bytes.Select(b => checked((byte)b!.GetValue<int>())).ToArray()); image = Image.FromStream(imageStream);
                panel.Controls.Add(new PictureBox { Image = image, SizeMode = PictureBoxSizeMode.Zoom, Width = 480, Height = 180 });
            }
            var input = result["input"]; var inputKind = input?["kind"]?.GetValue<string>();
            var field = new TextBox { Width = 520, UseSystemPasswordChar = (input?["secrecy"]?.GetValue<int>() ?? 0) > 0, MaxLength = Math.Min(input?["maxLength"]?.GetValue<int>() ?? 1024, 10000) };
            var selection = new ComboBox { Width = 520, DropDownStyle = ComboBoxStyle.DropDownList };
            if (inputKind == "field") panel.Controls.Add(field);
            if (inputKind == "selection") { foreach (var option in input!["options"]!.AsArray()) selection.Items.Add(option!["label"]!.GetValue<string>() + " " + option["explanation"]?.GetValue<string>()); panel.Controls.Add(selection); }
            if (url != null) {
                if (!System.Uri.TryCreate(url, UriKind.Absolute, out var uri) || uri.Scheme != "https") throw new InvalidDataException("Bank lieferte keine HTTPS-Weiterleitung.");
                var open = new Button { Text = "Bankfreigabe im Browser öffnen", AutoSize = true };
                open.Click += (_, _) => Process.Start(new ProcessStartInfo(uri.AbsoluteUri) { UseShellExecute = true }); panel.Controls.Add(open);
            }
            var accept = new Button { Text = url != null ? "Bankstatus abfragen" : "Bestätigen", AutoSize = true };
            var cancel = new Button { Text = "Abbrechen", AutoSize = true, DialogResult = DialogResult.Cancel };
            accept.Click += (_, _) => {
                if (inputKind == "selection" && selection.SelectedIndex < 0) return;
                if (inputKind == "field" && field.Text.Length < (input?["minLength"]?.GetValue<int>() ?? 0)) return;
                form.DialogResult = DialogResult.OK;
            };
            panel.Controls.Add(accept); panel.Controls.Add(cancel); form.Controls.Add(panel); form.CancelButton = cancel;
            if (form.ShowDialog(owner) != DialogResult.OK) return new(true);
            return new(false, inputKind == "selection" ? input!["options"]![selection.SelectedIndex]!["key"]!.GetValue<string>() : field.Text);
        } finally { image?.Dispose(); imageStream?.Dispose(); }
    }
}
