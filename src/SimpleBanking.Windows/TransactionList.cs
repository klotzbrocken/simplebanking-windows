namespace SimpleBanking;

internal sealed class TransactionList : UserControl
{
    readonly Panel rows = new() { Dock = DockStyle.Fill, AutoScroll = false };
    readonly VScrollBar scrollbar=new(){Width=12,Visible=false,TabStop=false};
    readonly System.Windows.Forms.Timer scrollIdle=new(){Interval=1100};
    int scrollOffset,contentHeight;
    readonly List<(int Top, int Height, Entry? Entry, string? Title)> slots = new();
    readonly Dictionary<int, Control> realized = new();
    public event Action<Entry>? OpenEntry;
    public event Action<Entry>? RenameMerchant;
    public event Action<Entry>? CreateMerchantRule;
    public event Action<Entry>? ResetMerchant;
    public event Action<Entry>? ToggleRead;
    public event Action<Entry?>? SelectionChanged;
    string? selectedKey;
    Entry[] source=[];
    bool hidden, categories, pending, rendering;
    ThemePalette theme=ThemePalette.For(new());
    static string Key(Entry entry) => entry.AccountId.Length+":"+entry.AccountId+entry.Id;
    internal int RealizedCount => realized.Count;
    internal int TotalEntries => source.Length;
    internal int ScrollOffset => scrollOffset;
    internal bool ScrollbarShown=>scrollbar.Visible;
    internal void ScrollTo(int offset) {
        int next=Math.Clamp(offset,0,Math.Max(0,contentHeight-rows.ClientSize.Height));
        if(next==scrollOffset)return;scrollOffset=next;RenderViewport();
        scrollbar.Visible=contentHeight>rows.ClientSize.Height;scrollbar.BringToFront();scrollIdle.Stop();scrollIdle.Start();
    }
    internal void SelectEntry(Entry entry) {
        var key=Key(entry);selectedKey=selectedKey==key?null:key;
        foreach(var pair in realized) if(pair.Value is BookingRow row) row.SetSelected(Key(slots[pair.Key].Entry!)==selectedKey);
        SelectionChanged?.Invoke(selectedKey==null?null:entry);
    }
    public TransactionList() {
        SetStyle(ControlStyles.SupportsTransparentBackColor,true);BackColor=rows.BackColor=Color.Transparent;
        Controls.Add(rows);Controls.Add(scrollbar);scrollbar.BringToFront();
        rows.SizeChanged+=(_,_)=>{scrollOffset=Math.Min(scrollOffset,Math.Max(0,contentHeight-rows.ClientSize.Height));RenderViewport();};
        scrollbar.ValueChanged+=(_,_)=>{if(!rendering)ScrollTo(scrollbar.Value);};
        scrollIdle.Tick+=(_,_)=>{if(scrollbar.Capture)return;scrollIdle.Stop();scrollbar.Visible=false;};
        Disposed+=(_,_)=>scrollIdle.Dispose();
        rows.MouseWheel+=(_,e)=>ScrollTo(scrollOffset-(int)(e.Delta/120d*(SystemInformation.MouseWheelScrollLines<0?rows.Height:Math.Max(1,SystemInformation.MouseWheelScrollLines)*48)));
        rows.TabStop=false;
    }
    internal static string DateTitle(DateOnly date, DateOnly today) => date == today ? "Heute" : date == today.AddDays(-1) ? "Gestern" : date == today.AddDays(-2) ? "Vorgestern" : date.ToString("dd.MM.yyyy");
    public void Populate(IEnumerable<Entry> entries,bool hidden,bool categories,bool pendingAsPill=true,ThemePalette? theme=null) {
        var next=entries.OrderByDescending(e=>e.Date).ToArray();
        bool same=source.Select(Key).SequenceEqual(next.Select(Key));int offset=same?ScrollOffset:0;
        this.hidden=hidden;this.categories=categories;pending=pendingAsPill;this.theme=theme??ThemePalette.For(new());source=next;
        rendering=true;
        try {
            foreach(var control in realized.Values)control.Dispose();realized.Clear();slots.Clear();
            int top=0;DateOnly? last=null;var today=DateOnly.FromDateTime(DateTime.Today);
            foreach(var entry in source) {
                if(last!=entry.Date){slots.Add((top,34,null,DateTitle(entry.Date,today)));top+=34;last=entry.Date;}
                int height=categories||(entry.Pending&&pending)?62:48;slots.Add((top,height,entry,null));top+=height;
            }
            if(source.Length==0)slots.Add((0,160,null,"Keine Umsätze"));
            contentHeight=top;scrollOffset=Math.Clamp(offset,0,Math.Max(0,top-rows.ClientSize.Height));scrollbar.Visible=false;
            if(selectedKey!=null&&!source.Any(e=>Key(e)==selectedKey)){selectedKey=null;SelectionChanged?.Invoke(null);}
        } finally {rendering=false;}
        RenderViewport();
    }
    void RenderViewport() {
        if(rendering||IsDisposed)return;rendering=true;
        try {
            scrollbar.SetBounds(Math.Max(0,Width-12),0,12,Height);
            scrollbar.LargeChange=Math.Max(1,rows.ClientSize.Height);scrollbar.SmallChange=48;scrollbar.Maximum=Math.Max(0,contentHeight-1);scrollbar.Value=Math.Min(scrollOffset,Math.Max(0,scrollbar.Maximum-scrollbar.LargeChange+1));
            int offset=ScrollOffset,minimum=Math.Max(0,offset-100),maximum=offset+rows.ClientSize.Height+100;
            int low=0,high=slots.Count;
            while(low<high){int mid=(low+high)/2;if(slots[mid].Top+slots[mid].Height<minimum)low=mid+1;else high=mid;}
            int end=low;while(end<slots.Count&&slots[end].Top<=maximum)end++;
            rows.SuspendLayout();
            foreach(var index in realized.Keys.Where(i=>i<low||i>=end).ToArray()){realized[index].Dispose();realized.Remove(index);}
            for(int i=low;i<end;i++) {
                var slot=slots[i];
                if(!realized.TryGetValue(i,out var control)) {
                    control=slot.Entry is {} entry?CreateRow(entry,i):slot.Title=="Keine Umsätze"?new EmptyBookings{TabStop=false}:new DateHeading(slot.Title!,theme){TabStop=false};
                    realized.Add(i,control);rows.Controls.Add(control);
                }
                control.Bounds=new Rectangle(0,slot.Top-offset,Math.Max(100,rows.ClientSize.Width-12),slot.Height);
            }
            rows.ResumeLayout(false);
        } finally {rendering=false;}
    }
    BookingRow CreateRow(Entry entry,int index) {
        var row=new BookingRow(entry,hidden,categories,pending,theme);row.SetSelected(Key(entry)==selectedKey);
        row.Click+=(_,_)=>SelectEntry(entry);row.DoubleClick+=(_,_)=>OpenEntry?.Invoke(entry);
        row.PreviewKeyDown+=(_,e)=>{if(e.KeyCode is Keys.Up or Keys.Down or Keys.Home or Keys.End)e.IsInputKey=true;};
        row.KeyDown+=(_,e)=>{
            if(e.KeyCode==Keys.Enter)OpenEntry?.Invoke(entry);
            else if(e.KeyCode==Keys.Space)SelectEntry(entry);
            else if(e.KeyCode is Keys.Up or Keys.Down or Keys.Home or Keys.End) {
                int direction=e.KeyCode is Keys.Up or Keys.End?-1:1;
                int target=e.KeyCode==Keys.Home?0:e.KeyCode==Keys.End?slots.Count-1:index+direction;
                while(target>=0&&target<slots.Count&&slots[target].Entry==null)target+=direction;
                if(target>=0&&target<slots.Count) {
                    var slot=slots[target];int offset=ScrollOffset;
                    if(slot.Top<offset)ScrollTo(slot.Top);else if(slot.Top+slot.Height>offset+rows.ClientSize.Height)ScrollTo(slot.Top+slot.Height-rows.ClientSize.Height);
                    if(realized.TryGetValue(target,out var control))control.Focus();
                }
            } else return;
            e.Handled=true;e.SuppressKeyPress=true;
        };
        var menu=new ContextMenuStrip();menu.Items.Add("Buchungsdetails",null,(_,_)=>OpenEntry?.Invoke(entry));
        menu.Items.Add("Händlername ändern …",null,(_,_)=>RenameMerchant?.Invoke(entry));
        menu.Items.Add("Als Händlerregel speichern …",null,(_,_)=>CreateMerchantRule?.Invoke(entry));
        if(entry.MerchantOverride!=null)menu.Items.Add("Händlername zurücksetzen",null,(_,_)=>ResetMerchant?.Invoke(entry));
        menu.Items.Add(entry.Read?"Als ungelesen markieren":"Als gelesen markieren",null,(_,_)=>ToggleRead?.Invoke(entry));
        row.ContextMenuStrip=menu;row.Disposed+=(_,_)=>menu.Dispose();return row;
    }
}
internal sealed class DateHeading : Control
{
    readonly string title;readonly ThemePalette theme;
    internal DateHeading(string title,ThemePalette theme){this.title=title;this.theme=theme;SetStyle(ControlStyles.SupportsTransparentBackColor,true);BackColor=Color.Transparent;}
    protected override Size DefaultSize => new(320,34);
    protected override void OnPaint(PaintEventArgs e) {
        using var font = theme.Font(12,true);
        var size = TextRenderer.MeasureText(title,font);
        TextRenderer.DrawText(e.Graphics,theme.Text(title),font,new Point(16,10),theme.Secondary,TextFormatFlags.NoPrefix);
        using var pen = new Pen(ThemePalette.Blend(theme.Ink,theme.Surface,.15));
        e.Graphics.DrawLine(pen,Math.Min(Width-16,16+size.Width+10),20,Width-16,20);
    }
}
