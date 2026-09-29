using System.Drawing.Drawing2D;
namespace SimpleBanking;
internal sealed class CalendarDay : Button
{
    readonly int day;
    readonly string amount;
    readonly List<Image?> logos=[];
    readonly string[] names;
    readonly ToolTip tip=new();
    internal CalendarDay(int day,Entry[] entries,string currency,Action open){
        this.day=day;amount=entries.Length==0?"":UiFormat.Money(-entries.Sum(e=>e.Amount),currency);
        var merchants=entries.DistinctBy(e=>e.Merchant).ToArray();names=merchants.Select(e=>e.Merchant).ToArray();
        foreach(var entry in merchants.Take(3))logos.Add(MerchantArtwork.Load(entry));
        AccessibleName=day+" · "+amount+" · "+string.Join(", ",names);tip.SetToolTip(this,AccessibleName);FlatStyle=FlatStyle.Flat;FlatAppearance.BorderSize=0;Margin=new Padding(3);Click+=(_,_)=>open();
        SetStyle(ControlStyles.UserPaint|ControlStyles.OptimizedDoubleBuffer|ControlStyles.AllPaintingInWmPaint,true);
    }
    protected override void OnPaint(PaintEventArgs e){
        var g=e.Graphics;g.Clear(BackColor);g.SmoothingMode=SmoothingMode.AntiAlias;float scale=DeviceDpi/96f;
        using var font=new Font("Segoe UI",12*scale,FontStyle.Regular,GraphicsUnit.Pixel);using var bold=new Font("Segoe UI",12*scale,FontStyle.Bold,GraphicsUnit.Pixel);
        TextRenderer.DrawText(g,day.ToString(),font,new Point((int)(7*scale),(int)(4*scale)),Color.FromArgb(65,63,67));
        int size=(int)(20*scale),y=(int)(25*scale);for(int i=0;i<logos.Count;i++){int x=(int)(7*scale)+i*(size+3);if(x+size>Width-4)break;if(logos[i] is {} logo)ArtworkDrawing.Draw(g,logo,new Rectangle(x,y,size,size));else{using var brush=new SolidBrush(Color.White);g.FillEllipse(brush,x,y,size,size);TextRenderer.DrawText(g,names[i].Length>0?names[i][..1]:"?",font,new Rectangle(x,y,size,size),Color.DimGray,TextFormatFlags.HorizontalCenter|TextFormatFlags.VerticalCenter);}}
        TextRenderer.DrawText(g,amount,font,new Rectangle((int)(24*scale),(int)(4*scale),Math.Max(1,Width-(int)(28*scale)),(int)(18*scale)),Color.FromArgb(96,48,48),TextFormatFlags.EndEllipsis|TextFormatFlags.SingleLine|TextFormatFlags.Right|TextFormatFlags.NoPadding);
        if(Focused)ControlPaint.DrawFocusRectangle(g,ClientRectangle);
    }
    protected override void Dispose(bool disposing){if(disposing){foreach(var logo in logos)logo?.Dispose();tip.Dispose();}base.Dispose(disposing);}
}
