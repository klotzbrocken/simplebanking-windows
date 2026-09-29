namespace SimpleBanking;
internal sealed class ThemePreview : Form
{
    internal ThemePreview(ThemePackage package){
        Text="Theme-Vorschau · "+package.Values["name"];ClientSize=new Size(620,310);FormBorderStyle=FormBorderStyle.FixedDialog;MaximizeBox=false;MinimizeBox=false;StartPosition=FormStartPosition.CenterParent;
        Controls.Add(new Label{Text=package.Values["name"]+" · Hell und Dunkel",Bounds=new Rectangle(16,14,580,25),Font=new Font("Segoe UI",15,FontStyle.Bold,GraphicsUnit.Pixel)});
        for(int i=0;i<2;i++)Controls.Add(new Sample(ThemePalette.For(new AppPreferences{ThemeId="custom",CustomTheme=package.Values,CustomThemeImages=package.Images,DarkAppearance=i==1})){Bounds=new Rectangle(16+i*302,48,286,192)});
        var cancel=new Button{Text="Abbrechen",DialogResult=DialogResult.Cancel,Bounds=new Rectangle(384,264,100,30)};var apply=new Button{Text="Übernehmen",DialogResult=DialogResult.OK,Bounds=new Rectangle(492,264,112,30)};Controls.Add(cancel);Controls.Add(apply);CancelButton=cancel;AcceptButton=apply;
    }
    sealed class Sample(ThemePalette theme):Control
    {
        protected override void OnPaint(PaintEventArgs e){
            var g=e.Graphics;g.Clear(theme.Surface);using var wallpaper=theme.Image("wallpaperFlyout")??theme.Image("wallpaper");if(wallpaper!=null)g.DrawImage(wallpaper,ClientRectangle);
            using var name=theme.Font(13,true);using var amount=theme.Font(28,true);using var small=theme.Font(12);using var ink=new SolidBrush(theme.Ink);using var positive=new SolidBrush(theme.IsDefault?theme.Positive:theme.Ink);using var logo=theme.Image("logo");
            if(logo!=null)ArtworkDrawing.Draw(g,logo,new Rectangle(14,14,22,22));g.DrawString(theme.Text("Girokonto · Vorschau"),name,ink,logo==null?14:42,16);
            g.DrawString("3.284,52 €",amount,positive,14,56);g.DrawString(theme.Text("Einkauf"),name,ink,14,126);g.DrawString("−24,90 €",small,ink,206,128);
            using var ring=theme.Image("ringImage");if(ring!=null)ArtworkDrawing.Draw(g,ring,new Rectangle(218,48,54,54));
        }
    }
}
