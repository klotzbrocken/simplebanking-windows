using System.Globalization;
using PdfSharp.Drawing;
using PdfSharp.Fonts;
using PdfSharp.Pdf;

namespace SimpleBanking;

// Geometry, typography, colors and page sequence follow MonthlyReportPDFRenderer.swift.
internal static class MonthlyReport
{
    static readonly CultureInfo De=CultureInfo.GetCultureInfo("de-DE");
    static readonly XColor Bg=XColor.FromArgb(237,232,220), Ink=XColor.FromArgb(31,31,31), Gray=XColor.FromArgb(102,102,102), Red=XColor.FromArgb(204,17,17), Green=XColor.FromArgb(42,122,79), Teal=XColor.FromArgb(26,112,112), Rule=XColor.FromArgb(178,173,161), Bar=XColor.FromArgb(90,138,138);
    sealed class Fonts : IFontResolver {
        public FontResolverInfo? ResolveTypeface(string familyName,bool bold,bool italic)=>new(bold?"SpaceMono-Bold":"SpaceMono-Regular");
        public byte[] GetFont(string faceName){var assembly=typeof(MonthlyReport).Assembly;using var stream=assembly.GetManifestResourceStream(assembly.GetManifestResourceNames().Single(n=>n.EndsWith(faceName+".ttf")))!;using var memory=new MemoryStream();stream.CopyTo(memory);return memory.ToArray();}
    }
    internal static Entry[] Rows(IEnumerable<Entry> all,DateOnly month)=>all.Where(e=>!e.Pending&&e.Date.Year==month.Year&&e.Date.Month==month.Month).OrderBy(e=>e.Date).ThenBy(e=>e.Id,StringComparer.Ordinal).ToArray();
    internal static void Write(string path,Account account,IEnumerable<Entry> history,DateOnly month){
        if(GlobalFontSettings.FontResolver==null)GlobalFontSettings.FontResolver=new Fonts();
        var all=history.Where(e=>e.AccountId==account.Id&&e.Currency==account.Currency).ToArray();var rows=Rows(all,month);
        var previous=Rows(all,month.AddMonths(-1)).Where(p=>!(p.Date.Day>=25&&rows.Any(c=>c.Date.Day<=5&&c.Merchant.Trim().ToLowerInvariant()==p.Merchant.Trim().ToLowerInvariant()&&Math.Abs(Math.Abs(c.Amount)-Math.Abs(p.Amount))<=.01m))).ToArray();
        decimal income=rows.Where(e=>e.Amount>0).Sum(e=>e.Amount),expense=-rows.Where(e=>e.Amount<0).Sum(e=>e.Amount),net=income-expense;
        string Money(decimal n)=>UiFormat.Money(n,account.Currency);
        var categories=rows.Where(e=>e.Amount<0).GroupBy(e=>string.IsNullOrWhiteSpace(e.Category)?"Sonstiges":e.Category).Select(g=>(Name:g.Key,Amount:-g.Sum(e=>e.Amount))).OrderByDescending(c=>c.Amount).Take(8).ToArray();
        var recurring=RecurringCosts.Analyze(all).Where(p=>p.EntryIds.Any(id=>rows.Any(e=>e.Id==id))).OrderByDescending(p=>p.AverageAmount).Take(10).ToArray();
        var chunks=rows.Chunk(55).ToArray();int total=2+chunks.Length;
        using var document=new PdfDocument();document.Info.Title="simple.report · "+month.ToString("MMMM yyyy",De);document.Info.Author="simplebanking";
        XGraphics graphics=null!;
        void Text(string value,double x,double baseline,double size=9,XColor? color=null,bool bold=false,double width=483,bool right=false){
            var font=new XFont("SpaceMono",size,bold?XFontStyleEx.Bold:XFontStyleEx.Regular);value=value.Replace('\r',' ').Replace('\n',' ');
            if(graphics.MeasureString(value,font).Width>width){while(value.Length>0&&graphics.MeasureString(value+"…",font).Width>width)value=value[..^1];value+="…";}
            if(right)x-=graphics.MeasureString(value,font).Width;
            graphics.DrawString(value,font,new XSolidBrush(color??Ink),x,842-baseline);
        }
        void Rect(double x,double y,double w,double h,XColor color)=>graphics.DrawRectangle(new XSolidBrush(color),x,842-y-h,Math.Max(0,w),h);
        void Line(double y,double x=56,double end=539)=>graphics.DrawLine(new XPen(Rule,.4),x,842-y,end,842-y);
        void Section(string title,double y){graphics.DrawPolygon(new XSolidBrush(Ink),new[]{new XPoint(56,842-y-9),new XPoint(62,842-y-5),new XPoint(56,842-y-1)},XFillMode.Winding);Text(title,69,y,13,Ink,true);}
        void Rainbow(double y){int[] colors=[0xE63946,0xF4A261,0xE9C469,0x52B788,0x2A9D8F,0x4895EF,0x9B5DE5];for(int i=0;i<colors.Length;i++)Rect(i*85,y,85,6,XColor.FromArgb((colors[i]>>16)&255,(colors[i]>>8)&255,colors[i]&255));}
        void Page(int number,string tag){var page=document.AddPage();page.Width=XUnit.FromPoint(595);page.Height=XUnit.FromPoint(842);graphics=XGraphics.FromPdfPage(page);Rect(0,0,595,842,Bg);Rainbow(832);Rainbow(10);Text("simple.report",56,20,7.5,Gray);Text($"SEITE {number} / {total}",539,20,7.5,Gray,right:true);Text(number==1?"simple.report":month.ToString("MMMM yyyy",De).ToUpperInvariant()+" — "+account.Name,56,808,number==1?15:13,number==1?Red:Ink,true,345);Text(tag,539,808,8,Gray,right:true,width:125);Line(800);}
        Page(1,"MONATSÜBERSICHT");Text(month.ToString("MMMM yyyy",De).ToUpperInvariant(),56,784,16,Ink,true,310);Text(account.BankName??account.Name,539,784,8,Gray,width:170,right:true);
        string iban=account.Iban.Replace(" ","");Text(iban.Length>8?iban[..4]+" •••• •••• •••• "+iban[^4..]:iban,539,771,8,Gray,right:true);Line(762);
        string[] labels=["EINNAHMEN","AUSGABEN","SALDO","BUCHUNGEN"],values=[Money(income),Money(expense),Money(Math.Abs(net)),rows.Length.ToString()];XColor[] colors=[Green,Red,net>=0?Teal:Red,Ink];
        for(int i=0;i<4;i++){double x=56+i*120.75;Rect(x,690,119.75,52,XColor.FromArgb(220,215,202));graphics.DrawRectangle(new XPen(Rule,.4),x,100,119.75,52);Text(labels[i],x+8,726,7.5,Gray);Text(values[i],x+8,702,i==3?18:13,colors[i],true,104);}
        Section("Zusammenfassung",674);double y=654;
        var narrative=new List<string>{"Du hast "+Money(income)+" eingenommen und "+Money(expense)+" ausgegeben.",net>=0?"Damit bleibt ein Plus von "+Money(net)+".":"Damit ergibt sich ein Minus von "+Money(-net)+"."};
        decimal prev=-previous.Where(e=>e.Amount<0).Sum(e=>e.Amount);if(prev>0&&Math.Abs(expense-prev)/prev>=.05m)narrative.Add($"Deine Ausgaben sind zum Vormonat um {Math.Abs((expense-prev)/prev):P0} "+(expense>prev?"gestiegen.":"gesunken."));
        Rect(57,y-narrative.Count*14-4,1.5,narrative.Count*14+4,Rule);foreach(string line in narrative){Text(line,68,y,width:469);y-=14;}
        y-=18;Section("Cashflow",y);y-=20;decimal maximum=Math.Max(income,expense);
        foreach(var flow in new[]{("EIN",income), ("AUS",expense)}){Text(flow.Item1,56,y,8,Gray);double w=maximum>0?(double)(flow.Item2/maximum)*265.65:0;Rect(94,y-2,w,10,flow.Item1=="EIN"?XColor.FromArgb(200,210,201):Bar);if(flow.Item1=="EIN")for(double x=94;x<94+w;x+=3.5)graphics.DrawLine(new XPen(Bar,.4),x,842-y+2,x,842-y-8);Text(Money(flow.Item2),539,y,right:true);y-=19;}
        Text("NET",56,y,8,Gray);Text(Money(net),539,y,9,net>=0?Green:Red,true,right:true);y-=32;Section("Auffälligkeiten",y);y-=20;
        var insights=new List<(string,XColor)>();var highest=rows.OrderByDescending(e=>e.Amount).FirstOrDefault();var lowest=rows.OrderBy(e=>e.Amount).FirstOrDefault();
        if(highest?.Amount>0)insights.Add(($"Größte Einnahme: {Money(highest.Amount)} von {highest.Merchant}.",Green));if(lowest?.Amount<0)insights.Add(($"Größte Ausgabe: {Money(-lowest.Amount)} bei {lowest.Merchant}.",Red));
        insights.Add((net>=0?$"Du hast diesen Monat {Money(net)} gespart – gut gemacht.":$"Diesen Monat hast du {Money(-net)} mehr ausgegeben als eingenommen.",net>=0?Green:Red));
        if(categories.Length>0&&expense>0&&categories[0].Amount/expense>.2m)insights.Add(($"{categories[0].Name} war mit {(int)(categories[0].Amount/expense*100)}% deine größte Ausgabenkategorie.",Gray));
        if(recurring.Length>0&&expense>0)insights.Add(($"Fixkosten: {Money(recurring.Sum(p=>p.AverageAmount))} – {(int)(recurring.Sum(p=>p.AverageAmount)/expense*100)}% deiner Ausgaben.",Gray));
        foreach(var item in insights){graphics.DrawEllipse(new XSolidBrush(item.Item2),57,842-y-3,5,5);Text(item.Item1,69,y,width:470);y-=16;}graphics.Dispose();
        Page(2,"DETAILS");y=782;Section("Ausgabenübersicht",y);y-=18;
        foreach(var c in categories){Text(c.Name.ToUpperInvariant(),56,y,7.5,Gray,width:105);Rect(164,y-2,(double)(c.Amount/categories[0].Amount)*183.54,9,Bar);Text(Money(c.Amount),415.5,y,8,right:true);Text($"{(int)(c.Amount/expense*100)}%",467.5,y,8,Gray,right:true);decimal old=-previous.Where(e=>e.Category==c.Name&&e.Amount<0).Sum(e=>e.Amount);if(old>0)Text((c.Amount>=old?"↑":"↓")+(int)(Math.Abs(c.Amount-old)/c.Amount*100)+"%",539,y,8,c.Amount>=old?Red:Green,right:true);y-=15;}
        y-=14;Section("Fixkosten",y);y-=18;int half=(recurring.Length+1)/2;for(int i=0;i<half;i++){for(int col=0;col<2;col++){int index=i+col*half;if(index>=recurring.Length)continue;double x=56+col*241.5;Text(recurring[index].Merchant.ToUpperInvariant(),x,y,7.5,Gray,width:173);Text(Money(recurring[index].AverageAmount),x+233.5,y,8,right:true);Line(y-9,x,x+231.5);}y-=16;}
        y-=14;Section("Wichtige Buchungen",y);y-=18;
        var highlights=rows.Where(e=>e.Amount<0).OrderBy(e=>e.Amount).Take(3).Concat(rows.Where(e=>e.Amount>0).OrderByDescending(e=>e.Amount).Take(2)).Concat(rows.Where(e=>e.Amount>0&&(e.Purpose+e.Merchant).Contains("Erstattung",StringComparison.OrdinalIgnoreCase)).Take(2)).Concat(rows.Where(e=>e.Amount<0).OrderBy(e=>e.Amount).Skip(3)).DistinctBy(e=>e.Id).Take(12).OrderBy(e=>e.Date);
        foreach(var e in highlights){Text(e.Date.ToString("dd. MMM",De),56,y,7.5,Gray,width:49);Text(e.Merchant.ToUpperInvariant(),108,y,8,width:160);Text(e.Category,471,y,7.5,Gray,width:190,right:true);Text((e.Amount>=0?"+":"")+Money(e.Amount),539,y,8,e.Amount>=0?Green:Red,right:true,width:66);Line(y-9);y-=16;}graphics.Dispose();
        for(int i=0;i<chunks.Length;i++){Page(i+3,"ALLE UMSÄTZE");Text("DATUM",56,786,7,Gray);Text("BUCHUNG",108,786,7,Gray);Text("KATEGORIE",316,786,7,Gray);Text("BETRAG",539,786,7,Gray,right:true);Line(781);y=768;foreach(var e in chunks[i]){Text(e.Date.ToString("dd. MMM",De),56,y,7.5,Gray,width:48);Text(e.Merchant.ToUpperInvariant(),108,y,7.5,width:200);Text(e.Category.ToUpperInvariant(),316,y,7,Gray,width:110);Text((e.Amount>=0?"+":"")+Money(e.Amount),539,y,7.5,e.Amount>=0?Green:Ink,right:true,width:105);y-=13;}graphics.Dispose();}
        document.Save(path);
    }
}

