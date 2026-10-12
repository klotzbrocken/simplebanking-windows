using System.Drawing.Text;
namespace SimpleBanking;
internal sealed class ThemePalette
{
    readonly Dictionary<string,string> values;
    readonly bool dark;
    Dictionary<string,byte[]>? images;
    static readonly PrivateFontCollection fonts=LoadFonts();
    static PrivateFontCollection LoadFonts(){var collection=new PrivateFontCollection();var path=Path.Combine(AppContext.BaseDirectory,"Assets","Fonts","VT323-Regular.ttf");if(File.Exists(path))collection.AddFontFile(path);return collection;}
    internal static readonly Dictionary<string,Dictionary<string,string>> BuiltIns=Directory.GetFiles(Path.Combine(AppContext.BaseDirectory,"Assets","Themes"),"*.cfg").Select(path=>Parse(File.ReadAllText(path))).ToDictionary(v=>v["id"]);
    internal static Dictionary<string,string> Parse(string source){var result=new Dictionary<string,string>(StringComparer.OrdinalIgnoreCase);foreach(string line in source.Split('\n')){string clean=line.Trim();if(clean.Length==0||clean.StartsWith('#'))continue;int index=clean.IndexOf('=');if(index>0)result[clean[..index].Trim()]=clean[(index+1)..].Trim();}if(!result.ContainsKey("id")||!result.ContainsKey("name"))throw new InvalidDataException("Theme-ID oder Name fehlt.");return result;}
    ThemePalette(Dictionary<string,string> values,bool dark){this.values=values;this.dark=dark;}
    internal static ThemePalette For(AppPreferences preferences){
        if(preferences.ThemeId=="custom"&&preferences.CustomTheme is {} custom){try{return new(ThemeFiles.Read(ThemeFiles.Write(custom)),preferences.DarkAppearance){images=preferences.CustomThemeImages};}catch(InvalidDataException){}}
        return new(BuiltIns.GetValueOrDefault(preferences.ThemeId)??BuiltIns["default"],preferences.DarkAppearance);
    }
    internal string Id=>values["id"];
    internal string FontIdentity=>Id+"/"+Lofi+"/"+values.GetValueOrDefault("bodyFont")+"/"+values.GetValueOrDefault("headingFont")+"/"+string.Join('/',(images??new()).Where(p=>Path.GetExtension(p.Key).ToLowerInvariant() is ".ttf" or ".otf").Select(p=>Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(p.Value))));
    internal Image? Image(string key){
        string? name=dark?values.GetValueOrDefault(key+"Dark"):null;name??=values.GetValueOrDefault(key);
        if(name==null||images==null)return null;var bytes=images.FirstOrDefault(p=>p.Key.Equals(name,StringComparison.OrdinalIgnoreCase)).Value;if(bytes==null)return null;
        try{using var stream=new MemoryStream(bytes);using var source=System.Drawing.Image.FromStream(stream);return new Bitmap(source);}catch(ArgumentException){return null;}
    }
    internal bool IsDefault=>Id=="default";
    internal bool IsDark=>dark;
    internal bool SquareControls=>values.GetValueOrDefault("squareControls")=="on";
    internal bool GlyphControls=>values.GetValueOrDefault("glyphControls")!="off";
    internal bool DottedLeaders=>values.GetValueOrDefault("dottedLeaders")=="on";
    internal Color Panel=>Read(dark?"panelDark":"panelLight",ColorTranslator.ToHtml(Surface));
    internal bool Lofi=>values.GetValueOrDefault("lofiTypography")=="on";
    internal bool AccountRing=>values.GetValueOrDefault("accountRing")!="off";
    internal bool HasRingImage=>images!=null&&(values.TryGetValue(dark?"ringImageDark":"ringImage",out var name)&&images.ContainsKey(name)||values.TryGetValue("ringImage",out name)&&images.ContainsKey(name));
    internal bool Ripple=>values.GetValueOrDefault("ripple")!="off";
    internal bool MerchantLogos=>values.GetValueOrDefault("merchantLogos")!="off";
    internal bool BankLogos=>values.GetValueOrDefault("bankLogos")!="off";
    internal bool CategoryIcons=>values.GetValueOrDefault("categoryIcons")!="off";
    internal string Text(string text)=>values.GetValueOrDefault("uppercase")=="on"?text.ToUpperInvariant():text;
    Color Read(string key,string fallback)=>ColorTranslator.FromHtml(values.GetValueOrDefault(key)??fallback);
    internal Color Surface=>IsDefault?(dark?Color.FromArgb(23,23,23):Color.FromArgb(248,250,249)):Read(dark?"cardDark":"cardLight","#FFFFFF");
    internal Color Ink=>Read(dark?"inkDark":"inkLight",dark?"#EEEEEE":"#1D1D1F");
    internal Color Secondary=>Blend(Ink,Surface,.65);
    internal Color Accent=>Read("accent","#4E79A7");
    internal Color Positive=>Read(dark?"positiveDark":"positiveLight",values.GetValueOrDefault("positive")??"#4F8A6A");
    internal Color Negative=>Read(dark?"negativeDark":"negativeLight",values.GetValueOrDefault("negative")??"#C65A5A");
    internal Color? Border=>values.ContainsKey("screenBorder")?Read("screenBorder","#e8b200"):null;
    internal Font Font(float size,bool bold=false){var name=values.GetValueOrDefault(bold?"headingFont":"bodyFont");if(name!=null&&images!=null&&images.TryGetValue(name,out var bytes))return ThemeFonts.Create(bytes,size,bold);if(!string.IsNullOrWhiteSpace(name)&&name!="VT323"){var f=new Font(name,size,bold?FontStyle.Bold:FontStyle.Regular,GraphicsUnit.Pixel);if(f.Name.Equals(name,StringComparison.OrdinalIgnoreCase))return f;f.Dispose();}if(Lofi&&fonts.Families.Length>0)return new Font(fonts.Families[0],size*1.22f,FontStyle.Regular,GraphicsUnit.Pixel);return new Font(Id=="gameboy"?"Consolas":"Segoe UI",size,bold?FontStyle.Bold:FontStyle.Regular,GraphicsUnit.Pixel);}
    internal static Color Blend(Color ink,Color background,double alpha)=>Color.FromArgb((int)(ink.R*alpha+background.R*(1-alpha)),(int)(ink.G*alpha+background.G*(1-alpha)),(int)(ink.B*alpha+background.B*(1-alpha)));
}
