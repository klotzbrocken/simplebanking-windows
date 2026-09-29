using System.Drawing.Drawing2D;
namespace SimpleBanking;

// Geometry and light-mode colors: BalanceBar.swift defaultThemeCard / BalanceWash.
internal sealed class BalanceFlyout : Form
{
    readonly MainWindow owner;
    readonly BalanceCard card;
    readonly AccountStrip slots = new();
    readonly HeaderIcon send;
    readonly HeaderIcon pin;
    readonly HeaderIcon closeWidget;
    readonly System.Windows.Forms.Timer hoverTimer=new(){Interval=80};
    internal void SetWidgetHover(bool inside){card.SetPrivacyHidden(IsPinned&&!inside);}
    internal void CloseWidget(){if(quickSend?.Busy==true)return;SetPinned(false);Hide();}

    QuickSendPanel? quickSend;
    Point? dragStart;
    Point dragLocation;
    bool dragging;
    internal bool IsPinned {get;private set;}
    public BalanceFlyout(MainWindow owner)
    {
        this.owner = owner; FormBorderStyle = FormBorderStyle.None; ShowInTaskbar = false;
        StartPosition = FormStartPosition.Manual; TopMost = true; DoubleBuffered = true; KeyPreview = true;
        ClientSize = new Size(348, 178); Controls.Add(slots);
        card=new BalanceCard(owner,true) { Dock=DockStyle.Top,Height=140 };
        Controls.Add(card);
        send=new HeaderIcon("send","Geld senden",ToggleQuickSend);pin=new HeaderIcon("pin","Im Vordergrund halten",()=>SetPinned(!IsPinned),()=>IsPinned);Controls.Add(send);Controls.Add(pin);pin.BringToFront();
        closeWidget=new HeaderIcon("close","Widget schließen",CloseWidget){Visible=false};Controls.Add(closeWidget);closeWidget.BringToFront();
        hoverTimer.Tick+=(_,_)=>{if(Visible&&IsPinned)SetWidgetHover(RectangleToScreen(ClientRectangle).Contains(Cursor.Position));};
        VisibleChanged+=(_,_)=>{if(Visible){SetWidgetHover(RectangleToScreen(ClientRectangle).Contains(Cursor.Position));hoverTimer.Start();}else hoverTimer.Stop();};
        card.DoubleClick+=(_,_)=>{if(!IsPinned)Hide();owner.OpenMainWindow();};
        card.MouseDown+=(_,e)=>{if(e.Button!=MouseButtons.Left)return;dragStart=Cursor.Position;dragLocation=Location;dragging=false;card.Capture=true;};
        card.MouseMove+=(_,e)=>{if(dragStart is not {} start||e.Button!=MouseButtons.Left)return;var delta=new Size(Cursor.Position.X-start.X,Cursor.Position.Y-start.Y);if(!dragging&&Math.Abs(delta.Width)<SystemInformation.DragSize.Width&&Math.Abs(delta.Height)<SystemInformation.DragSize.Height)return;dragging=true;SetPinned(true,false);Location=dragLocation+delta;};
        card.MouseUp+=(_,_)=>{dragStart=null;card.Capture=false;if(dragging){dragging=false;SaveWidgetPosition();}};
        slots.SetBounds(12, 136, 324, 38);
        slots.AccountSelected += owner.SelectFlyoutAccount; slots.AllSelected += owner.SelectAllAccounts;
        Deactivate += (_, _) => {if(!IsPinned&&!dragging&&quickSend?.Busy!=true)Hide();};
        MouseDoubleClick += (_, _) => { if(!IsPinned)Hide(); owner.OpenMainWindow(); };
        KeyDown += (_, e) => { if (e.KeyCode == Keys.Escape&&!IsPinned) Hide(); };
        var menu = new ContextMenuStrip();
        var unlockItem = menu.Items.Add("Entsperren", null, (_, _) => { if(!IsPinned)Hide(); owner.RequestUnlock(); });
        menu.Opening += (_, _) => { unlockItem.Visible = owner.IsLocked; unlockItem.Text = owner.UnlockActionText; };
        menu.Items.Add("Umsätze", null, (_, _) => owner.OpenMainWindow());
        menu.Items.Add("Dashboard",null,(_,_)=>{if(!IsPinned)Hide();owner.OpenDashboard();});
        menu.Items.Add("Abos & Verträge",null,(_,_)=>{if(!IsPinned)Hide();owner.OpenDashboard(1);});
        menu.Items.Add("Aktualisieren", null, (_, _) => owner.RefreshFromFlyout());
        menu.Items.Add("Einstellungen", null, (_, _) => owner.OpenSettings());
        menu.Items.Add("Als Desktop-Widget behalten",null,(_,_)=>SetPinned(true));
        menu.Items.Add("Widget schließen",null,(_,_)=>CloseWidget());
        ContextMenuStrip = menu;
        owner.BalanceChanged += RefreshCards;
    }
    protected override void OnFormClosing(FormClosingEventArgs e){
        if(e.CloseReason==CloseReason.UserClosing&&!owner.IsDisposed){e.Cancel=true;CloseWidget();}
        base.OnFormClosing(e);
    }
    protected override void Dispose(bool disposing)
    {
        if (disposing) { hoverTimer.Dispose();owner.BalanceChanged -= RefreshCards;ContextMenuStrip?.Dispose(); }
        base.Dispose(disposing);
    }
    internal void ToggleQuickSend(){if(owner.QuickSendAccountId==null||quickSend?.Busy==true)return;if(quickSend!=null){quickSend.Dispose();quickSend=null;}else{quickSend=new QuickSendPanel(owner,async draft=>{bool top=TopMost;TopMost=false;try{return await owner.ReviewQuickSend(draft);}finally{if(!IsDisposed)TopMost=top;}});quickSend.LayoutRequested+=RefreshCards;Controls.Add(quickSend);}RefreshCards();}
    internal void ShowAtTray()
    {
        if(IsPinned){RefreshCards();Show();Activate();return;}
        RefreshCards(); var screen = Screen.FromPoint(Cursor.Position); var area = screen.WorkingArea;
        int x = Cursor.Position.X - Width/2, y = area.Bottom-Height-8;
        if(area.Top > screen.Bounds.Top) y = area.Top+8;
        else if(area.Left > screen.Bounds.Left) { x=area.Left+8;y=Cursor.Position.Y-Height/2; }
        else if(area.Right < screen.Bounds.Right) { x=area.Right-Width-8;y=Cursor.Position.Y-Height/2; }
        Location = new Point(Math.Clamp(x,area.Left,Math.Max(area.Left,area.Right-Width)),Math.Clamp(y,area.Top,Math.Max(area.Top,area.Bottom-Height)));
        Show(); Activate(); if(owner.Preferences.RippleAlwaysOn)card.StartRipple();
    }
    internal void SetPinned(bool value,bool save=true){IsPinned=value;TopMost=true;owner.Preferences.WidgetEnabled=value;pin.Invalidate();closeWidget.Visible=value;SetWidgetHover(RectangleToScreen(ClientRectangle).Contains(Cursor.Position));RefreshCards();if(save)SaveWidgetPosition();}
    void SaveWidgetPosition(){var area=Screen.FromPoint(Location).WorkingArea;Location=new Point(Math.Clamp(Left,area.Left,Math.Max(area.Left,area.Right-Width)),Math.Clamp(Top,area.Top,Math.Max(area.Top,area.Bottom-Height)));owner.Preferences.WidgetX=Left;owner.Preferences.WidgetY=Top;owner.Preferences.Save();}
    internal void RestoreWidget(){if(!owner.Preferences.WidgetEnabled)return;RefreshCards();Location=new Point(owner.Preferences.WidgetX,owner.Preferences.WidgetY);SetPinned(true);Show();}
    protected override void OnSizeChanged(EventArgs e) {
        base.OnSizeChanged(e); if(Width<24 || Height<24)return;
        if(owner?.Theme.Border is not null){var previous=Region;Region=null;previous?.Dispose();return;}
        using var path=new GraphicsPath();const int d=24;
        path.AddArc(0,0,d,d,180,90);path.AddArc(Width-d,0,d,d,270,90);path.AddArc(Width-d,Height-d,d,d,0,90);path.AddArc(0,Height-d,d,d,90,90);path.CloseFigure();
        var old=Region;Region=new Region(path);old?.Dispose();
    }
    protected override void OnPaint(PaintEventArgs e){base.OnPaint(e);if(owner.Theme.Border is {} color){using var pen=new Pen(color,3);e.Graphics.DrawRectangle(pen,1,1,Width-3,Height-3);}}
    internal void RefreshCards()
    {
        OnSizeChanged(EventArgs.Empty);Padding=owner.Theme.Border.HasValue?new Padding(3):Padding.Empty;
        var accounts=owner.FlyoutAccounts();BackColor=owner.Theme.Surface;send.Tint=pin.Tint=owner.Theme.Secondary;send.TextMode=pin.TextMode=owner.Theme.Lofi;
        slots.UpdateAccounts(accounts,owner.SelectedAccountIndex,owner.AllAccountsSelected,BalancePresentation.Create(owner.CurrentBalance,owner.Preferences),owner.Theme.BankLogos);
        float scale=DeviceDpi/96f;
        if(owner.QuickSendAccountId==null&&quickSend!=null){quickSend.Dispose();quickSend=null;}
        int oldBottom=Bottom;Height=(int)((178+(quickSend?.PreferredHeight??0))*scale);if(Visible&&!IsPinned)Top=Math.Max(Screen.FromPoint(Location).WorkingArea.Top,oldBottom-Height);if(quickSend!=null){quickSend.SetBounds(0,(int)(178*scale),ClientSize.Width,(int)(quickSend.PreferredHeight*scale));quickSend.RefreshAccount();}slots.Visible=accounts.Count>1;
        slots.SetBounds((int)(12*scale),(int)(140*scale),ClientSize.Width-(int)((IsPinned?126:96)*scale),(int)(38*scale));
        send.SetBounds(ClientSize.Width-(int)(40*scale),(int)(143*scale),(int)(28*scale),(int)(28*scale));send.Enabled=!owner.IsLocked;pin.SetBounds(ClientSize.Width-(int)(74*scale),(int)(143*scale),(int)(28*scale),(int)(28*scale));
        closeWidget.Tint=owner.Theme.Secondary;closeWidget.TextMode=owner.Theme.Lofi;
        closeWidget.SetBounds(ClientSize.Width-(int)(106*scale),(int)(143*scale),(int)(28*scale),(int)(28*scale));closeWidget.Visible=IsPinned;closeWidget.Enabled=quickSend?.Busy!=true;
        card.Height=(int)(140*scale);card.RefreshCard();
    }
}
internal sealed partial class MainWindow{internal void TransferFromFlyout()=>Transfer();}
