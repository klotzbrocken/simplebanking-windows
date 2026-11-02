using System.Runtime.CompilerServices;
namespace SimpleBanking;
internal static class ThemeStyling
{
    sealed class State { internal int Revision=-1;internal Font? Font;internal float Size;internal bool Bold;internal string? Text;internal bool ChangingText;internal Color Ink; }
    static readonly ConditionalWeakTable<Control,State> states=new();
    static readonly Dictionary<(string,float,bool),Font> fonts=new();
    static readonly Dictionary<Form,BtxCrtOverlay> overlays=new();
    static bool crt;
    internal static ThemePalette Current {get;private set;}=ThemePalette.For(new());
    static int revision;
    static bool installed;
    internal static void Use(AppPreferences preferences){
        Current=ThemePalette.For(preferences);crt=preferences.BtxCrtEnabled&&Current.Id=="btx";revision++;
        if(!installed){installed=true;Application.Idle+=(_,_)=>Refresh();Application.ApplicationExit+=(_,_)=>{foreach(var font in fonts.Values)font.Dispose();fonts.Clear();};}
        Refresh();
    }
    internal static void Refresh(){
        foreach(var pair in overlays.ToArray())if(!crt||pair.Key.IsDisposed||pair.Value.IsDisposed){pair.Value.Dispose();overlays.Remove(pair.Key);}
        foreach(Form form in Application.OpenForms.Cast<Form>().ToArray()){
            if(form is ThemePreview or BtxCrtOverlay)continue;Apply(form);
            if(crt&&form.Visible&&(form is MainWindow or BalanceFlyout)&&!overlays.ContainsKey(form))overlays.Add(form,new BtxCrtOverlay(form));
        }
    }
    internal static void Apply(Control root){
        if(root.IsDisposed)return;
        if(!states.TryGetValue(root,out var state)){
            bool inherited=root.Parent!=null&&ReferenceEquals(root.Font,root.Parent.Font)&&states.TryGetValue(root.Parent,out _);
            state=inherited?new(){Size=states.GetOrCreateValue(root.Parent!).Size,Bold=states.GetOrCreateValue(root.Parent!).Bold}:new(){Size=root.Font.SizeInPoints*96/72,Bold=root.Font.Bold};states.Add(root,state);
            state.Ink=root.ForeColor;
            if(root is Label or Button && root is not HeaderIcon){state.Text=root.Text;root.TextChanged+=(_,_)=>{if(state.ChangingText)return;state.Text=root.Text;SetText(root,state);};}
            root.ControlAdded+=(_,e)=>{if(e.Control!=null)Apply(e.Control);};
            if(root is ComboBox combo){combo.DrawMode=DrawMode.OwnerDrawFixed;combo.DrawItem+=(_,e)=>{if(e.Index<0)return;var theme=Current;using var fill=new SolidBrush((e.State&DrawItemState.Selected)!=0?ThemePalette.Blend(theme.Accent,theme.Surface,.2):theme.Surface);e.Graphics.FillRectangle(fill,e.Bounds);TextRenderer.DrawText(e.Graphics,combo.GetItemText(combo.Items[e.Index]),combo.Font,e.Bounds,theme.Ink,TextFormatFlags.VerticalCenter|TextFormatFlags.EndEllipsis);if((e.State&DrawItemState.Focus)!=0)e.DrawFocusRectangle();};}
        }
        if(state.Revision!=revision){
            state.Revision=revision;var theme=Current;
            // Main window and balance surfaces paint their own MoneyMood backgrounds.
            bool owned=root is MainWindow or BalanceFlyout or BalanceCard or BookingRow or DateHeading or HeaderIcon;
            if(!owned){
                if(root.BackColor!=Color.Transparent)root.BackColor=root is TextBoxBase or ComboBox or NumericUpDown?theme.Surface:theme.Panel;
                root.ForeColor=state.Ink==ExportDesign.Error?ExportDesign.Error:theme.Ink;
                var key=(theme.FontIdentity,state.Size,state.Bold);
                if(!fonts.TryGetValue(key,out var font)){font=theme.Font(state.Size,state.Bold);fonts.Add(key,font);}
                state.Font=font;root.Font=font;
            }
            if(root is HeaderIcon icon){icon.Tint=theme.Secondary;icon.TextMode=!theme.GlyphControls;}
            SetText(root,state);
            if(root is Button button){button.FlatAppearance.BorderColor=ThemePalette.Blend(theme.Ink,theme.Surface,.2);button.FlatAppearance.MouseOverBackColor=ThemePalette.Blend(theme.Accent,theme.Surface,.12);}
            if(root is LinkLabel link){link.LinkColor=theme.Accent;link.ActiveLinkColor=theme.Ink;}
            if(root is ComboBox box){box.FlatStyle=FlatStyle.Flat;box.ItemHeight=Math.Max(20,box.Font.Height+4);}
            if(root is DataGridView grid){
                grid.BackgroundColor=theme.Panel;grid.GridColor=ThemePalette.Blend(theme.Ink,theme.Surface,.15);
                grid.DefaultCellStyle.BackColor=theme.Surface;grid.DefaultCellStyle.ForeColor=theme.Ink;grid.DefaultCellStyle.Font=root.Font;
                grid.DefaultCellStyle.SelectionBackColor=ThemePalette.Blend(theme.Accent,theme.Surface,.2);grid.DefaultCellStyle.SelectionForeColor=theme.Ink;
                grid.ColumnHeadersDefaultCellStyle.BackColor=theme.Panel;grid.ColumnHeadersDefaultCellStyle.ForeColor=theme.Ink;
                grid.ColumnHeadersDefaultCellStyle.SelectionBackColor=theme.Panel;grid.ColumnHeadersDefaultCellStyle.SelectionForeColor=theme.Ink;
            }
            root.Invalidate();
        }
        foreach(Control child in root.Controls)Apply(child);
    }
    static void SetText(Control control,State state){if(state.Text==null)return;state.ChangingText=true;try{control.Text=Current.Text(state.Text);}finally{state.ChangingText=false;}}
}
