namespace SimpleBanking;
internal static class BtxMosaic
{
    internal static void Draw(Graphics graphics,Rectangle bounds,string category){
        var (rows,color)=category switch{
            "Essen & Alltag" or "Lebensmittel"=>(new[]{"x.x","xxx","x.x"},"#c00000"),
            "Gastronomie"=>(new[]{"x.x","xxx",".x."},"#8a5a1a"),
            "Mobilität"=>(new[]{"xx.","xxx","xx."},"#0033b0"),
            "Shopping"=>(new[]{".x.","xxx","xxx"},"#0a7a24"),
            "Gesundheit"=>(new[]{".x.","xxx","xxx"},"#a00050"),
            "Freizeit"=>(new[]{".x.","xxx","xxx"},"#c81ec8"),
            "Gehalt" or "Einkommen"=>(new[]{"xxx","x.x","xxx"},"#0a7a24"),
            _=>(new[]{"xxx","x.x","xxx"},"#0033b0")};
        using var fill=new SolidBrush(ColorTranslator.FromHtml(color));float w=bounds.Width/3f,h=bounds.Height/3f;
        for(int y=0;y<3;y++)for(int x=0;x<3;x++)if(rows[y][x]=='x')graphics.FillRectangle(fill,bounds.X+x*w,bounds.Y+y*h,w-1,h-1);
    }
}
