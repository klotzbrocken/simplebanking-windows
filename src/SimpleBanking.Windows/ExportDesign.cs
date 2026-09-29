using System.Drawing.Drawing2D;
namespace SimpleBanking;
internal static class ExportDesign
{
    internal static readonly Color Accent=Color.FromArgb(78,121,167), Text=Color.FromArgb(27,27,27), Secondary=Color.FromArgb(94,94,94), Surface=Color.FromArgb(243,243,243), Error=Color.FromArgb(196,43,28);
    internal static Image? Icon(string name) {
        var assembly=typeof(ExportDesign).Assembly;var resource=assembly.GetManifestResourceNames().FirstOrDefault(n=>n.EndsWith(".Fluent."+name+".png",StringComparison.Ordinal));
        if(resource==null)return null;using var stream=assembly.GetManifestResourceStream(resource)!;using var source=Image.FromStream(stream);return new Bitmap(source,new Size(20,20));
    }
    internal static GraphicsPath Round(RectangleF r,float radius) {var p=new GraphicsPath();float d=radius*2;p.AddArc(r.X,r.Y,d,d,180,90);p.AddArc(r.Right-d,r.Y,d,d,270,90);p.AddArc(r.Right-d,r.Bottom-d,d,d,0,90);p.AddArc(r.X,r.Bottom-d,d,d,90,90);p.CloseFigure();return p;}
}
internal sealed class FilterPill : Button
{
    [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)] internal bool Selected {get;set;}
    internal FilterPill(string title) {Text=title;AccessibleName=title;Font=new Font("Segoe UI",14,GraphicsUnit.Pixel);Size=new Size(TextRenderer.MeasureText(title,Font,Size.Empty,TextFormatFlags.NoPadding).Width+20,28);Margin=new Padding(0,0,6,6);FlatStyle=FlatStyle.Flat;FlatAppearance.BorderSize=0;SetStyle(ControlStyles.UserPaint|ControlStyles.OptimizedDoubleBuffer|ControlStyles.SupportsTransparentBackColor,true);BackColor=Color.Transparent;}
    protected override void OnPaint(PaintEventArgs e) {base.OnPaintBackground(e);e.Graphics.SmoothingMode=SmoothingMode.AntiAlias;using var path=ExportDesign.Round(new RectangleF(.5f,.5f,Width-1,Height-1),Height/2f-1);using var fill=new SolidBrush(Selected?ExportDesign.Accent:Color.FromArgb(250,252,251));using var border=new Pen(Selected?ExportDesign.Accent:Color.FromArgb(218,222,220));e.Graphics.FillPath(fill,path);e.Graphics.DrawPath(border,path);TextRenderer.DrawText(e.Graphics,Text,Font,ClientRectangle,Selected?Color.White:ExportDesign.Text,TextFormatFlags.HorizontalCenter|TextFormatFlags.VerticalCenter);if(Focused&&ShowFocusCues){using var pen=new Pen(Selected?Color.White:ExportDesign.Accent){DashStyle=DashStyle.Dot};e.Graphics.DrawPath(pen,path);}}
}
internal sealed class UnlockDialog : Form
{
    readonly TextBox password=new(){Name="UnlockPassword",UseSystemPasswordChar=true,BorderStyle=BorderStyle.None,PlaceholderText="Passwort eingeben …"};
    readonly Label error=new(){Name="UnlockError",ForeColor=ExportDesign.Error};
    readonly CheckBox enroll=new(){Name="EnrollHello",Text="Künftig mit Windows Hello entsperren",Checked=true,AutoSize=true};
    readonly Panel field=new(){BackColor=Color.White};
    readonly Button submit;
    readonly Func<string,bool,string?> unlock;
    internal bool ResetRequested {get;private set;}
    internal UnlockDialog(bool helloEnabled,Func<string,bool,string?> unlock,Func<string?> hello)
    {
        this.unlock=unlock;Text="simplebanking entsperren";Name="UnlockDialog";AutoScaleMode=AutoScaleMode.Dpi;AutoScaleDimensions=new SizeF(96,96);ClientSize=new Size(400,helloEnabled?372:354);Font=new Font("Segoe UI",14,GraphicsUnit.Pixel);BackColor=ExportDesign.Surface;ForeColor=ExportDesign.Text;FormBorderStyle=FormBorderStyle.FixedDialog;MaximizeBox=false;MinimizeBox=false;ShowInTaskbar=false;StartPosition=FormStartPosition.CenterParent;
        Icon=AppArtwork.CreateTrayIcon();Disposed+=(_,_)=>Icon.Dispose();
        var mark=new PictureBox{Location=new Point(24,24),Size=new Size(44,44),SizeMode=PictureBoxSizeMode.Zoom,Image=LoadMark()};Disposed+=(_,_)=>mark.Image?.Dispose();Controls.Add(mark);
        Controls.Add(new Label{Text="Entsperren",Location=new Point(82,18),Size=new Size(294,30),Font=new Font("Segoe UI",20,FontStyle.Bold,GraphicsUnit.Pixel)});
        Controls.Add(new Label{Text="Gib dein Master-Passwort ein, um\nsimplebanking zu entsperren.",ForeColor=ExportDesign.Secondary,Location=new Point(82,49),Size=new Size(294,45)});
        int y=112;
        if(helloEnabled){var button=Button("Mit Windows Hello entsperren",new Rectangle(24,y,352,40),false);button.Name="UnlockHello";button.Image=ExportDesign.Icon("fingerprint_20_regular-4e79a7");button.ImageAlign=ContentAlignment.MiddleLeft;button.TextImageRelation=TextImageRelation.ImageBeforeText;Disposed+=(_,_)=>button.Image?.Dispose();button.Click+=(_,_)=>{SetError(hello());if(error.Text.Length==0)DialogResult=DialogResult.OK;};Controls.Add(button);Controls.Add(new Label{Text="──────   oder mit Passwort   ──────",ForeColor=ExportDesign.Secondary,TextAlign=ContentAlignment.MiddleCenter,Location=new Point(24,y+53),Size=new Size(352,24)});y+=92;}
        Controls.Add(new Label{Text="Master-Passwort",Location=new Point(24,y),Size=new Size(352,24)});y+=28;
        field.SetBounds(24,y,352,34);password.SetBounds(10,8,304,22);field.Controls.Add(password);
        var eye=Button("",new Rectangle(318,1,32,32),false);((FluentButton)eye).Borderless=true;eye.Name="RevealPassword";eye.AccessibleName="Passwort anzeigen";eye.FlatAppearance.BorderSize=0;eye.Image=ExportDesign.Icon("eye_20_regular-5e5e5e");eye.Click+=(_,_)=>{password.UseSystemPasswordChar=!password.UseSystemPasswordChar;eye.AccessibleName=password.UseSystemPasswordChar?"Passwort anzeigen":"Passwort verbergen";var previous=eye.Image;eye.Image=ExportDesign.Icon(password.UseSystemPasswordChar?"eye_20_regular-5e5e5e":"eye_off_20_regular-5e5e5e");previous?.Dispose();password.Focus();};Disposed+=(_,_)=>eye.Image?.Dispose();field.Controls.Add(eye);field.Paint+=(_,e)=>{using var pen=new Pen(error.Text.Length>0?ExportDesign.Error:password.Focused?ExportDesign.Accent:Color.Gray,error.Text.Length>0||password.Focused?2:1);e.Graphics.DrawLine(pen,0,field.Height-1,field.Width,field.Height-1);};password.Enter+=(_,_)=>field.Invalidate();password.Leave+=(_,_)=>field.Invalidate();password.TextChanged+=(_,_)=>SetError(null);Controls.Add(field);
        y+=38;error.SetBounds(24,y,352,24);error.Font=new Font("Segoe UI",12,GraphicsUnit.Pixel);Controls.Add(error);
        if(!helloEnabled){enroll.Location=new Point(24,y+29);Controls.Add(enroll);Controls.Add(new Label{Text="Windows Hello wird nach dem Entsperren eingerichtet —\ndu wirst einmal gefragt.",Font=new Font("Segoe UI",12,GraphicsUnit.Pixel),ForeColor=ExportDesign.Secondary,Location=new Point(24,y+56),Size=new Size(352,36)});}
        var footer=new Panel{Dock=DockStyle.Bottom,Height=72,BackColor=Color.FromArgb(251,251,251)};var reset=new LinkLabel{Text="Zurücksetzen …",LinkColor=ExportDesign.Accent,ActiveLinkColor=ExportDesign.Accent,LinkBehavior=LinkBehavior.HoverUnderline,AutoSize=true,Location=new Point(24,29)};reset.LinkClicked+=(_,_)=>{if(MessageBox.Show(this,"Der lokale Tresor wird zurückgesetzt. Konten müssen danach neu eingerichtet werden. Eine verschlüsselte Kopie bleibt zur Wiederherstellung erhalten; dafür ist weiterhin das bisherige Passwort erforderlich.\n\nJetzt zurücksetzen?","simplebanking zurücksetzen",MessageBoxButtons.YesNo,MessageBoxIcon.Warning,MessageBoxDefaultButton.Button2)==DialogResult.Yes){ResetRequested=true;DialogResult=DialogResult.Retry;}};
        var cancel=Button("Abbrechen",new Rectangle(164,20,102,32),false);cancel.DialogResult=DialogResult.Cancel;submit=Button("Entsperren",new Rectangle(274,20,102,32),true);submit.Name="SubmitUnlock";submit.Click+=(_,_)=>Submit();footer.Controls.AddRange([reset,cancel,submit]);Controls.Add(footer);AcceptButton=submit;CancelButton=cancel;Shown+=(_,_)=>password.Focus();
    }
    static Image LoadMark(){using var stream=typeof(UnlockDialog).Assembly.GetManifestResourceStream("SimpleBanking.AppMark.png")!;using var image=Image.FromStream(stream);return new Bitmap(image);}
    internal void SetError(string? message){error.Text=message??"";field.Invalidate();}
    void Submit(){if(password.Text.Length==0){SetError("Bitte gib dein Master-Passwort ein.");password.Focus();return;}submit.Enabled=false;try{SetError(unlock(password.Text,enroll.Visible&&enroll.Checked));if(error.Text.Length==0)DialogResult=DialogResult.OK;else{password.SelectAll();password.Focus();}}finally{submit.Enabled=true;}}
    static Button Button(string text,Rectangle bounds,bool primary)=>new FluentButton(primary){Text=text,Bounds=bounds,ForeColor=primary?Color.White:ExportDesign.Text};
}
