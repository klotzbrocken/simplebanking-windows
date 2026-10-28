namespace SimpleBanking;
internal static class ThemeFiles
{
    static readonly string[] colors=["accent","positive","negative","positiveLight","positiveDark","negativeLight","negativeDark","cardLight","cardDark","panelLight","panelDark","inkLight","inkDark","screenBorder"];
    static readonly string[] switches=["lofiTypography","accountRing","ripple","merchantLogos","bankLogos","categoryIcons","uppercase","dottedLeaders","squareControls","glyphControls"];
    internal static Dictionary<string,string> Read(string text){
        if(text.Length>65536)throw new InvalidDataException("Die Theme-Datei ist zu groß.");
        var values=ThemePalette.Parse(text);
        if(string.IsNullOrWhiteSpace(values["id"])||string.IsNullOrWhiteSpace(values["name"])||values["name"].Length>80)throw new InvalidDataException("Das Theme benötigt einen Namen und eine ID.");
        foreach(var key in colors)if(values.TryGetValue(key,out var value)){if(!System.Text.RegularExpressions.Regex.IsMatch(value,"^#[0-9a-fA-F]{6}$"))throw new InvalidDataException("Ungültige Farbe bei „"+key+"“. Erwartet wird #RRGGBB.");}
        foreach(var key in switches)if(values.TryGetValue(key,out var value)&&value is not ("on" or "off"))throw new InvalidDataException("Ungültiger Schalter bei „"+key+"“.");
        return values;
    }
    internal static string Write(Dictionary<string,string> values)=>"# simplebanking Farbtheme\n"+string.Join("\n",values.OrderBy(p=>p.Key).Select(p=>p.Key+"="+p.Value))+"\n";
}
internal sealed partial class SettingsWindow
{
    void ThemeTools(){var row=new FlowLayoutPanel{Height=38,WrapContents=false,Margin=new Padding(0,4,0,8)};row.Controls.Add(DashboardWindow.ActionButton("Importieren …",ImportTheme));row.Controls.Add(DashboardWindow.ActionButton("Exportieren …",ExportTheme));row.Controls.Add(DashboardWindow.ActionButton("Verwalten …",ManageThemes));row.Controls.Add(DashboardWindow.ActionButton("Standard",()=>ApplyTheme(null)));body.Controls.Add(row);}
    void ApplyTheme(ThemePackage? package){try{ThemeLibrary.Change(prefs,()=>{if(package==null)prefs.ThemeId="default";else ThemeLibrary.Import(prefs,package);},Save);ShowTab(3);}catch{owner.ApplyPreferences();throw;}}
    void ImportTheme(){
        using var dialog=new OpenFileDialog{Filter="simplebanking Theme|*.cfg;*.zip",Title="Theme importieren"};if(dialog.ShowDialog(this)!=DialogResult.OK)return;
        try{var package=ThemePackage.Read(dialog.FileName);using var preview=new ThemePreview(package);if(preview.ShowDialog(this)==DialogResult.OK)ApplyTheme(package);}catch(Exception e) when(e is IOException or InvalidDataException or UnauthorizedAccessException or ArgumentException or OutOfMemoryException or System.Xml.XmlException or InvalidOperationException or System.Runtime.InteropServices.ExternalException){MessageBox.Show(this,e.Message,"Theme konnte nicht importiert werden");}
    }
    void ExportTheme(){
        using var dialog=new SaveFileDialog{Filter="simplebanking Theme-Paket|*.zip|Farbtheme ohne Grafiken|*.cfg",FileName="simplebanking-theme.zip"};if(dialog.ShowDialog(this)!=DialogResult.OK)return;
        try{var package=ThemeLibrary.Current(prefs);var values=package?.Values??ThemePalette.BuiltIns.GetValueOrDefault(prefs.ThemeId)??ThemePalette.BuiltIns["default"];if(dialog.FilterIndex==2){if(package?.Images.Count>0)throw new IOException("Für ein Theme mit Grafiken oder Schriften bitte ZIP als Dateityp auswählen.");File.WriteAllText(dialog.FileName,ThemeFiles.Write(values));}else new ThemePackage(values,package?.Images??new()).Write(dialog.FileName);}catch(Exception e) when(e is IOException or UnauthorizedAccessException){MessageBox.Show(this,e.Message,"Theme konnte nicht exportiert werden");}
    }
}
