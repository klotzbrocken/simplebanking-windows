using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Security.Principal;
using System.Text;
using System.Text.Json;
using Microsoft.Win32.SafeHandles;
namespace SimpleBanking;

internal interface IHelloKeys
{
    byte[] Create(string name, IntPtr parent);
    byte[] Unwrap(string name, byte[] publicHash, byte[] wrapped, IntPtr parent);
    void Delete(string name);
}

// A Passport private-key operation is the gate; no DPAPI-only or consent-boolean fallback.
internal sealed class PassportHelloKeys : IHelloKeys
{
    static readonly CngProvider Provider = new("Microsoft Passport Key Storage Provider");
    const string CachePolicy = "NgcCacheType";
    [DllImport("ncrypt.dll")] static extern int NCryptDecrypt(SafeNCryptKeyHandle key, byte[] input, int inputLength, IntPtr padding, byte[] output, int outputLength, out int written, int flags);
    static CngProperty Number(string name, int value) => new(name, BitConverter.GetBytes(value), CngPropertyOptions.None);
    static void Context(CngKey key, IntPtr parent) {
        key.ParentWindowHandle=parent;
        key.SetProperty(new("Use Context",Encoding.Unicode.GetBytes("simplebanking entsperren\0"),CngPropertyOptions.None));
    }
    static void Validate(CngKey key) {
        var policy=key.GetProperty(CachePolicy,CngPropertyOptions.None).GetValue();
        if(policy==null||policy.Length!=4||BitConverter.ToInt32(policy)!=1||key.ExportPolicy!=CngExportPolicies.None||((int)key.KeyUsage&8)!=0||!key.KeyUsage.HasFlag(CngKeyUsages.Decryption))
            throw new CryptographicException("Der Windows-Hello-Schlüssel erfüllt die Schutzanforderungen nicht.");
    }
    public byte[] Create(string name, IntPtr parent) {
        var options=new CngKeyCreationParameters {Provider=Provider,ExportPolicy=CngExportPolicies.None,KeyUsage=CngKeyUsages.Decryption,ParentWindowHandle=parent};
        options.Parameters.Add(Number("Length",2048));options.Parameters.Add(Number(CachePolicy,1));
        using var key=CngKey.Create(CngAlgorithm.Rsa,name,options);
        try {Validate(key);using var rsa=new RSACng(key);return rsa.ExportSubjectPublicKeyInfo();}
        catch {key.Delete();throw;}
    }
    public byte[] Unwrap(string name, byte[] publicHash, byte[] wrapped, IntPtr parent) {
        using var key=CngKey.Open(name,Provider);Validate(key);Context(key,parent);
        using var rsa=new RSACng(key);
        if(!CryptographicOperations.FixedTimeEquals(SHA256.HashData(rsa.ExportSubjectPublicKeyInfo()),publicHash))throw new CryptographicException("Windows-Hello-Schlüssel wurde ersetzt.");
        key.SetProperty(Number("PinCacheIsGestureRequired",1));
        byte[] buffer=new byte[256];
        try {
            using var handle=key.Handle;
            int status=NCryptDecrypt(handle,wrapped,wrapped.Length,IntPtr.Zero,buffer,buffer.Length,out int length,2);
            if(status!=0)throw new CryptographicException(status);
            if(length!=32)throw new CryptographicException("Ungültiger Freigabeschlüssel.");
            return buffer.AsSpan(0,length).ToArray();
        } finally {CryptographicOperations.ZeroMemory(buffer);}
    }
    public void Delete(string name) {if(CngKey.Exists(name,Provider)){using var key=CngKey.Open(name,Provider,CngKeyOpenOptions.Silent);key.Delete();}}
}

