using System.Runtime.InteropServices;
using Microsoft.Win32;
namespace SimpleBanking;
internal sealed class BankingHotkeys : NativeWindow,IDisposable
{
    [DllImport("user32.dll")] static extern bool RegisterHotKey(IntPtr handle,int id,uint modifiers,uint key);
    [DllImport("user32.dll")] static extern bool UnregisterHotKey(IntPtr handle,int id);
    readonly Action open,refresh;
    internal BankingHotkeys(Action open,Action refresh){this.open=open;this.refresh=refresh;CreateHandle(new CreateParams());}
    internal bool Configure(bool enabled,bool refreshEnabled){UnregisterHotKey(Handle,1);UnregisterHotKey(Handle,2);bool success=true;if(enabled)success=RegisterHotKey(Handle,1,0x4003,(uint)Keys.S);if(refreshEnabled)success=RegisterHotKey(Handle,2,0x4003,(uint)Keys.R)&&success;return success;}
    protected override void WndProc(ref Message m){if(m.Msg==0x312){if(m.WParam==1)open();else if(m.WParam==2)refresh();}base.WndProc(ref m);}
    public void Dispose(){UnregisterHotKey(Handle,1);UnregisterHotKey(Handle,2);DestroyHandle();}
}
internal sealed partial class MainWindow
{
    BankingHotkeys? hotkeys;
    readonly System.Windows.Forms.Timer idleLockTimer=new(){Interval=1000};
    bool forceLockPending;
    int securityEpoch;
    CancellationTokenSource? bankCancellation;
    [StructLayout(LayoutKind.Sequential)] struct LastInput {public uint Size;public uint Tick;}
    [DllImport("user32.dll")] static extern bool GetLastInputInfo(ref LastInput input);
    void InitializeWindowsIntegration(){
        hotkeys=new BankingHotkeys(OpenMainWindow,()=>{if(RequireUnlocked())RefreshBank();});ConfigureWindowsIntegration();
        SystemEvents.SessionSwitch+=OnSessionSwitch;
        idleLockTimer.Tick+=(_,_)=>{
            if(IsLocked||Preferences.AutoLockMinutes<=0)return;var input=new LastInput{Size=(uint)Marshal.SizeOf<LastInput>()};
            if(GetLastInputInfo(ref input)&&unchecked((uint)Environment.TickCount-input.Tick)>Preferences.AutoLockMinutes*60000L)ForceLock();
        };idleLockTimer.Start();
        Disposed+=(_,_)=>{idleLockTimer.Dispose();hotkeys?.Dispose();SystemEvents.SessionSwitch-=OnSessionSwitch;};
    }
    void OnSessionSwitch(object sender,SessionSwitchEventArgs e){
        if(e.Reason!=SessionSwitchReason.SessionLock||IsDisposed)return;
        Interlocked.Increment(ref securityEpoch);
        if(Preferences.LockOnSessionLock&&IsHandleCreated)BeginInvoke(new Action(ForceLock));
    }
    void ForceLock(){
        if(demo||IsLocked)return;
        if(bankBusy){forceLockPending=true;foreach(var form in OwnedForms.ToArray())form.Close();bankCancellation?.Cancel();transactionList.Populate([],true,false);BalanceChanged?.Invoke();Hide();return;}
        Lock();Hide();
    }
    void ConfigureWindowsIntegration(){if(hotkeys!=null&&!hotkeys.Configure(Preferences.GlobalHotkey,Preferences.RefreshHotkey))status.Text="Tastenkürzel wird bereits von einer anderen Anwendung verwendet.";}
    internal void SetLaunchAtLogin(bool enabled){
        if(demo){MessageBox.Show(this,"Autostart bitte in der regulären Version einstellen.");return;}
        try{using var key=Registry.CurrentUser.CreateSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run");if(enabled)key.SetValue("simplebanking",'"'+Environment.ProcessPath+'"');else key.DeleteValue("simplebanking",false);Preferences.LaunchAtLogin=enabled;Preferences.Save();}catch(Exception e) when(e is UnauthorizedAccessException or System.Security.SecurityException){MessageBox.Show(this,"Die Windows-Anmeldeeinstellung konnte nicht gespeichert werden.");}
    }
}
