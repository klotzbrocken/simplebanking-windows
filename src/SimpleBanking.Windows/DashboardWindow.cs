using System.Globalization;
using System.Drawing.Drawing2D;
namespace SimpleBanking;
internal sealed record AnalysisSnapshot(string Name,Account[] Accounts,Entry[] Entries,decimal? Balance) {
    internal string Currency=>Accounts.FirstOrDefault()?.Currency??"EUR";
    internal bool Mixed=>Accounts.Select(a=>a.Currency).Distinct().Count()>1;
}
internal sealed class DashboardWindow : Form
{
    readonly MainWindow owner;
    readonly FlowLayoutPanel tabs=new(){Dock=DockStyle.Top,Height=50,Padding=new Padding(16,10,16,0),WrapContents=false};
    readonly Panel page=new(){Dock=DockStyle.Fill,Padding=new Padding(20)};
    AnalysisSnapshot snapshot;
    readonly List<Button> tabButtons=[];
    int active,period=2;
    string bucket="Abos",mode="Liste";
    DateOnly month=new(DateTime.Today.Year,DateTime.Today.Month,1);
    internal DashboardWindow(MainWindow owner,int tab=0) {
        this.owner=owner;snapshot=owner.AnalysisSnapshot();Text="simplebanking · Dashboard";ClientSize=new Size(840,620);MinimumSize=new Size(760,600);StartPosition=FormStartPosition.CenterParent;
        AutoScaleMode=AutoScaleMode.Dpi;AutoScaleDimensions=new SizeF(96,96);Font=new Font("Segoe UI",13,GraphicsUnit.Pixel);BackColor=Color.FromArgb(248,250,249);
        Icon=AppArtwork.CreateTrayIcon();Disposed+=(_,_)=>Icon?.Dispose();
        foreach(var (name,index) in new[]{("Übersicht",0),("Abos & Verträge",1),("Kalender",2),("Geld-Alter",3),("Regeln",4)}) {
            int i=index;var button=ActionButton(name,()=>ShowTab(i));tabs.Controls.Add(button);tabButtons.Add(button);
        }
        Controls.Add(page);Controls.Add(tabs);owner.BalanceChanged+=Reload;FormClosed+=(_,_)=>owner.BalanceChanged-=Reload;ShowTab(tab);
    }
    void Reload(){if(owner.IsLocked){Close();return;}snapshot=owner.AnalysisSnapshot();ShowTab(active);}
    internal void ShowTab(int index) {
        active=index;page.SuspendLayout();foreach(Control c in page.Controls.Cast<Control>().ToArray())c.Dispose();
        foreach(var (b,i) in tabButtons.Select((b,i)=>(b,i)))b.BackColor=i==index?Color.White:BackColor;
        Text="simplebanking · Dashboard · "+snapshot.Name;
        if(snapshot.Mixed){page.Controls.Add(Label("Die Konten haben unterschiedliche Währungen. Bitte ein einzelnes Konto wählen.",14));page.ResumeLayout();return;}
        switch(index){case 0:Overview();break;case 1:SubscriptionsPage();break;case 2:Calendar(snapshot.Entries);break;case 3:Age();break;case 4:Rules();break;}
        page.ResumeLayout(true);
    }
    internal void ShowSubscriptionMode(string value){mode=value;ShowTab(1);}
    internal void ShowFixedCosts(){bucket="Verbindlichkeiten";ShowTab(1);}
    internal static Button ActionButton(string text,Action action){var b=new Button{Text=text,UseMnemonic=false,AutoSize=true,Height=30,FlatStyle=FlatStyle.Flat,Padding=new Padding(7,3,7,3),Margin=new Padding(0,0,7,6)};b.FlatAppearance.BorderColor=Color.FromArgb(220,222,224);b.Click+=(_,_)=>action();return b;}
    internal static Label Label(string text,float size=13,bool bold=false)=>new(){Text=text,UseMnemonic=false,AutoSize=true,BackColor=Color.Transparent,Font=new Font("Segoe UI",size,bold?FontStyle.Bold:FontStyle.Regular,GraphicsUnit.Pixel),Margin=new Padding(0,0,0,10)};
    FlowLayoutPanel Stack(){var s=new FlowLayoutPanel{Dock=DockStyle.Fill,FlowDirection=FlowDirection.TopDown,WrapContents=false,AutoScroll=true};page.Controls.Add(s);return s;}
    FlowLayoutPanel Header(string title,string subtitle,Control? right=null){var h=new FlowLayoutPanel{Dock=DockStyle.Top,AutoSize=true,WrapContents=true,Margin=Padding.Empty};var labels=new FlowLayoutPanel{AutoSize=true,FlowDirection=FlowDirection.TopDown,WrapContents=false,Margin=new Padding(0,0,20,12)};labels.Controls.Add(Label(title,20,true));labels.Controls.Add(Label(subtitle,12));h.Controls.Add(labels);if(right!=null)h.Controls.Add(right);return h;}
    List<Subscription> Candidates()=>snapshot.Accounts.SelectMany(a=>Subscriptions.Detect(snapshot.Entries.Where(e=>e.AccountId==a.Id),a.BankName??a.Name,owner.AnalysisRules,owner.AnalysisAssignments)).ToList();
    void Overview(){
        var selector=new ComboBox{DropDownStyle=ComboBoxStyle.DropDownList,Width=180};selector.Items.AddRange(["Dieser Monat","3 Monate","Max"]);selector.SelectedIndex=period;selector.SelectedIndexChanged+=(_,_)=>{period=selector.SelectedIndex;ShowTab(0);};
        var header=Header("Financial Health","Money Mass Index (MMI) · "+snapshot.Name,selector);
        if(snapshot.Balance==null || snapshot.Entries.Length==0){page.Controls.Add(Label("Für diese Auswertung werden ein Kontostand und Umsätze benötigt."));page.Controls.Add(header);return;}
        var metric=Mmi.Calculate(snapshot.Entries,snapshot.Balance.Value,period,DateOnly.FromDateTime(DateTime.Today),Candidates(),owner.Preferences.MmiIncludeSavings);
        var display=new MmiView(metric,snapshot.Currency){Dock=DockStyle.Fill};page.Controls.Add(display);page.Controls.Add(header);
    }
    void SubscriptionsPage(){
        var all=Candidates();var items=all.Where(s=>s.Tab==bucket).ToList();var bar=new FlowLayoutPanel{Dock=DockStyle.Top,AutoSize=true,WrapContents=true};
        foreach(var name in new[]{"Abos","Verträge","Sparen","Verbindlichkeiten"}){var b=ActionButton(name,()=>{bucket=name;ShowTab(1);});if(name==bucket)b.BackColor=Color.White;bar.Controls.Add(b);}
        foreach(var name in new[]{"Kalender","Liste","Statistik"})bar.Controls.Add(ActionButton(name,()=>{mode=name;ShowTab(1);}));
        if(mode=="Kalender") {
            var charges=all.SelectMany(s=>s.Entries).DistinctBy(e=>e.AccountId+"/"+e.Id).ToList();
            foreach(var item in all.Where(s=>s.Cadence!=PaymentFrequency.Irregular)) {
                int step=item.Cadence==PaymentFrequency.Monthly?1:item.Cadence==PaymentFrequency.Quarterly?3:12;
                for(int n=step;n<=120;n+=step){var date=item.LastDate.AddMonths(n);if(date>month.AddMonths(1))break;if(date>=DateOnly.FromDateTime(DateTime.Today))charges.Add(new("forecast/"+item.Id+"/"+n,item.Entries[0].AccountId,date,-item.Average,snapshot.Currency,item.Name,"Voraussichtliche Abbuchung","",Pending:true));}
            }
            Calendar(charges);
        }
        else if(mode=="Statistik") {
            var stack=Stack();var stats=new SubscriptionStatistics(all,snapshot.Currency);stack.Controls.Add(stats);stack.SizeChanged+=(_,_)=>stats.Width=Math.Max(400,stack.ClientSize.Width-SystemInformation.VerticalScrollBarWidth-6);stats.Width=page.ClientSize.Width-60;
        } else {
            var list=new FlowLayoutPanel{Dock=DockStyle.Fill,AutoScroll=true,FlowDirection=FlowDirection.TopDown,WrapContents=false,Padding=new Padding(0,12,0,0)};
            bool shownPossible=false;
            foreach(var item in items.OrderBy(s=>s.Confidence<10)){
                if(item.Confidence<10&&!shownPossible){list.Controls.Add(Label("Mögliche "+bucket,12,true));shownPossible=true;}
                var row=new SubscriptionCard(item,snapshot.Currency,name=>{owner.SetRecurringAssignment(item.Entries[0].AccountId+"/"+item.Id,name);ShowTab(1);},()=>SubscriptionDetails(item));list.Controls.Add(row);
            }
            if(items.Count==0)list.Controls.Add(Label("Keine "+bucket+" erkannt.",14));
            void ResizeRows(){foreach(Control c in list.Controls)c.Width=Math.Max(200,list.ClientSize.Width-SystemInformation.VerticalScrollBarWidth-4);}list.SizeChanged+=(_,_)=>ResizeRows();ResizeRows();page.Controls.Add(list);

        }
        var summary=new TableLayoutPanel{Dock=DockStyle.Top,Height=54,ColumnCount=2,Padding=new Padding(12,6,12,6),BackColor=Color.FromArgb(235,238,235)};summary.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,50));summary.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,50));summary.Controls.Add(Label(bucket+"\n"+UiFormat.Money(items.Sum(s=>s.Monthly),snapshot.Currency),12,true),0,0);var grand=Label("Monatl. Fixkosten\n"+UiFormat.Money(all.Sum(s=>s.Monthly),snapshot.Currency),12,true);grand.Dock=DockStyle.Fill;grand.TextAlign=ContentAlignment.TopRight;summary.Controls.Add(grand,1,0);page.Controls.Add(summary);page.Controls.Add(bar);page.Controls.Add(Header("Abos & Verträge",items.Count+" erkannt · "+UiFormat.Money(items.Sum(s=>s.Monthly),snapshot.Currency)+" / Monat"));
    }
    void SubscriptionDetails(Subscription item){using var details=new Form{Text=item.Name,ClientSize=new Size(550,410),StartPosition=FormStartPosition.CenterParent,Font=Font};var table=Table(["Datum","Betrag","Verwendungszweck"]);foreach(var e in item.Entries.OrderByDescending(e=>e.Date))table.Rows.Add(e.Date.ToString("dd.MM.yyyy"),UiFormat.Money(e.Amount,e.Currency),e.Purpose);details.Controls.Add(table);if(item.CancellationUrl!=null){var b=ActionButton("Kündigung / Konto verwalten",()=>System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(item.CancellationUrl){UseShellExecute=true}));b.Dock=DockStyle.Bottom;details.Controls.Add(b);}details.ShowDialog(this);}
    void Calendar(IEnumerable<Entry> input){
        var entries=input.ToArray();var container=new Panel{Dock=DockStyle.Fill};page.Controls.Add(container);
        var nav=new FlowLayoutPanel{Dock=DockStyle.Top,Height=42};nav.Controls.Add(ActionButton("‹",()=>{month=month.AddMonths(-1);ShowTab(active);}));nav.Controls.Add(Label(month.ToString("MMMM yyyy",CultureInfo.GetCultureInfo("de-DE")),18,true));nav.Controls.Add(ActionButton("›",()=>{month=month.AddMonths(1);ShowTab(active);}));
        int weeks=(((int)month.DayOfWeek+6)%7+DateTime.DaysInMonth(month.Year,month.Month)+6)/7;float scale=DeviceDpi/96f;
        var scroll=new Panel{Dock=DockStyle.Fill,AutoScroll=true};var grid=new TableLayoutPanel{Dock=DockStyle.Top,Height=(int)((24+weeks*54)*scale),ColumnCount=7,RowCount=weeks+1};for(int i=0;i<7;i++)grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100f/7));grid.RowStyles.Add(new RowStyle(SizeType.Absolute,24*scale));for(int i=0;i<weeks;i++)grid.RowStyles.Add(new RowStyle(SizeType.Absolute,54*scale));scroll.Controls.Add(grid);
        string[] days=["Mo","Di","Mi","Do","Fr","Sa","So"];for(int i=0;i<7;i++)grid.Controls.Add(Label(days[i],13,true),i,0);
        var sums=entries.Where(e=>e.Date.Year==month.Year&&e.Date.Month==month.Month&&e.Amount<0).GroupBy(e=>e.Date.Day).ToDictionary(g=>g.Key,g=>-g.Sum(e=>e.Amount));decimal max=sums.Count>0?sums.Values.Max():0;int offset=((int)month.DayOfWeek+6)%7;
        for(int day=1;day<=DateTime.DaysInMonth(month.Year,month.Month);day++){int d=day;decimal amount=sums.GetValueOrDefault(day);float t=max>0?(float)(amount/max):0;var b=new CalendarDay(day,entries.Where(e=>e.Date==new DateOnly(month.Year,month.Month,d)&&e.Amount<0).ToArray(),snapshot.Currency,()=>ShowDay(entries,new DateOnly(month.Year,month.Month,d)));b.Dock=DockStyle.Fill;b.BackColor=Color.FromArgb(255,(int)(249-100*t),(int)(246-105*t));int cell=offset+day-1;grid.Controls.Add(b,cell%7,cell/7+1);}
        container.Controls.Add(scroll);container.Controls.Add(nav);
    }
    void ShowDay(Entry[] entries,DateOnly date){using var form=new Form{Text=date.ToString("dd.MM.yyyy"),ClientSize=new Size(600,380),StartPosition=FormStartPosition.CenterParent,Font=Font};var table=Table(["Händler","Betrag","Zweck"]);foreach(var e in entries.Where(e=>e.Date==date))table.Rows.Add(e.Merchant,UiFormat.Money(e.Amount,e.Currency),e.Purpose);form.Controls.Add(table);form.ShowDialog(this);}
    void Age(){
        var stack=Stack();var select=new ComboBox{DropDownStyle=ComboBoxStyle.DropDownList,Width=160};select.Items.AddRange(["letzte 10","letzte 25","letzte 50"]);select.SelectedIndex=0;stack.Controls.Add(Label("Money Age",20,true));stack.Controls.Add(Label("Wie alt ist das Geld, das du ausgibst?"));stack.Controls.Add(select);
        var result=Label("",32,true);var coverage=Label("",14);var explanation=Label("Das älteste Geld wird zuerst ausgegeben (FIFO).\nLücken in der Historie oder Dispo können Ausgaben teilweise ungedeckt lassen.");stack.Controls.Add(result);stack.Controls.Add(coverage);stack.Controls.Add(explanation);
        void Update(){var age=MoneyAgeResult.Calculate(snapshot.Entries,new[]{10,25,50}[select.SelectedIndex]);result.Text=age.Days is {} d?d.ToString("N1")+" Tage":"Noch keine gedeckten Ausgaben";coverage.Text=$"{age.Samples} Ausgaben im Fenster · {age.Total} insgesamt\n{age.Uncovered} teilweise oder vollständig ungedeckt"+(age.Previous is {} p?$"\nVorheriges Fenster: {p:N1} Tage":"");}select.SelectedIndexChanged+=(_,_)=>Update();Update();
    }
    void ShowAssignments(){
        using var dialog=new Form{Text="Eigene Regeln · Zuordnungen",ClientSize=new Size(660,430),Font=Font,StartPosition=FormStartPosition.CenterParent};
        var table=Table(["Konto","Händler","Zuordnung"]);
        void Reload(){table.Rows.Clear();foreach(var pair in owner.AnalysisAssignments){var account=snapshot.Accounts.FirstOrDefault(a=>pair.Key.StartsWith(a.Id+"/",StringComparison.Ordinal));if(account==null)continue;var merchant=pair.Key[(account.Id.Length+1)..];int i=table.Rows.Add(account.Name,merchant,pair.Value);table.Rows[i].Tag=pair.Key;}}
        var reset=ActionButton("Zurücksetzen",()=>{if(table.CurrentRow?.Tag is string key){owner.ResetRecurringAssignment(key);Reload();}});reset.Dock=DockStyle.Bottom;dialog.Controls.Add(table);dialog.Controls.Add(reset);Reload();dialog.ShowDialog(this);
    }
    void Rules(){
        var list=Table(["Aktiv","Regel","Priorität"]);foreach(var rule in owner.AnalysisRules){int i=list.Rows.Add(rule.Enabled?"Ja":"Nein",AssignmentRules.Summary(rule),rule.Priority);list.Rows[i].Tag=rule;}
        void Edit(AssignmentRule? rule){using var editor=new RuleAssistant(owner,snapshot,rule);if(editor.ShowDialog(this)==DialogResult.OK)ShowTab(4);}
        list.CellDoubleClick+=(_,e)=>{if(e.RowIndex>=0)Edit((AssignmentRule)list.Rows[e.RowIndex].Tag!);};
        var actions=new FlowLayoutPanel{Dock=DockStyle.Top,AutoSize=true};actions.Controls.Add(ActionButton("Regel-Assistent",()=>Edit(null)));actions.Controls.Add(ActionButton("Eigene Zuordnungen",ShowAssignments));actions.Controls.Add(ActionButton("Händler-Regeln",owner.OpenMerchantRules));actions.Controls.Add(ActionButton("Bearbeiten",()=>{if(list.CurrentRow?.Tag is AssignmentRule r)Edit(r);}));actions.Controls.Add(ActionButton("Aktivieren / Deaktivieren",()=>{if(list.CurrentRow?.Tag is AssignmentRule r){owner.SaveAnalysisRule(r with{Enabled=!r.Enabled},false,snapshot.Accounts.Select(a=>a.Id).ToHashSet());ShowTab(4);}}));actions.Controls.Add(ActionButton("Löschen",()=>{if(list.CurrentRow?.Tag is AssignmentRule r){owner.DeleteAnalysisRule(r.Id);ShowTab(4);}}));
        page.Controls.Add(list);page.Controls.Add(actions);page.Controls.Add(Header("Regeln & Zuordnungen","Eigene Bedingungen und Kategorien · erste passende Regel nach Priorität"));
    }
    internal static DataGridView Table(string[] columns){var grid=new DataGridView{Dock=DockStyle.Fill,ReadOnly=true,AllowUserToAddRows=false,AllowUserToDeleteRows=false,RowHeadersVisible=false,AutoSizeColumnsMode=DataGridViewAutoSizeColumnsMode.Fill,SelectionMode=DataGridViewSelectionMode.FullRowSelect,MultiSelect=false,BackgroundColor=Color.FromArgb(248,250,249),BorderStyle=BorderStyle.None,CellBorderStyle=DataGridViewCellBorderStyle.SingleHorizontal,GridColor=Color.FromArgb(232,234,233),AutoGenerateColumns=false,EnableHeadersVisualStyles=false};grid.ColumnHeadersDefaultCellStyle.BackColor=Color.FromArgb(240,243,241);grid.RowTemplate.Height=38;grid.DefaultCellStyle.BackColor=grid.BackgroundColor;grid.DataBindingComplete+=(_,_)=>grid.ClearSelection();foreach(var name in columns)grid.Columns.Add(name,name);return grid;}
}
internal sealed class MmiView(Mmi metric,string currency) : Control
{
    protected override void OnPaint(PaintEventArgs e){
        var g=e.Graphics;g.SmoothingMode=SmoothingMode.AntiAlias;float scale=DeviceDpi/96f;g.ScaleTransform(scale,scale);float width=Width/scale;
        decimal total=metric.Expenses+metric.Savings+Math.Max(0,metric.Balance);float angle=-90;Color[] colors=[Color.FromArgb(207,63,70),Color.FromArgb(50,115,218),Color.FromArgb(37,142,88)];decimal[] values=[metric.Expenses,metric.Savings,Math.Max(0,metric.Balance)];
        using(var basePen=new Pen(Color.FromArgb(225,230,227),18))g.DrawEllipse(basePen,16,22,150,150);
        if(total>0)for(int i=0;i<3;i++){float sweep=(float)(values[i]/total)*360;if(sweep>0){using var pen=new Pen(colors[i],18);g.DrawArc(pen,16,22,150,150,angle,sweep);}angle+=sweep;}
        void Text(string text,float size,float x,float y,float w,Color color,bool bold=false){using var f=new Font("Segoe UI",size,bold?FontStyle.Bold:FontStyle.Regular,GraphicsUnit.Pixel);using var b=new SolidBrush(color);g.DrawString(text,f,b,new RectangleF(x,y,w,110));}
        Text(metric.Rating,16,35,82,135,Color.FromArgb(30,80,55),true);
        Text($"{metric.Score:N2}  Score\n{metric.SavingsRate:+0%;-0%;0%}  Sparrate\n{(double.IsInfinity(metric.Buffer)?"∞":metric.Buffer.ToString("N1"))} Mon.  Puffer",19,12,195,215,Color.FromArgb(35,80,65),true);
        Text("Money Mass Index",22,220,28,width-230,Color.FromArgb(35,40,38),true);
        Text("Der MMI verbindet die Reichweite deines Kontostands mit deiner Sparrate.\n\nDie Pufferreichweite trägt den Hauptanteil. Eine positive Sparrate erhöht den Score.",14,220,70,width-245,Color.DimGray);
        string[] labels=["Ausgegeben","Geparkt","Liquide"];for(int i=0;i<3;i++){float x=i*width/3;Text(labels[i],14,x,330,width/3,colors[i],true);Text(UiFormat.Money(i==2?metric.Balance:values[i],currency),24,x,360,width/3,colors[i],true);}
        Text("Kontostand ≠ Notfallreserve",12,0,425,width,Color.DimGray);
    }
}
