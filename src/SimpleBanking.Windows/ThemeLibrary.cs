namespace SimpleBanking;

internal static class ThemeLibrary
{
    internal static ThemePackage? Current(AppPreferences prefs)=>prefs.ImportedThemes.GetValueOrDefault(prefs.ThemeId)??(prefs.ThemeId=="custom"&&prefs.CustomTheme!=null?new(prefs.CustomTheme,prefs.CustomThemeImages??new()):null);
    internal static List<KeyValuePair<string,string>> Choices(AppPreferences prefs){
        var choices=ThemePalette.BuiltIns.Select(p=>new KeyValuePair<string,string>(p.Key,p.Value["name"])).ToList();
        if(prefs.CustomTheme is {} custom)choices.Add(new("custom",custom.GetValueOrDefault("name")??"Importiertes Theme"));
        choices.AddRange(prefs.ImportedThemes.Select(p=>new KeyValuePair<string,string>(p.Key,p.Value.Values.GetValueOrDefault("name")??"Importiertes Theme")));return choices;
    }
    internal static void Change(AppPreferences prefs,Action change,Action save){
        var id=prefs.ThemeId;var values=prefs.CustomTheme;var images=prefs.CustomThemeImages;var library=new Dictionary<string,ThemePackage>(prefs.ImportedThemes);
        try{change();save();}catch{prefs.ThemeId=id;prefs.CustomTheme=values;prefs.CustomThemeImages=images;prefs.ImportedThemes=library;throw;}
    }
    internal static void Import(AppPreferences prefs,ThemePackage package){
        if(prefs.ImportedThemes.Count+(prefs.CustomTheme==null?0:1)>=20)throw new InvalidDataException("Maximal 20 eigene Themes. Bitte zuerst ein nicht mehr benötigtes Theme entfernen.");
        long bytes=package.Images.Values.Sum(b=>(long)b.Length)+prefs.ImportedThemes.Values.Sum(p=>p.Images.Values.Sum(b=>(long)b.Length))+(prefs.CustomThemeImages?.Values.Sum(b=>(long)b.Length)??0);
        if(bytes>64L*1024*1024)throw new InvalidDataException("Die Theme-Sammlung würde 64 MB an Grafiken und Schriften überschreiten.");
        if(prefs.CustomTheme!=null){var legacy="imported-"+Guid.NewGuid().ToString("N");prefs.ImportedThemes.Add(legacy,new(prefs.CustomTheme,prefs.CustomThemeImages??new()));prefs.CustomTheme=null;prefs.CustomThemeImages=null;}
        var names=prefs.ImportedThemes.Values.Select(p=>p.Values.GetValueOrDefault("name")).Concat(ThemePalette.BuiltIns.Values.Select(p=>p["name"])).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var values=new Dictionary<string,string>(package.Values,StringComparer.OrdinalIgnoreCase);string original=values["name"],name=original;int suffix=2;
        while(names.Contains(name)){string number=" ("+(suffix++)+")";name=original[..Math.Min(original.Length,80-number.Length)]+number;}values["name"]=name;
        string id="imported-"+Guid.NewGuid().ToString("N");prefs.ImportedThemes.Add(id,new(values,new(package.Images,StringComparer.OrdinalIgnoreCase)));prefs.ThemeId=id;
    }
    internal static void Remove(AppPreferences prefs,string id){
        if(id=="custom"){prefs.CustomTheme=null;prefs.CustomThemeImages=null;}
        else if(!prefs.ImportedThemes.Remove(id))throw new InvalidOperationException("Original-Themes können nicht entfernt werden.");
        if(prefs.ThemeId==id)prefs.ThemeId="default";
    }
}

internal sealed partial class SettingsWindow
{
    void ManageThemes(){
        using var dialog=new Form{Text="Eigene Themes",ClientSize=new(490,340),StartPosition=FormStartPosition.CenterParent,Font=Font,FormBorderStyle=FormBorderStyle.FixedDialog,MaximizeBox=false,MinimizeBox=false,Padding=new(16)};
        var list=new ListBox{Dock=DockStyle.Fill,DisplayMember="Value",BorderStyle=BorderStyle.None};
        var footer=new FlowLayoutPanel{Dock=DockStyle.Bottom,Height=44,Padding=new(0,8,0,0)};
        string? Selected()=>list.SelectedItem is KeyValuePair<string,string> pair?pair.Key:null;
        ThemePackage Package(string id)=>id=="custom"?new(prefs.CustomTheme!,prefs.CustomThemeImages??new()):prefs.ImportedThemes[id];
        var preview=DashboardWindow.ActionButton("Vorschau",()=>{if(Selected() is not {} id)return;using var window=new ThemePreview(Package(id));if(window.ShowDialog(dialog)==DialogResult.OK){ThemeLibrary.Change(prefs,()=>prefs.ThemeId=id,Save);dialog.DialogResult=DialogResult.OK;}});
        var use=DashboardWindow.ActionButton("Verwenden",()=>{if(Selected() is {} id){ThemeLibrary.Change(prefs,()=>prefs.ThemeId=id,Save);dialog.DialogResult=DialogResult.OK;}});
        var remove=DashboardWindow.ActionButton("Entfernen",()=>{});
        void Fill(){list.Items.Clear();foreach(var choice in ThemeLibrary.Choices(prefs).Where(p=>!ThemePalette.BuiltIns.ContainsKey(p.Key)))list.Items.Add(choice);if(list.Items.Count>0)list.SelectedIndex=0;preview.Enabled=use.Enabled=remove.Enabled=list.Items.Count>0;}
        remove.Click+=(_,_)=>{if(Selected() is not {} id)return;if(MessageBox.Show(dialog,"Dieses Theme aus der Sammlung entfernen? Beim aktiven Theme wird auf Standard gewechselt.","Theme entfernen",MessageBoxButtons.OKCancel)!=DialogResult.OK)return;try{ThemeLibrary.Change(prefs,()=>ThemeLibrary.Remove(prefs,id),Save);Fill();}catch(IOException){MessageBox.Show(dialog,"Das Theme konnte nicht entfernt werden. Bitte erneut versuchen.");}};
        footer.Controls.AddRange([preview,use,remove]);dialog.Controls.Add(list);dialog.Controls.Add(footer);Fill();dialog.ShowDialog(this);ShowTab(3);
    }
}
