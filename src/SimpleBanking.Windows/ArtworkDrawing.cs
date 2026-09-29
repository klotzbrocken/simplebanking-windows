using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
namespace SimpleBanking;
internal static class ArtworkDrawing
{
    internal static Rectangle Fit(Size image,Rectangle bounds){
        if(image.Width<=0||image.Height<=0||bounds.Width<=0||bounds.Height<=0)return Rectangle.Empty;
        double scale=Math.Min((double)bounds.Width/image.Width,(double)bounds.Height/image.Height);
        int width=Math.Max(1,(int)Math.Round(image.Width*scale)),height=Math.Max(1,(int)Math.Round(image.Height*scale));
        return new(bounds.X+(bounds.Width-width)/2,bounds.Y+(bounds.Height-height)/2,width,height);
    }
    internal static void Draw(Graphics graphics,Image image,Rectangle bounds,float opacity=1){
        var state=graphics.Save();try{
            graphics.InterpolationMode=InterpolationMode.HighQualityBicubic;
            graphics.PixelOffsetMode=PixelOffsetMode.HighQuality;
            graphics.CompositingQuality=CompositingQuality.HighQuality;
            using var attributes=new ImageAttributes();attributes.SetWrapMode(WrapMode.TileFlipXY);
            attributes.SetColorMatrix(new ColorMatrix{Matrix33=opacity});
            graphics.DrawImage(image,Fit(image.Size,bounds),0,0,image.Width,image.Height,GraphicsUnit.Pixel,attributes);
        }finally{graphics.Restore(state);}
    }
}
