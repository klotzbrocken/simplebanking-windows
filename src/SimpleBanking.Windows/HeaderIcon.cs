using System.Drawing.Drawing2D;
namespace SimpleBanking;

internal sealed class HeaderIcon : Button
{
    string symbol;
    internal Color? Tint;
    internal bool TextMode;
    internal int Badge;
    readonly Func<bool>? active;
    readonly ToolTip tip = new();
    internal HeaderIcon(string symbol,string label,Action action,Func<bool>? active=null) {
        SetStyle(ControlStyles.UserPaint|ControlStyles.AllPaintingInWmPaint|ControlStyles.OptimizedDoubleBuffer|ControlStyles.SupportsTransparentBackColor,true);
        BackColor=Color.Transparent;
        this.symbol=symbol;this.active=active;Size=new(28,28);Margin=new(0,0,4,0);FlatStyle=FlatStyle.Flat;FlatAppearance.BorderSize=0;
        AccessibleName=label;tip.SetToolTip(this,label);Click+=(_,_)=>action();
    }
    internal void SetSymbol(string value) { symbol=value;Invalidate(); }
    internal void SetLabel(string value){AccessibleName=value;tip.SetToolTip(this,value);}
    protected override void OnPaint(PaintEventArgs e) {
        base.OnPaintBackground(e);
        var g=e.Graphics;float extent=symbol is "subtitle" or "metrics" or "daily" or "bankfees"?18:28;g.ScaleTransform(Width/extent,Height/extent);g.SmoothingMode=SmoothingMode.AntiAlias;
        if(TextMode){string text=symbol switch{"dashboard"=>"AUSW","inbox"=>"INBOX","send"=>"SEND","search"=>"SU","filter"=>"FIL","categories"=>"KAT","pin"=>"PIN","all"=>"ALLE",_=>"…"};using var font=new Font("Consolas",symbol=="inbox"?7:8,FontStyle.Bold,GraphicsUnit.Pixel);TextRenderer.DrawText(g,text,font,new Rectangle(0,0,(int)extent,(int)extent),Tint??Color.Blue,TextFormatFlags.HorizontalCenter|TextFormatFlags.VerticalCenter);return;}
        using var pen=new Pen(active?.Invoke()==true?ExportDesign.Accent:Tint??Color.FromArgb(110,110,115),1.5f);
        switch(symbol) {
            case "dashboard":g.DrawRectangle(pen,5,5,7,7);g.DrawRectangle(pen,16,5,7,7);g.DrawRectangle(pen,5,16,7,7);g.DrawRectangle(pen,16,16,7,7);break;
            case "minimize":g.DrawLine(pen,7,18,21,18);break;
            case "bell":
            case "inbox":g.DrawArc(pen,8,5,12,16,180,180);g.DrawLines(pen,new Point[]{new(8,13),new(7,20),new(21,20),new(20,13)});g.DrawArc(pen,11,20,6,5,0,180);break;
            case "send":g.DrawPolygon(pen,new Point[]{new(4,5),new(24,14),new(4,23),new(8,14)});g.DrawLine(pen,8,14,23,14);break;
            case "pin":g.DrawPolygon(pen,new Point[]{new(9,4),new(19,4),new(17,12),new(21,17),new(7,17),new(11,12)});g.DrawLine(pen,14,17,14,25);break;
            case "subtitle":g.DrawLine(pen,2,4,15,4);g.DrawLine(pen,2,8,12,8);g.DrawLine(pen,2,12,15,12);break;
            case "metrics":g.DrawRectangle(pen,2,9,3,5);g.DrawRectangle(pen,7,5,3,9);g.DrawRectangle(pen,12,2,3,12);break;
            case "daily":g.DrawEllipse(pen,5,5,7,7);g.DrawLine(pen,8,0,8,3);g.DrawLine(pen,8,14,8,17);g.DrawLine(pen,0,8,3,8);g.DrawLine(pen,14,8,17,8);break;
            case "bankfees":g.DrawPolygon(pen,new Point[]{new(1,5),new(8,1),new(15,5)});foreach(int x in new[]{4,8,12})g.DrawLine(pen,x,7,x,13);g.DrawLine(pen,1,15,15,15);break;
            case "read":if(active?.Invoke()==true){using var brush=new SolidBrush(pen.Color);g.FillEllipse(brush,7,7,14,14);}else g.DrawEllipse(pen,7,7,14,14);break;
            case "close":g.DrawEllipse(pen,4,4,20,20);g.DrawLine(pen,10,10,18,18);g.DrawLine(pen,18,10,10,18);break;
            case "copy":g.DrawRectangle(pen,5,5,13,15);g.DrawLines(pen,new Point[]{new(10,21),new(22,21),new(22,10),new(20,10)});break;
            case "search":g.DrawEllipse(pen,6,5,11,11);g.DrawLine(pen,15,15,22,22);break;
            case "filter":if(active?.Invoke()==true){using var fill=new SolidBrush(Color.FromArgb(220,229,240));using var path=ExportDesign.Round(new RectangleF(1,1,26,26),4);g.FillPath(fill,path);using var dot=new SolidBrush(ExportDesign.Accent);g.FillEllipse(dot,19,3,6,6);}g.DrawLine(pen,8,10,20,10);g.DrawLine(pen,10,14,18,14);g.DrawLine(pen,12,18,16,18);break;
            case "categories":g.DrawPolygon(pen,new Point[]{new(5,5),new(14,5),new(23,14),new(14,23),new(5,14)});g.DrawEllipse(pen,8,8,3,3);break;
            case "refresh":g.DrawArc(pen,6,6,16,16,45,285);g.DrawLines(pen,new Point[]{new(16,5),new(22,6),new(22,12)});break;
            case "settings":
                var teeth=new PointF[64];for(int i=0;i<64;i++){double a=i*Math.PI/32;float r=i%8 is 0 or 1 or 6 or 7?10:8;teeth[i]=new(14+(float)Math.Cos(a)*r,14+(float)Math.Sin(a)*r);}g.DrawPolygon(pen,teeth);g.DrawEllipse(pen,10,10,8,8);break;
            case "all":g.DrawPolygon(pen,new Point[]{new(4,10),new(14,5),new(24,10),new(14,15)});g.DrawLines(pen,new Point[]{new(4,14),new(14,19),new(24,14)});g.DrawLines(pen,new Point[]{new(4,18),new(14,23),new(24,18)});break;
            default:using(var ink=new SolidBrush(pen.Color))foreach(int x in new[]{7,13,19})g.FillEllipse(ink,x,13,3,3);break;
        }
        if(Badge>0){using var badge=new SolidBrush(Color.FromArgb(222,125,43));g.FillEllipse(badge,17,0,11,11);using var font=new Font("Segoe UI",7,FontStyle.Bold,GraphicsUnit.Pixel);TextRenderer.DrawText(g,Math.Min(9,Badge).ToString(),font,new Rectangle(17,0,11,11),Color.White,TextFormatFlags.HorizontalCenter|TextFormatFlags.VerticalCenter|TextFormatFlags.NoPadding);}
        if(Focused&&ShowFocusCues){using var focus=new Pen(pen.Color,1){DashStyle=DashStyle.Dot};g.DrawEllipse(focus,2,2,extent-4,extent-4);}
    }
    protected override void Dispose(bool disposing){if(disposing)tip.Dispose();base.Dispose(disposing);}
}
