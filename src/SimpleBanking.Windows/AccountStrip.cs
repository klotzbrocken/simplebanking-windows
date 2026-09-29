namespace SimpleBanking;

// KontoKachel.swift: active 30/20 px, inactive 26/16 px, spacing 10 px.
internal sealed class AccountStrip : FlowLayoutPanel
{
    readonly ToolTip hints = new();
    public event Action<int>? AccountSelected;
    public event Action? AllSelected;
    public AccountStrip() { Height = 38; WrapContents = false; AutoScroll = true; BackColor = Color.Transparent; }
    internal void UpdateAccounts(IReadOnlyList<Account> accounts, int index, bool unified, BalancePresentation presentation,bool logos=true)
    {
        SuspendLayout();
        foreach (Control control in Controls.Cast<Control>().ToArray()) control.Dispose();
        for (int i = 0; i < accounts.Count; i++) {
            int captured = i; bool active = !unified && i == index;
            var text = accounts[i].Name.Trim();
            var tile = new AccountTile { Active = active, Accent = presentation.Ink, Logo = logos?BankArtwork.LoadLogo(accounts[i], active ? 20 : 16):null, Text = text.Length == 0 ? "?" : text[..1].ToUpperInvariant(), Width = active ? 30 : 26, Height = active ? 30 : 26, AutoSize = false, FlatStyle = FlatStyle.Flat, Margin = new Padding(0,0,10,0), ForeColor = active ? presentation.Ink : presentation.Detail, BackColor = Color.Transparent, AccessibleName = accounts[i].Name, Font = new Font("Segoe UI", active ? 14 : 11, FontStyle.Bold, GraphicsUnit.Pixel) };
            tile.FlatAppearance.BorderSize = 0; tile.Click += (_, _) => AccountSelected?.Invoke(captured); hints.SetToolTip(tile, accounts[i].Name); Controls.Add(tile);
        }
        if (accounts.Count > 1) {
            var all = new HeaderIcon("all", "Alle Konten", () => AllSelected?.Invoke(), () => unified) { Width = 26, Height = 26, FlatStyle = FlatStyle.Flat, Margin = Padding.Empty, ForeColor = unified ? presentation.Ink : presentation.Detail, BackColor = Color.Transparent, AccessibleName = "Alle Konten" };
            all.FlatAppearance.BorderSize = 0; hints.SetToolTip(all, "Alle Konten"); Controls.Add(all);
        }
        ResumeLayout();
    }
    protected override void Dispose(bool disposing) { if (disposing) hints.Dispose(); base.Dispose(disposing); }
}
