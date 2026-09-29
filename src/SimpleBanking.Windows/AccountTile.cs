using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
namespace SimpleBanking;
internal sealed class AccountTile : Button
{
    [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
    internal bool Active { get; init; }
    [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
    internal Color Accent { get; init; }
    [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
    internal Image? Logo { get; init; }
    public AccountTile() { SetStyle(ControlStyles.SupportsTransparentBackColor | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer, true); FlatStyle = FlatStyle.Flat; FlatAppearance.BorderSize = 0; }
    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics; g.SmoothingMode = SmoothingMode.AntiAlias; base.OnPaintBackground(e);
        if (!Active) { using var fill = new SolidBrush(Color.FromArgb(13, Color.Black)); g.FillEllipse(fill, ClientRectangle); }
        int size = Active ? 20 : 16, x = (Width - size) / 2, y = (Height - size) / 2;
        if (Logo != null) {
            using var attrs = new ImageAttributes(); attrs.SetColorMatrix(new ColorMatrix { Matrix33 = Active ? 1f : .42f });
            ArtworkDrawing.Draw(g,Logo,new Rectangle(x,y,size,size),Active?1f:.42f);
        } else {
            var r = new Rectangle(x,y,size,size); using var path = new GraphicsPath(); int d = Math.Max(2,(int)(size * .56));
            path.AddArc(r.Left,r.Top,d,d,180,90); path.AddArc(r.Right-d,r.Top,d,d,270,90); path.AddArc(r.Right-d,r.Bottom-d,d,d,0,90); path.AddArc(r.Left,r.Bottom-d,d,d,90,90); path.CloseFigure();
            using var fill = new SolidBrush(Active ? Accent : Color.FromArgb(107,Accent)); g.FillPath(fill,path);
            TextRenderer.DrawText(g,Text,Font,r,Color.White,TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
        }
        if (Focused && ShowFocusCues) ControlPaint.DrawFocusRectangle(g,ClientRectangle);
    }
    protected override void Dispose(bool disposing) { if (disposing) Logo?.Dispose(); base.Dispose(disposing); }
}