internal sealed partial class MainWindow
{
    void ExportMonthlyReport(){
        if(!RequireUnlocked())return;if(SelectedAccount is not {} account||unifiedAccounts){MessageBox.Show(this,"Bitte ein einzelnes Konto für simple.report auswählen.");return;}
        using var choose=new Form{Text="simple.report",ClientSize=new Size(360,150),StartPosition=FormStartPosition.CenterParent,Font=Font,FormBorderStyle=FormBorderStyle.FixedDialog,MaximizeBox=false,MinimizeBox=false};
        var date=new DateTimePicker{Format=DateTimePickerFormat.Custom,CustomFormat="MMMM yyyy",ShowUpDown=true,Location=new Point(24,47),Width=310};choose.Controls.Add(new Label{Text="Monatsübersicht als PDF exportieren",AutoSize=true,Location=new Point(24,20)});choose.Controls.Add(date);var accept=new Button{Text="PDF erstellen …",DialogResult=DialogResult.OK,Location=new Point(184,100),Width=150};choose.Controls.Add(accept);choose.AcceptButton=accept;if(choose.ShowDialog(this)!=DialogResult.OK)return;
        using var save=new SaveFileDialog{Filter="PDF|*.pdf",FileName="simple.report-"+date.Value.ToString("yyyy-MM")+".pdf"};if(save.ShowDialog(this)!=DialogResult.OK)return;
        try{MonthlyReport.Write(save.FileName,account,data.Entries,DateOnly.FromDateTime(date.Value));status.Text="simple.report exportiert";}catch(Exception e) when(e is IOException or UnauthorizedAccessException or InvalidOperationException){MessageBox.Show(this,e.Message,"PDF konnte nicht erstellt werden");}
    }
}
