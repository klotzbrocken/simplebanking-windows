using System.Drawing.Drawing2D;
namespace SimpleBanking;
internal sealed class FluentButton : Button
{
    readonly bool primary;
    bool hover,pressed;
    internal bool Borderless;
    internal bool Chevron;
    internal bool ExternalLink;
    internal FluentButton(bool primary=false){this.primary=primary;SetStyle(ControlStyles.UserPaint|ControlStyles.OptimizedDoubleBuffer|ControlStyles.SupportsTransparentBackColor,true);FlatStyle=FlatStyle.Flat;FlatAppearance.BorderSize=0;BackColor=Color.Transparent;UseVisualStyleBackColor=false;}
    protected override void OnMouseEnter(EventArgs e){hover=true;Invalidate();base.OnMouseEnter(e);}
    protected override void OnMouseLeave(EventArgs e){hover=pressed=false;Invalidate();base.OnMouseLeave(e);}
    protected override void OnMouseDown(MouseEventArgs e){pressed=true;Invalidate();base.OnMouseDown(e);}
    protected override void OnMouseUp(MouseEventArgs e){pressed=false;Invalidate();base.OnMouseUp(e);}
    protected override void OnPaint(PaintEventArgs e){
        base.OnPaintBackground(e);var g=e.Graphics;g.SmoothingMode=SmoothingMode.AntiAlias;float scale=DeviceDpi/96f;
        using var shape=ExportDesign.Round(new RectangleF(.5f,.5f,Width-1,Height-1),4*scale);
        Color background=primary?(pressed?Color.FromArgb(61,101,145):hover?Color.FromArgb(69,110,154):ExportDesign.Accent):(hover?Color.FromArgb(243,243,243):Color.White);
        using var fill=new SolidBrush(background);using var border=new Pen(primary?Color.FromArgb(61,101,145):Color.FromArgb(225,225,225));g.FillPath(fill,shape);if(!Borderless)g.DrawPath(border,shape);
        Color ink=!Enabled?Color.Gray:primary?Color.White:ExportDesign.Text;
        var bounds=Rectangle.Inflate(ClientRectangle,-(int)(12*scale),0);bool left=TextAlign==ContentAlignment.MiddleLeft;
        if(Image!=null){int size=(int)(20*scale);int textWidth=TextRenderer.MeasureText(Text,Font).Width;int x=left?bounds.Left:Math.Max(bounds.Left,(Width-size-(int)(10*scale)-textWidth)/2);g.DrawImage(Image,new Rectangle(x,(Height-size)/2,size,size));bounds.X=x+size+(int)(10*scale);bounds.Width=Width-bounds.X-(int)(12*scale);left=true;}
        if(Chevron||ExternalLink){bounds.Width-=(int)(24*scale);using var pen=new Pen(ExportDesign.Secondary,1.3f*scale);float x=Width-22*scale,y=Height/2f;if(ExternalLink){g.DrawLines(pen,new PointF[]{new(x-5*scale,y-2*scale),new(x-5*scale,y+6*scale),new(x+3*scale,y+6*scale),new(x+3*scale,y+2*scale)});g.DrawLines(pen,new PointF[]{new(x-1*scale,y-6*scale),new(x+6*scale,y-6*scale),new(x+6*scale,y+1*scale)});g.DrawLine(pen,x-1*scale,y+1*scale,x+6*scale,y-6*scale);}else g.DrawLines(pen,new PointF[]{new(x,y-4*scale),new(x+4*scale,y),new(x,y+4*scale)});}
        TextRenderer.DrawText(g,Text,Font,bounds,ink,TextFormatFlags.VerticalCenter|TextFormatFlags.SingleLine|TextFormatFlags.EndEllipsis|TextFormatFlags.NoPrefix|(left?TextFormatFlags.Left:TextFormatFlags.HorizontalCenter));
        if(Focused&&ShowFocusCues){using var focus=new Pen(ink){DashStyle=DashStyle.Dot};using var inset=ExportDesign.Round(new RectangleF(3,3,Width-6,Height-6),3*scale);g.DrawPath(focus,inset);}
    }
}
internal sealed class EmptyBookings : Control
{
    readonly Image? icon=ExportDesign.Icon("filter_20_regular-8a8a8a");
    internal EmptyBookings(){Name="EmptyBookings";AccessibleName="Keine Umsätze. Ändere die Filter oder den Suchbegriff.";SetStyle(ControlStyles.SupportsTransparentBackColor,true);BackColor=Color.Transparent;}
    protected override void OnPaint(PaintEventArgs e){float scale=DeviceDpi/96f;int y=(int)(22*scale);if(icon!=null)e.Graphics.DrawImage(icon,new Rectangle((Width-(int)(28*scale))/2,y,(int)(28*scale),(int)(28*scale)));using var title=new Font("Segoe UI",14*scale,FontStyle.Bold,GraphicsUnit.Pixel);using var help=new Font("Segoe UI",12*scale,GraphicsUnit.Pixel);TextRenderer.DrawText(e.Graphics,"Keine Umsätze",title,new Rectangle(12,y+(int)(40*scale),Width-24,(int)(24*scale)),ExportDesign.Text,TextFormatFlags.HorizontalCenter);TextRenderer.DrawText(e.Graphics,"Ändere die Filter oder den Suchbegriff.",help,new Rectangle(12,y+(int)(67*scale),Width-24,(int)(44*scale)),ExportDesign.Secondary,TextFormatFlags.HorizontalCenter|TextFormatFlags.WordBreak);}
    protected override void Dispose(bool disposing){if(disposing)icon?.Dispose();base.Dispose(disposing);}
}
internal sealed class KeyCaps : Control
{
    readonly string[] keys;
    internal KeyCaps(params string[] keys){this.keys=keys;Size=new Size(150,32);AccessibleName=string.Join(" + ",keys);SetStyle(ControlStyles.SupportsTransparentBackColor,true);BackColor=Color.Transparent;}
    protected override void OnPaint(PaintEventArgs e){var g=e.Graphics;g.SmoothingMode=SmoothingMode.AntiAlias;float scale=DeviceDpi/96f;float x=0;using var font=new Font("Segoe UI",12*scale,FontStyle.Bold,GraphicsUnit.Pixel);foreach(var key in keys){float width=TextRenderer.MeasureText(key,font).Width+14*scale;using var p=ExportDesign.Round(new RectangleF(x,.5f,width,27*scale),3*scale);g.FillPath(Brushes.White,p);using var pen=new Pen(Color.LightGray);g.DrawPath(pen,p);TextRenderer.DrawText(g,key,font,Rectangle.Round(new RectangleF(x,0,width,27*scale)),ExportDesign.Text,TextFormatFlags.HorizontalCenter|TextFormatFlags.VerticalCenter);x+=width+5*scale;}}
}
