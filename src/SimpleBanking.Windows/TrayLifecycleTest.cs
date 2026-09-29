using System.Reflection;
namespace SimpleBanking;
internal static class TrayLifecycleTest
{
    internal static int Run(){
        using var signal=new EventWaitHandle(false,EventResetMode.AutoReset);
        using var context=new TrayContext(true,false,null,signal);
        var window=(MainWindow)typeof(TrayContext).GetField("window",BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(context)!;
        var tray=(NotifyIcon)typeof(TrayContext).GetField("tray",BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(context)!;
        var first=tray.Icon;window.SelectFlyoutAccount(1);
        if(ReferenceEquals(first,tray.Icon)||!tray.Text.Contains(window.FlyoutAccounts()[1].Name))throw new InvalidOperationException("Tray icon did not follow selected bank.");
        using(var actual=tray.Icon!.ToBitmap())using(var expectedIcon=AppArtwork.CreateBankTrayIcon(window.FlyoutAccounts()[1]))using(var expected=expectedIcon.ToBitmap()){
            for(int y=0;y<32;y++)for(int x=0;x<32;x++)if(actual.GetPixel(x,y)!=expected.GetPixel(x,y))throw new InvalidOperationException("Tray does not display bank artwork.");
        }
        bool exited=false;context.ThreadExit+=(_,_)=>exited=true;
        window.Close();
        if(window.IsDisposed||window.Visible||exited)throw new InvalidOperationException("User close must hide the tray window.");
        window.OpenMainWindow();if(!window.Visible)throw new InvalidOperationException("Hidden tray window did not reopen.");
        window.Dispose();
        if(!exited){try{window.OpenMainWindow();}catch(ObjectDisposedException){Console.Error.WriteLine("REPRODUCED: tray survives disposed main window; subsequent activation throws ObjectDisposedException.");}return 1;}
        Console.WriteLine("PASS: hiding/reopening works; disposed main window terminates tray context.");return 0;
    }
}
