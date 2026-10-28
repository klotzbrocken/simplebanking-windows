using System.Text.Json;
namespace SimpleBanking;
internal static class ThemeLibraryTests
{
    static void Check(bool value){if(!value)throw new InvalidOperationException("Theme collection regression.");}
    internal static void Register(Action<string,Action> test){
        ThemePackage Package()=>new(new(){["id"]="same",["name"]="Mein Theme",["accent"]="#123456"},new());
        test("Theme library retains legacy themes and distinct imports across serialization",()=>{
            var prefs=new AppPreferences{ThemeId="custom",CustomTheme=Package().Values,CustomThemeImages=new(){["fixture"]=[1,2,3]}};
            ThemeLibrary.Import(prefs,Package());string first=prefs.ThemeId;ThemeLibrary.Import(prefs,Package());
            Check(prefs.ImportedThemes.Count==3&&prefs.CustomTheme==null&&first!=prefs.ThemeId);
            Check(prefs.ImportedThemes.Values.Select(p=>p.Values["name"]).Distinct().Count()==3);
            var restored=JsonSerializer.Deserialize<AppPreferences>(JsonSerializer.Serialize(prefs))!;
            Check(restored.ImportedThemes.Count==3&&ThemeLibrary.Current(restored)!.Values["name"]=="Mein Theme (3)");
            Check(restored.ImportedThemes.Values.Any(p=>p.Images.GetValueOrDefault("fixture")?.SequenceEqual(new byte[]{1,2,3})==true));
        });
        test("Theme removal protects originals and failed saving restores the collection",()=>{
            var prefs=new AppPreferences();ThemeLibrary.Import(prefs,Package());string id=prefs.ThemeId;
            try{ThemeLibrary.Change(prefs,()=>ThemeLibrary.Remove(prefs,id),()=>throw new IOException("fixture"));}catch(IOException){}
            Check(prefs.ThemeId==id&&prefs.ImportedThemes.Count==1);
            ThemeLibrary.Remove(prefs,id);Check(prefs.ThemeId=="default"&&prefs.ImportedThemes.Count==0);
            bool blocked=false;try{ThemeLibrary.Remove(prefs,"default");}catch(InvalidOperationException){blocked=true;}Check(blocked);
        });
        test("Theme collection limits reject without replacing an existing selection",()=>{
            var prefs=new AppPreferences();for(int i=0;i<20;i++)ThemeLibrary.Import(prefs,Package());string id=prefs.ThemeId;
            bool blocked=false;try{ThemeLibrary.Import(prefs,Package());}catch(InvalidDataException){blocked=true;}Check(blocked&&prefs.ImportedThemes.Count==20&&prefs.ThemeId==id);
            Check(ThemeLibrary.Choices(prefs).Count==ThemePalette.BuiltIns.Count+20);
        });
    }
}
