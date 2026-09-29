using System.Drawing.Drawing2D;
namespace SimpleBanking;

// TransactionRowNew: 16px unread gutter, 20px logo, 10px gap, 14/12px padding.
internal sealed class BookingRow : Control
{
    readonly Entry entry;
    readonly bool hidden, categories, pendingPill;
    readonly Image? logo;
    readonly ToolTip hint = new();
    bool selected;
    readonly ThemePalette theme;
    internal BookingRow(Entry entry, bool hidden, bool categories, bool pendingAsPill,ThemePalette? theme=null) {
        this.theme=theme??ThemePalette.For(new());SetStyle(ControlStyles.SupportsTransparentBackColor,true);BackColor=Color.Transparent;
        this.entry=entry; this.hidden=hidden; this.categories=categories; pendingPill = entry.Pending && pendingAsPill; logo=this.theme.MerchantLogos?MerchantArtwork.Load(entry):null;
        Height = categories || pendingPill ? 62 : 48; Margin=Padding.Empty; TabStop=true; Cursor=Cursors.Hand;
        SetStyle(ControlStyles.Selectable|ControlStyles.StandardClick|ControlStyles.StandardDoubleClick|ControlStyles.OptimizedDoubleBuffer|ControlStyles.AllPaintingInWmPaint|ControlStyles.UserPaint,true);
        AccessibleRole=AccessibleRole.ListItem; AccessibleName=entry.Merchant + ", " + (hidden ? "Betrag verborgen" : UiFormat.Money(entry.Amount,entry.Currency));
        hint.SetToolTip(this,entry.Purpose); SizeChanged+=(_,_)=>Invalidate();
    }
    internal void SetSelected(bool value) { selected=value; Invalidate(); }
    protected override void OnMouseDown(MouseEventArgs e) { Focus(); base.OnMouseDown(e); }
    Color Tone(Color color) => !entry.Pending ? color : Color.FromArgb(
        (int)(color.R*.65+BackColor.R*.35),(int)(color.G*.65+BackColor.G*.35),(int)(color.B*.65+BackColor.B*.35));
    protected override void OnPaint(PaintEventArgs e) {
        var g = e.Graphics;
        g.SmoothingMode=SmoothingMode.AntiAlias;
        if(selected) { using var fill=new SolidBrush(Tone(ThemePalette.Blend(theme.Accent,theme.Surface,.20))); using var shape=Rounded(new RectangleF(16,0,Width-16,Height),10); g.FillPath(fill,shape); }
        if(!entry.Read) { using var dot=new SolidBrush(Color.FromArgb(0,122,255)); g.FillEllipse(dot,10,Height/2-3,6,6); }
        int center=Height/2;
        if(logo!=null) {
            ArtworkDrawing.Draw(g,logo,new Rectangle(28,center-16,32,32),1f);
        }
        else if(theme.CategoryIcons)DrawCategory(g,new Point(34,center-10),entry.Category,theme.Secondary);
        else {using var mosaic=new SolidBrush(theme.Accent);for(int i=0;i<6;i++)if((entry.Merchant.Sum(c=>(int)c)&(1<<i))!=0)g.FillRectangle(mosaic,30+i%2*9,center-10+i/2*7,7,5);}
        using var main=theme.Font(14,true);
        using var small=theme.Font(12);
        using var body=theme.Font(11);
        var flags=TextFormatFlags.NoPrefix|TextFormatFlags.EndEllipsis|TextFormatFlags.VerticalCenter|TextFormatFlags.SingleLine;
        string amount=hidden ? "••••" : UiFormat.Money(entry.Amount,entry.Currency);
        int amountWidth=Math.Min(Width/2,TextRenderer.MeasureText(amount,main).Width+4);
        int amountLeft=Width-14-amountWidth;
        int textRight=amountLeft-10-(entry.Note.Length>0?14:0);
        int nameWidth=Math.Max(10,textRight-70);
        bool wide=Width>=680; if(wide) nameWidth=Math.Min(nameWidth,200);
        int nameY=categories?center-19:center-10;
        TextRenderer.DrawText(g,theme.Text(entry.Merchant),main,new Rectangle(70,nameY,nameWidth,20),Tone(theme.Ink),flags);
        if(categories) TextRenderer.DrawText(g,entry.Category,small,new Rectangle(70,center+2,Math.Max(10,nameWidth),20),Tone(theme.Secondary),flags);
        if(wide) TextRenderer.DrawText(g,entry.Purpose,body,new Rectangle(70+nameWidth+8,center-10,Math.Max(1,textRight-68-nameWidth),20),Tone(theme.Secondary),flags);
        if(entry.Note.Length>0) { using var pen=new Pen(Tone(Color.Gray),1); g.DrawLine(pen,amountLeft-18,center+3,amountLeft-11,center-4); g.DrawLine(pen,amountLeft-19,center+5,amountLeft-12,center+5); }
        TextRenderer.DrawText(g,amount,main,new Rectangle(amountLeft,pendingPill?center-20:center-10,amountWidth,20),Tone(entry.Amount>0?theme.Positive:theme.IsDefault?theme.Ink:theme.Negative),flags|TextFormatFlags.Right);
        if(pendingPill) { int width=TextRenderer.MeasureText("Vorgemerkt",small).Width+12; Pill(g,"Vorgemerkt",small,new Point(Width-14-width,center+2),width,Tone(Color.FromArgb(174,92,0)),Tone(Color.FromArgb(255,239,213))); }
        if(Focused) ControlPaint.DrawFocusRectangle(g,new Rectangle(17,1,Width-19,Height-3));
    }
    static void Pill(Graphics g,string text,Font font,Point location,int maximum,Color ink,Color fill) {
        int width=Math.Min(maximum,TextRenderer.MeasureText(text,font).Width+10);
        using var path=Rounded(new RectangleF(location.X,location.Y,width,16),8); using var brush=new SolidBrush(fill); g.FillPath(brush,path);
        TextRenderer.DrawText(g,text,font,new Rectangle(location.X+5,location.Y,width-10,16),ink,TextFormatFlags.NoPrefix|TextFormatFlags.EndEllipsis|TextFormatFlags.VerticalCenter|TextFormatFlags.SingleLine);
    }
    static GraphicsPath Rounded(RectangleF r,float radius) {
        var p=new GraphicsPath(); float d=Math.Min(radius*2,Math.Min(r.Width,r.Height));
        p.AddArc(r.X,r.Y,d,d,180,90);p.AddArc(r.Right-d,r.Y,d,d,270,90);p.AddArc(r.Right-d,r.Bottom-d,d,d,0,90);p.AddArc(r.X,r.Bottom-d,d,d,90,90);p.CloseFigure();return p;
    }
    internal static void DrawCategory(Graphics g,Point origin,string category,Color color) {
        var state=g.Save();g.TranslateTransform(origin.X,origin.Y);using var pen=new Pen(color,1.4f);
        switch(category) {
            case "Wohnen & Kredit": case "Wohnen": g.DrawLines(pen,new Point[]{new(2,9),new(10,2),new(18,9)});g.DrawRectangle(pen,4,9,12,9);break;
            case "Shopping": g.DrawLines(pen,new Point[]{new(1,3),new(4,3),new(6,13),new(16,13),new(18,6),new(5,6)});g.DrawEllipse(pen,6,16,2,2);g.DrawEllipse(pen,14,16,2,2);break;
            case "Lebensmittel":case "Essen & Alltag":case "Gastronomie":g.DrawLine(pen,5,2,5,18);g.DrawLine(pen,2,2,2,7);g.DrawLine(pen,8,2,8,7);g.DrawLine(pen,2,7,8,7);g.DrawLine(pen,15,2,15,18);g.DrawArc(pen,11,2,8,9,90,180);break;
            case "Versicherungen":g.DrawPolygon(pen,new Point[]{new(3,3),new(10,1),new(17,3),new(16,12),new(10,18),new(4,12)});break;
            case "Abos & Digital":case "Abos":g.DrawRectangle(pen,1,4,18,12);g.DrawPolygon(pen,new Point[]{new(8,7),new(13,10),new(8,13)});break;
            case "Einkommen":g.DrawRectangle(pen,2,6,16,12);g.DrawRectangle(pen,7,2,6,4);g.DrawLine(pen,2,11,18,11);break;
            case "Gehalt":g.DrawEllipse(pen,1,1,18,18);g.DrawArc(pen,6,4,8,12,70,220);g.DrawLine(pen,4,8,12,8);g.DrawLine(pen,4,12,12,12);break;
            case "Mobilität":case "Mobilitaet":g.DrawPolygon(pen,new Point[]{new(2,8),new(5,3),new(15,3),new(18,8),new(18,15),new(2,15)});g.DrawLine(pen,2,8,18,8);g.DrawLine(pen,4,15,4,18);g.DrawLine(pen,16,15,16,18);g.DrawLine(pen,4,11,7,11);g.DrawLine(pen,13,11,16,11);break;
            case "Gesundheit":g.DrawRectangle(pen,2,6,16,12);g.DrawRectangle(pen,7,2,6,4);g.DrawLine(pen,10,8,10,16);g.DrawLine(pen,6,12,14,12);break;
            case "Freizeit":g.DrawRectangle(pen,1,4,18,12);g.DrawLine(pen,10,4,10,16);g.DrawEllipse(pen,7,7,6,6);break;
            case "Sparen":g.DrawLines(pen,new Point[]{new(2,2),new(2,18),new(18,18)});g.DrawLines(pen,new Point[]{new(4,14),new(9,9),new(12,11),new(18,4)});g.DrawLines(pen,new Point[]{new(13,4),new(18,4),new(18,9)});break;
            case "Umbuchungen":g.DrawArc(pen,2,2,16,16,10,155);g.DrawArc(pen,2,2,16,16,190,155);g.DrawLines(pen,new Point[]{new(1,10),new(2,15),new(7,14)});g.DrawLines(pen,new Point[]{new(13,6),new(18,5),new(19,10)});break;
            default:foreach(int x in new[]{3,11})foreach(int y in new[]{3,11})g.DrawRectangle(pen,x,y,6,6);break;
        }
        g.Restore(state);
    }
    protected override void Dispose(bool disposing) { if(disposing){logo?.Dispose();hint.Dispose();}base.Dispose(disposing); }
}
