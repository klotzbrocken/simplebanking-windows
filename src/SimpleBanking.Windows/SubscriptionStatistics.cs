using System.Drawing.Drawing2D;
namespace SimpleBanking;

// SubscriptionsView.statsContent: two forecast cards, category ring and category rows.
internal sealed class SubscriptionStatistics : Control
{
    readonly (string Category,decimal Yearly)[] slices;
    readonly decimal yearly;
    readonly string currency;
    internal SubscriptionStatistics(IEnumerable<Subscription> items,string currency){this.currency=currency;slices=items.Where(s=>s.Monthly>0).GroupBy(s=>s.Category).Select(g=>(g.Key,g.Sum(s=>s.Monthly)*12)).OrderByDescending(s=>s.Item2).ToArray();yearly=slices.Sum(s=>s.Yearly);DoubleBuffered=true;Height=330+slices.Length*42;}
    internal SubscriptionStatistics(IEnumerable<RecurringPayment> items,string currency){this.currency=currency;slices=items.Where(p=>p.Confidence>=.6&&p.Frequency!=PaymentFrequency.Irregular).GroupBy(p=>p.Category).Select(g=>(g.Key,g.Sum(p=>p.AverageAmount*(p.Frequency==PaymentFrequency.Monthly?12:p.Frequency==PaymentFrequency.Quarterly?4:1)))).OrderByDescending(s=>s.Item2).ToArray();yearly=slices.Sum(s=>s.Yearly);DoubleBuffered=true;Height=330+slices.Length*42;}
    static (string,Color) Style(string category)=>category switch{
        "streaming"=>("Streaming",ColorTranslator.FromHtml("#D98A8A")),"software"=>("Software/Apps",ColorTranslator.FromHtml("#7FA6CE")),"telecom"=>("Telekommunikation",ColorTranslator.FromHtml("#E0B36B")),"insurance"=>("Versicherung",ColorTranslator.FromHtml("#7FAE94")),"utilities"=>("Versorger",ColorTranslator.FromHtml("#C98A3D")),"membership"=>("Mitgliedschaft",ColorTranslator.FromHtml("#C65A5A")),"finance"=>("Finanzen",ColorTranslator.FromHtml("#4E79A7")),"bankFees"=>("Bankgebühren",ColorTranslator.FromHtml("#E0B36B")),"transport"=>("Mobilität",ColorTranslator.FromHtml("#4F8A6A")),_=>("Sonstiges",ColorTranslator.FromHtml("#B0A699"))};
    protected override void OnPaint(PaintEventArgs e){
        base.OnPaint(e);var theme=ThemeStyling.Current;var g=e.Graphics;g.SmoothingMode=SmoothingMode.AntiAlias;float scale=DeviceDpi/96f;g.ScaleTransform(scale,scale);float w=Width/scale;
        void Text(string text,float size,RectangleF rect,bool bold=false,bool right=false){using var font=theme.Font(size,bold);using var format=new StringFormat{Alignment=right?StringAlignment.Far:StringAlignment.Near,Trimming=StringTrimming.EllipsisCharacter,FormatFlags=StringFormatFlags.NoWrap};using var ink=new SolidBrush(theme.Ink);g.DrawString(theme.Text(text),font,ink,rect,format);}
        void Card(RectangleF rect){using var p=new GraphicsPath();float d=20;p.AddArc(rect.X,rect.Y,d,d,180,90);p.AddArc(rect.Right-d,rect.Y,d,d,270,90);p.AddArc(rect.Right-d,rect.Bottom-d,d,d,0,90);p.AddArc(rect.X,rect.Bottom-d,d,d,90,90);p.CloseFigure();using var fill=new SolidBrush(theme.Surface);if(theme.SquareControls)g.FillRectangle(fill,rect);else g.FillPath(fill,p);using var pen=new Pen(Color.FromArgb(226,228,227));g.DrawPath(pen,p);}
        if(yearly<=0){Text("Noch keine bestätigten Abos für eine Auswertung.",14,new(20,180,w-40,40));return;}
        for(int i=0;i<2;i++){float x=20+i*(w-28)/2;Card(new(x,10,(w-52)/2,72));Text(i==0?"Jahres-Hochrechnung":"Ø pro Monat",11,new(x+14,24,(w-80)/2,18));Text(UiFormat.Money(i==0?yearly:yearly/12,currency),20,new(x+14,45,(w-80)/2,30),true);}
        float angle=-90;var ring=new RectangleF(w/2-78,114,156,156);
        foreach(var s in slices){float sweep=(float)(s.Yearly/yearly*360);using var pen=new Pen(Style(s.Category).Item2,26);g.DrawArc(pen,ring,angle,sweep);angle+=sweep;}
        float y=314;foreach(var s in slices){Card(new(20,y,w-40,34));var style=Style(s.Category);using var brush=new SolidBrush(style.Item2);g.FillEllipse(brush,32,y+12,10,10);Text(style.Item1,13,new(52,y+8,w-250,22));Text(Math.Round(s.Yearly/yearly*100)+" %",12,new(w-208,y+8,65,22),right:true);Text(UiFormat.Money(s.Yearly,currency),13,new(w-133,y+8,100,22),true,true);y+=42;}
    }
}
