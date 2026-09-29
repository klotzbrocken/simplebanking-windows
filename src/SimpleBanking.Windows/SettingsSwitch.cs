using System.Drawing.Drawing2D;
namespace SimpleBanking;

internal sealed class SettingsSwitch : CheckBox
{
    internal SettingsSwitch() { Size=new Size(84,24);SetStyle(ControlStyles.UserPaint|ControlStyles.OptimizedDoubleBuffer|ControlStyles.AllPaintingInWmPaint,true);CheckedChanged+=(_,_)=>Invalidate(); }
    protected override void OnPaint(PaintEventArgs e) {
        base.OnPaintBackground(e);
        var g=e.Graphics;g.ScaleTransform(Width/84f,Height/24f);g.SmoothingMode=SmoothingMode.AntiAlias;
        TextRenderer.DrawText(g,Checked?"Ein":"Aus",Font,new Rectangle(0,0,38,24),ExportDesign.Secondary,TextFormatFlags.VerticalCenter);
        using var path=ExportDesign.Round(new RectangleF(43,2,40,20),10);
        using var track=new SolidBrush(Checked?ExportDesign.Accent:Color.FromArgb(251,251,251));using var border=new Pen(Checked?ExportDesign.Accent:Color.Gray);g.FillPath(track,path);g.DrawPath(border,path);
        using var knob=new SolidBrush(Checked?Color.White:ExportDesign.Secondary);g.FillEllipse(knob,Checked?67:47,6,12,12);
        if(Focused&&ShowFocusCues){using var focus=new Pen(ExportDesign.Accent){DashStyle=DashStyle.Dot};g.DrawPath(focus,path);}
    }
}
