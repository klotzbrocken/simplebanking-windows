namespace SimpleBanking;

internal static class WindowPlacement
{
    // Work-area coordinates can be negative on monitors left of or above the primary display.
    internal static Point Clamp(Rectangle window, Rectangle area) => new(
        Math.Clamp(window.Left, area.Left, Math.Max(area.Left, area.Right-window.Width)),
        Math.Clamp(window.Top, area.Top, Math.Max(area.Top, area.Bottom-window.Height)));

    internal static Point Tray(Point cursor, Size size, Rectangle bounds, Rectangle area, int gap)
    {
        int x=cursor.X-size.Width/2,y=area.Bottom-size.Height-gap;
        if(area.Top>bounds.Top)y=area.Top+gap;
        else if(area.Left>bounds.Left){x=area.Left+gap;y=cursor.Y-size.Height/2;}
        else if(area.Right<bounds.Right){x=area.Right-size.Width-gap;y=cursor.Y-size.Height/2;}
        return Clamp(new Rectangle(x,y,size.Width,size.Height),area);
    }

    internal static Size MainClient(bool wide, int dpi, Size frame, Rectangle area) => new(
        Math.Min((int)Math.Round((wide?840:360)*dpi/96d),Math.Max(1,area.Width-frame.Width)),
        Math.Min((int)Math.Round(688*dpi/96d),Math.Max(1,area.Height-frame.Height)));
}
