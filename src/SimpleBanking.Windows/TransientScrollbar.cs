namespace SimpleBanking;
internal static class TransientScrollbar
{
    internal static void Attach(TextBox text){
        text.ScrollBars=ScrollBars.None;
        var timer=new System.Windows.Forms.Timer{Interval=1200};
        text.MouseWheel+=(_,_)=>{text.ScrollBars=ScrollBars.Vertical;timer.Stop();timer.Start();};
        timer.Tick+=(_,_)=>{timer.Stop();if(!text.IsDisposed)text.ScrollBars=ScrollBars.None;};
        text.Disposed+=(_,_)=>timer.Dispose();
    }
}
