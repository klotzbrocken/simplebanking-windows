using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
namespace SimpleBanking;

internal sealed class RemoteMerchantLogos
{
    sealed record Configuration(Dictionary<string,string> domains,string token);
    sealed record Budget(string Day,int Count);
    static readonly Configuration config=ReadConfiguration();
    internal static readonly RemoteMerchantLogos Shared=new(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"SimplebankingWindows","LogoCache"));
    readonly string root;readonly HttpClient http;readonly SemaphoreSlim gate=new(1);
    readonly Dictionary<string,DateTimeOffset> retryAfter=[];
    static Configuration ReadConfiguration(){using var stream=typeof(RemoteMerchantLogos).Assembly.GetManifestResourceStream("SimpleBanking.RemoteMerchantLogos.json")!;return JsonSerializer.Deserialize<Configuration>(stream)!;}
    internal RemoteMerchantLogos(string root,HttpMessageHandler? handler=null){this.root=root;http=handler==null?new HttpClient():new HttpClient(handler,false);http.Timeout=TimeSpan.FromSeconds(15);}
    internal static IEnumerable<string> KnownMerchants=>config.domains.Keys;
    internal static string? Domain(string merchant)=>config.domains.GetValueOrDefault(merchant.Trim().ToLowerInvariant());
    string FileFor(string domain,string extension)=>Path.Combine(root,Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(domain)))+extension);
    static bool Fresh(string path)=>File.Exists(path)&&File.GetLastWriteTimeUtc(path)>DateTime.UtcNow.AddDays(-30);
    internal Image? Load(string merchant){
        if(Domain(merchant) is not {} domain)return null;string file=FileFor(domain,".png");
        try{if(!Fresh(file))return null;using var image=Image.FromFile(file);return new Bitmap(image);}catch(Exception e) when(e is IOException or ArgumentException or OutOfMemoryException){return null;}
    }
    internal static byte[] ValidateImage(byte[] bytes){
        if(bytes.Length<33||bytes.Length>2*1024*1024||!bytes.AsSpan(0,8).SequenceEqual(new byte[]{137,80,78,71,13,10,26,10}))throw new InvalidDataException("Kein gültiges Logo-PNG.");
        uint w=System.Buffers.Binary.BinaryPrimitives.ReadUInt32BigEndian(bytes.AsSpan(16,4)),h=System.Buffers.Binary.BinaryPrimitives.ReadUInt32BigEndian(bytes.AsSpan(20,4));
        if(w<64||h<64||w>1024||h>1024)throw new InvalidDataException("Logo-Abmessungen ungeeignet.");
        using var stream=new MemoryStream(bytes);using var image=Image.FromStream(stream,true,true);using var output=new MemoryStream();image.Save(output,System.Drawing.Imaging.ImageFormat.Png);return output.ToArray();
    }
    static void AtomicWrite(string path,byte[] bytes){string temporary=path+"."+Guid.NewGuid().ToString("N")+".tmp";try{File.WriteAllBytes(temporary,bytes);File.Move(temporary,path,true);}finally{if(File.Exists(temporary))File.Delete(temporary);}}
    bool Reserve(){
        Directory.CreateDirectory(root);string path=Path.Combine(root,"budget.json"),today=DateTime.Today.ToString("yyyy-MM-dd");Budget? budget=null;
        if(File.Exists(path)){try{budget=JsonSerializer.Deserialize<Budget>(File.ReadAllBytes(path));}catch(JsonException){return false;}}
        int count=budget?.Day==today?budget.Count:0;if(count>=3)return false;AtomicWrite(path,JsonSerializer.SerializeToUtf8Bytes(new Budget(today,count+1)));return true;
    }
    internal async Task<bool> Fetch(IEnumerable<Entry> entries,CancellationToken cancellation){
        var domains=entries.Where(e=>e.CustomLogo is not {Length:>0}&&e.MerchantLogo is not {Length:>0}&&!MerchantArtwork.HasBundled(e)).Select(e=>Domain(e.Merchant)).OfType<string>().Distinct().ToArray();
        bool changed=false;await gate.WaitAsync(cancellation).ConfigureAwait(false);
        try {
            foreach(string domain in domains){
                cancellation.ThrowIfCancellationRequested();
                if(Fresh(FileFor(domain,".png"))||Fresh(FileFor(domain,".miss"))||retryAfter.GetValueOrDefault(domain)>DateTimeOffset.UtcNow)continue;
                if(!Reserve())break;
                retryAfter[domain]=DateTimeOffset.UtcNow.AddMinutes(15);
                try {
                    var url=new Uri("https://img.logo.dev/"+Uri.EscapeDataString(domain)+"?token="+Uri.EscapeDataString(config.token)+"&size=256&format=png&fallback=404");
                    using var response=await http.GetAsync(url,HttpCompletionOption.ResponseHeadersRead,cancellation).ConfigureAwait(false);
                    if(response.StatusCode==System.Net.HttpStatusCode.NotFound){AtomicWrite(FileFor(domain,".miss"),[]);continue;}
                    if(!response.IsSuccessStatusCode||response.Content.Headers.ContentLength>2*1024*1024)continue;
                    using var input=await response.Content.ReadAsStreamAsync(cancellation).ConfigureAwait(false);using var buffer=new MemoryStream();byte[] chunk=new byte[8192];int read;
                    while((read=await input.ReadAsync(chunk,cancellation).ConfigureAwait(false))>0){if(buffer.Length+read>2*1024*1024)throw new InvalidDataException("Logo zu groß.");buffer.Write(chunk,0,read);}
                    byte[] image=ValidateImage(buffer.ToArray());cancellation.ThrowIfCancellationRequested();AtomicWrite(FileFor(domain,".png"),image);changed=true;
                } catch(Exception e) when(e is HttpRequestException or IOException or ArgumentException or OutOfMemoryException or TaskCanceledException){if(cancellation.IsCancellationRequested)throw;}
            }
        } finally {gate.Release();}
        return changed;
    }
}
internal sealed partial class MainWindow
{
    bool logosLoading;
    CancellationTokenSource? logoCancellation;
    async void LoadRemoteLogos(IEnumerable<Entry> entries){
        if(demo||Preferences.Ephemeral||IsLocked||!Preferences.RemoteMerchantLogos||logosLoading)return;
        logosLoading=true;logoCancellation?.Dispose();logoCancellation=new();
        try{if(await RemoteMerchantLogos.Shared.Fetch(entries.Take(5000).ToArray(),logoCancellation.Token)&&!IsDisposed&&!IsLocked){RefreshEntries();dashboardWindow?.Invalidate(true);}}
        catch(Exception e) when(e is IOException or HttpRequestException or OperationCanceledException or UnauthorizedAccessException){/* Keep the category icon; never interrupt banking for artwork. */}
        finally{logosLoading=false;}
    }
}
