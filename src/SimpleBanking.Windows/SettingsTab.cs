using System.Drawing.Drawing2D;
namespace SimpleBanking;

// SettingsPanel.TabButton: 16px symbol, 10px label, 4px gap, 2px selection line.
internal sealed class SettingsTab(int index,string title,Func<bool> selected) : Button
{
    protected override void OnPaint(PaintEventArgs e) {
        base.OnPaintBackground(e);
        bool active=selected();Color[] palette=[Color.FromArgb(30,119,207),Color.FromArgb(22,145,125),Color.FromArgb(105,79,194),Color.FromArgb(208,120,24),Color.FromArgb(41,144,75),Color.FromArgb(208,74,109),Color.FromArgb(108,83,187),Color.FromArgb(44,136,184)];var color=palette[index];
        var g=e.Graphics;g.SmoothingMode=SmoothingMode.AntiAlias;
        float dpi=DeviceDpi/96f;if(active){using var background=new SolidBrush(Color.FromArgb(230,230,230));using var selectedShape=ExportDesign.Round(new RectangleF(0,2,Width,Height-4),4*dpi);g.FillPath(background,selectedShape);using var marker=new SolidBrush(ExportDesign.Accent);using var markerShape=ExportDesign.Round(new RectangleF(0,(Height-16*dpi)/2,3*dpi,16*dpi),1.5f*dpi);g.FillPath(marker,markerShape);}using(var tile=new SolidBrush(Color.FromArgb(28,color)))g.FillEllipse(tile,12*dpi,10*dpi,28*dpi,28*dpi);using var pen=new Pen(color,1.7f);
        var state=g.Save();float scale=DeviceDpi/96f;g.TranslateTransform(18*scale,16*scale);g.ScaleTransform(scale,scale);
        switch(index) {
            case 0:g.DrawEllipse(pen,4,4,8,8);for(int i=0;i<8;i++){double a=i*Math.PI/4;g.DrawLine(pen,8+(float)Math.Cos(a)*6,8+(float)Math.Sin(a)*6,8+(float)Math.Cos(a)*8,8+(float)Math.Sin(a)*8);}break;
            case 1:g.DrawPolygon(pen,new Point[]{new(0,5),new(8,0),new(16,5)});foreach(int x in new[]{3,8,13})g.DrawLine(pen,x,7,x,14);g.DrawLine(pen,0,16,16,16);break;
            case 2:g.DrawArc(pen,0,1,15,15,0,270);g.DrawLine(pen,8,1,8,9);g.DrawLine(pen,8,9,16,9);g.DrawPie(pen,10,0,6,6,270,90);break;
            case 3:g.DrawPolygon(pen,new Point[]{new(3,0),new(3,13),new(7,9),new(10,16),new(13,14),new(10,8),new(15,8)});break;
            case 4:g.DrawPolygon(pen,new Point[]{new(1,3),new(8,0),new(15,3),new(14,10),new(8,16),new(2,10)});g.DrawRectangle(pen,5,7,6,5);g.DrawArc(pen,6,4,4,6,180,180);break;
            case 5:g.DrawLines(pen,new Point[]{new(0,5),new(5,5),new(5,1),new(10,1),new(10,5),new(15,5),new(15,10),new(11,10),new(11,15),new(5,15),new(5,11),new(0,11),new(0,5)});break;
            case 6:g.DrawLines(pen,new Point[]{new(5,0),new(11,0),new(10,1),new(10,6),new(15,14),new(14,16),new(2,16),new(1,14),new(6,6),new(6,1),new(5,0)});g.DrawLine(pen,4,11,12,11);break;
            case 7:g.DrawEllipse(pen,0,0,16,16);g.DrawLine(pen,8,7,8,12);g.DrawEllipse(pen,7.5f,3.5f,1,1);break;
        }
        g.Restore(state);
        using var font=new Font("Segoe UI",14*DeviceDpi/96f,active?FontStyle.Bold:FontStyle.Regular,GraphicsUnit.Pixel);
        TextRenderer.DrawText(g,title,font,new Rectangle((int)(54*dpi),0,Width-(int)(58*dpi),Height),Color.FromArgb(35,42,53),TextFormatFlags.VerticalCenter|TextFormatFlags.NoPrefix|TextFormatFlags.SingleLine);

        if(Focused&&ShowFocusCues)ControlPaint.DrawFocusRectangle(g,new Rectangle(2,2,Width-4,Height-4));
    }
}
