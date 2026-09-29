using System.Drawing.Drawing2D;
namespace SimpleBanking;
internal sealed class SubscriptionCard : Control
{
    readonly Subscription item;readonly string currency;readonly Image? logo;readonly ContextMenuStrip menu=new();
    internal SubscriptionCard(Subscription item,string currency,Action<string> change,Action open){
        this.item=item;this.currency=currency;Height=112;Margin=new Padding(0,0,0,10);SetStyle(ControlStyles.UserPaint|ControlStyles.AllPaintingInWmPaint|ControlStyles.OptimizedDoubleBuffer|ControlStyles.StandardDoubleClick|ControlStyles.StandardClick|ControlStyles.ResizeRedraw,true);
        logo=MerchantArtwork.Load(item.Entries[0]);DoubleClick+=(_,_)=>open();AccessibleName=item.Name+" "+UiFormat.Money(item.LastAmount,currency);TabStop=true;
        foreach(string option in new[]{"Abos","Verträge","Sparen","Verbindlichkeiten","Ausschließen"})menu.Items.Add(option=="Ausschließen"?"Kein Abo / Vertrag":option,null,(_,_)=>change(option));
        if(item.Confidence<10)menu.Items.Insert(0,new ToolStripMenuItem("Als echt markieren",null,(_,_)=>change(item.Tab)));
        var edit=DashboardWindow.ActionButton("Ändern",()=>menu.Show(this,new Point(14,99)));edit.Location=new Point(14,70);edit.Height=27;Controls.Add(edit);
        if(item.CancellationUrl!=null&&item.Confidence>=10&&item.Tab!="Verbindlichkeiten"){
            var cancel=DashboardWindow.ActionButton("Kündigen",()=>System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(item.CancellationUrl){UseShellExecute=true}));cancel.Anchor=AnchorStyles.Right|AnchorStyles.Bottom;Controls.Add(cancel);SizeChanged+=(_,_)=>cancel.Location=new Point(Width-cancel.Width-14,70);
        }
        ContextMenuStrip=menu;KeyDown+=(_,e)=>{if(e.KeyCode==Keys.Enter)open();};
    }
    protected override void OnPaint(PaintEventArgs e){
        var g=e.Graphics;g.SmoothingMode=SmoothingMode.AntiAlias;g.Clear(Parent?.BackColor??Color.FromArgb(248,250,249));
        using var path=new GraphicsPath();float d=20,w=Width-1,h=Height-1;path.AddArc(0,0,d,d,180,90);path.AddArc(w-d,0,d,d,270,90);path.AddArc(w-d,h-d,d,d,0,90);path.AddArc(0,h-d,d,d,90,90);path.CloseFigure();g.FillPath(Brushes.White,path);
        if(logo!=null)ArtworkDrawing.Draw(g,logo,new Rectangle(14,14,34,34));else BookingRow.DrawCategory(g,new Point(20,20),"Abos & Digital",Color.DimGray);
        var ink=item.Confidence>=10?Color.FromArgb(29,29,31):Color.DimGray;using var font=new Font("Segoe UI",15,FontStyle.Bold,GraphicsUnit.Pixel);using var small=new Font("Segoe UI",12,GraphicsUnit.Pixel);
        TextRenderer.DrawText(g,item.Name,font,new Rectangle(58,14,Math.Max(30,Width-230),22),ink,TextFormatFlags.EndEllipsis|TextFormatFlags.NoPrefix);
        TextRenderer.DrawText(g,"Zuletzt: "+item.LastDate.ToString("dd.MM.yyyy")+(item.Cadence==PaymentFrequency.Monthly?" · Monatlich":""),small,new Point(58,37),Color.DimGray,TextFormatFlags.NoPrefix);
        TextRenderer.DrawText(g,UiFormat.Money(item.LastAmount,currency),font,new Rectangle(Width-164,14,150,22),ink,TextFormatFlags.Right|TextFormatFlags.NoPrefix);
        TextRenderer.DrawText(g,item.Entries.Count+"×",small,new Rectangle(Width-164,37,150,20),Color.DimGray,TextFormatFlags.Right);
        if(Focused)ControlPaint.DrawFocusRectangle(g,new Rectangle(4,4,Width-8,Height-8));
    }
    protected override void Dispose(bool disposing){if(disposing){logo?.Dispose();menu.Dispose();}base.Dispose(disposing);}
}
