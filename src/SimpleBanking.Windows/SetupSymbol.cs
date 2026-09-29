using System.Drawing.Drawing2D;
namespace SimpleBanking;

internal sealed class SetupSymbol(bool euro) : Control
{
    protected override void OnPaint(PaintEventArgs e)
    {
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        int size = euro ? 64 : 40; int radius = euro ? 16 : 12;
        using var path = new GraphicsPath();
        path.AddArc(0,0,radius*2,radius*2,180,90); path.AddArc(size-radius*2,0,radius*2,radius*2,270,90);
        path.AddArc(size-radius*2,size-radius*2,radius*2,radius*2,0,90); path.AddArc(0,size-radius*2,radius*2,radius*2,90,90); path.CloseFigure();
        using var background = new SolidBrush(Color.FromArgb(240,240,242)); e.Graphics.FillPath(background,path);
        if (euro) {
            using var font = new Font("Segoe UI",28,FontStyle.Regular,GraphicsUnit.Pixel);
            TextRenderer.DrawText(e.Graphics,"€",font,new Rectangle(0,0,size,size),Color.FromArgb(29,29,31),TextFormatFlags.HorizontalCenter|TextFormatFlags.VerticalCenter);
        } else {
            using var pen = new Pen(Color.FromArgb(29,29,31),2.5f);
            e.Graphics.DrawArc(pen,15,10,10,14,180,180);
            using var ink = new SolidBrush(Color.FromArgb(29,29,31)); e.Graphics.FillRectangle(ink,12,17,16,13);
        }
    }
}