internal sealed class HelloVault
{
    internal sealed record Envelope(int Version,string KeyName,byte[] PublicHash,byte[] WrappedKey,byte[] Nonce,byte[] Tag,byte[] Ciphertext);
    readonly string root,path,prefix;readonly IHelloKeys keys;
    internal HelloVault(string root,IHelloKeys? keys=null) {
        this.root=Path.GetFullPath(root).ToUpperInvariant();path=Path.Combine(root,"hello.json");this.keys=keys??new PassportHelloKeys();
        var sid=WindowsIdentity.GetCurrent().User?.Value??throw new InvalidOperationException("Windows-Benutzer fehlt.");
        prefix=sid+"//simplebanking/"+Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(this.root)))[..24]+"/";
    }
    internal bool Enabled=>File.Exists(path);
    byte[] Aad(string name)=>Encoding.UTF8.GetBytes("simplebanking-hello-v1|"+root+"|"+name);
    Envelope Read() {
        if(new FileInfo(path).Length>65536)throw new InvalidDataException("Ungültige Hello-Konfiguration.");
        var e=JsonSerializer.Deserialize<Envelope>(File.ReadAllBytes(path))??throw new InvalidDataException("Leere Hello-Konfiguration.");
        if(e.Version!=1||e.KeyName==null||!e.KeyName.StartsWith(prefix,StringComparison.Ordinal)||!Guid.TryParseExact(e.KeyName[prefix.Length..],"N",out _)||e.PublicHash?.Length!=32||e.WrappedKey?.Length!=256||e.Nonce?.Length!=12||e.Tag?.Length!=16||e.Ciphertext==null||e.Ciphertext.Length>16384)throw new InvalidDataException("Ungültige Hello-Konfiguration.");
        return e;
    }
    internal void Enable(string password,IntPtr parent) {
        byte[] plain=Encoding.UTF8.GetBytes(password),secret=RandomNumberGenerator.GetBytes(32);string name=prefix+Guid.NewGuid().ToString("N");bool committed=false,created=false;
        string temporary=path+"."+Guid.NewGuid().ToString("N")+".tmp";Envelope? old=null;
        try {
            if(plain.Length>16384)throw new InvalidDataException("Passwort ist für Windows Hello zu lang.");
            if(Enabled){try{old=Read();}catch(InvalidDataException){}catch(JsonException){}}
            byte[] publicKey=keys.Create(name,parent);created=true;
            using var rsa=RSA.Create();rsa.ImportSubjectPublicKeyInfo(publicKey,out _);
            byte[] wrapped=rsa.Encrypt(secret,RSAEncryptionPadding.Pkcs1),hash=SHA256.HashData(publicKey);
            byte[] verified=keys.Unwrap(name,hash,wrapped,parent);
            try {if(!CryptographicOperations.FixedTimeEquals(secret,verified))throw new CryptographicException("Hello-Freigabe konnte nicht bestätigt werden.");}finally{CryptographicOperations.ZeroMemory(verified);}
            byte[] nonce=RandomNumberGenerator.GetBytes(12),tag=new byte[16],cipher=new byte[plain.Length];
            using(var aes=new AesGcm(secret,16))aes.Encrypt(nonce,plain,cipher,tag,Aad(name));
            Directory.CreateDirectory(root);
            using(var file=new FileStream(temporary,FileMode.CreateNew,FileAccess.Write,FileShare.None)){file.Write(JsonSerializer.SerializeToUtf8Bytes(new Envelope(1,name,hash,wrapped,nonce,tag,cipher)));file.Flush(true);}
            File.Move(temporary,path,true);committed=true;
            if(old!=null)TryDelete(old.KeyName);
        } finally {
            CryptographicOperations.ZeroMemory(plain);CryptographicOperations.ZeroMemory(secret);
            if(File.Exists(temporary))File.Delete(temporary);
            if(created&&!committed)TryDelete(name);
        }
    }
    internal string Unlock(IntPtr parent) {
        var e=Read();byte[] secret=keys.Unwrap(e.KeyName,e.PublicHash,e.WrappedKey,parent),plain=new byte[e.Ciphertext.Length];
        try {using var aes=new AesGcm(secret,16);aes.Decrypt(e.Nonce,e.Ciphertext,e.Tag,plain,Aad(e.KeyName));return Encoding.UTF8.GetString(plain);}
        finally {CryptographicOperations.ZeroMemory(secret);CryptographicOperations.ZeroMemory(plain);}
    }
    internal void Disable() {
        Envelope? old=null;if(Enabled){try{old=Read();}catch(InvalidDataException){}catch(JsonException){}File.Delete(path);}if(old!=null)TryDelete(old.KeyName);
    }
    void TryDelete(string name){try{keys.Delete(name);}catch(CryptographicException){/* Removed envelope revokes application access even if Windows cannot remove an orphaned key. */}}
    internal static bool Canceled(Exception error)=>error is OperationCanceledException||error.HResult==unchecked((int)0x80090036)||error.HResult==unchecked((int)0x800704C7)||error.HResult==1223;
}

internal sealed partial class MainWindow
{
    readonly IHelloKeys? helloKeys;
    HelloVault Hello=>new(store.Root,helloKeys);
    internal bool HelloEnabled=>!demo&&Hello.Enabled;
    internal void ConfigureHello(IWin32Window parent) {
        if(demo){MessageBox.Show(parent,"Windows Hello wird nur für einen echten Tresor eingerichtet. Die Demo speichert keine Zugangsdaten.","Windows Hello");return;}
        if(!RequireUnlocked()||password==null)return;
        int epoch=Volatile.Read(ref securityEpoch);
        try {
            if(HelloEnabled){Hello.Disable();return;}
            var confirmation=Prompt("Windows Hello einrichten","Masterpasswort zur Bestätigung",true);if(confirmation==null||epoch!=Volatile.Read(ref securityEpoch)||!Unlocked)return;
            store.Load(confirmation);
            Hello.Enable(confirmation,parent.Handle);
            if(epoch!=Volatile.Read(ref securityEpoch)||!Unlocked){Hello.Disable();return;}
            MessageBox.Show(parent,"Windows Hello ist eingerichtet. Beim nächsten Entsperren erscheint die Windows-Freigabe. Dein Masterpasswort bleibt als Alternative verfügbar.","Windows Hello");
        } catch(Exception e) when(e is CryptographicException or IOException or UnauthorizedAccessException or JsonException or InvalidDataException) {
            if(!HelloVault.Canceled(e))MessageBox.Show(parent,"Windows Hello konnte nicht eingerichtet werden. Prüfe dein Masterpasswort und Windows → Einstellungen → Konten → Anmeldeoptionen. Der Tresor bleibt per Masterpasswort zugänglich.\n\nFehlercode: 0x"+e.HResult.ToString("X8"),"Windows Hello");
        }
    }
}
