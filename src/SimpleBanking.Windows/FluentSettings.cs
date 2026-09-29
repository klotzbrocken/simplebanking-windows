namespace SimpleBanking;
internal sealed partial class SettingsWindow
{
    void BuildAbout(){
        var app=new RoundedCard{Width=550,Padding=new Padding(16)};
        var identity=new Panel{Width=510,Height=88,BackColor=Color.Transparent};
        using var stream=typeof(MainWindow).Assembly.GetManifestResourceStream("SimpleBanking.AppMark.png")!;using var source=Image.FromStream(stream);
        var mark=new PictureBox{Image=new Bitmap(source),SizeMode=PictureBoxSizeMode.Zoom,Bounds=new Rectangle(0,2,48,48)};mark.Disposed+=(_,_)=>mark.Image?.Dispose();identity.Controls.Add(mark);
        var title=new Label{Text="simplebanking",Font=new Font("Segoe UI",20,FontStyle.Bold,GraphicsUnit.Pixel),Bounds=new Rectangle(64,0,400,28),BackColor=Color.Transparent};
        var version=new Label{Text="Version "+typeof(MainWindow).Assembly.GetName().Version?.ToString(3)+" · Windows x64",Font=new Font("Segoe UI",14,GraphicsUnit.Pixel),ForeColor=ExportDesign.Secondary,Bounds=new Rectangle(64,32,400,24),BackColor=Color.Transparent};
        var developer=new Label{Text="Entwickler / Herausgeber: Maik Klotz · © 2026 Maik Klotz",Font=new Font("Segoe UI",12,GraphicsUnit.Pixel),ForeColor=ExportDesign.Secondary,Bounds=new Rectangle(0,64,510,22),BackColor=Color.Transparent};
        identity.Controls.AddRange([title,version,developer]);app.Controls.Add(identity);app.SizeChanged+=(_,_)=>{identity.Width=app.ClientSize.Width-app.Padding.Horizontal;title.Width=version.Width=Math.Max(100,identity.Width-64);developer.Width=identity.Width;};body.Controls.Add(app);
        TextLine("Bankanbindung",true);
        var bank=new RoundedCard{Width=550,Padding=new Padding(16)};
        bank.Controls.Add(new Label{Text="YAXI PSD2 Banking API · Routex Client",AutoSize=true,Font=new Font("Segoe UI",14,GraphicsUnit.Pixel),BackColor=Color.Transparent,Margin=new Padding(0,0,0,6)});
        bank.Controls.Add(new Label{Text="Die Bankanbindung basiert auf der YAXI-Technologie.",AutoSize=true,Font=new Font("Segoe UI",12,GraphicsUnit.Pixel),ForeColor=ExportDesign.Secondary,BackColor=Color.Transparent,Margin=new Padding(0,0,0,12)});
        foreach(var (name,url) in new[]{("yaxi.tech","https://yaxi.tech/"),("simplebanking.de","https://www.simplebanking.de/")}){
            bank.Controls.Add(new Panel{Height=1,Width=510,BackColor=Color.FromArgb(237,237,237),Margin=Padding.Empty});
            var link=ActionControl(name,()=>System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(url){UseShellExecute=true}));link.Borderless=true;link.Chevron=false;link.ExternalLink=true;bank.Controls.Add(link);
        }
        bank.Controls.Add(new Label{Text="Support: support@simplebanking.de",AutoSize=true,BackColor=Color.Transparent,Margin=new Padding(0,12,0,0)});
        bank.SizeChanged+=(_,_)=>{int width=bank.ClientSize.Width-bank.Padding.Horizontal;foreach(Control c in bank.Controls){c.Width=width;if(c is Label label)label.MaximumSize=new Size(width,0);}};body.Controls.Add(bank);
        TextLine("Open Source",true);
        var licenses=new RoundedCard{Width=550};licenses.Controls.Add(new Label{Text="Vorlage: simplebanking für macOS · MIT-Lizenz",AutoSize=true,Font=new Font("Segoe UI",14,GraphicsUnit.Pixel),BackColor=Color.Transparent,Margin=new Padding(0,0,0,6)});
        licenses.Controls.Add(new Label{Text="Lizenztexte liegen im Installationsordner.\nLogos provided by Logo.dev · https://logo.dev",AutoSize=true,Font=new Font("Segoe UI",12,GraphicsUnit.Pixel),ForeColor=ExportDesign.Secondary,BackColor=Color.Transparent});licenses.SizeChanged+=(_,_)=>{foreach(Control c in licenses.Controls)c.MaximumSize=new Size(licenses.ClientSize.Width-licenses.Padding.Horizontal,0);};body.Controls.Add(licenses);
    }
}
