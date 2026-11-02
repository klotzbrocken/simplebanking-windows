using System.Diagnostics;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text.Json.Nodes;
namespace SimpleBanking;

internal sealed record AppUpdate(Version Version,string Tag,string Notes,Uri Download,long Size,string Sha256);
internal static class AppUpdater
{
    internal const string Repository="klotzbrocken/simplebanking-windows";
    internal static Version Current=>typeof(AppUpdater).Assembly.GetName().Version!;
    internal static AppUpdate? Select(JsonArray releases,Version current,bool beta){
        var available=new List<AppUpdate>();
        foreach(var release in releases){
            if(release?["draft"]?.GetValue<bool>()!=false||(!beta&&release["prerelease"]?.GetValue<bool>()==true))continue;
            string tag=release["tag_name"]?.GetValue<string>()??"";
            if(!Version.TryParse(tag.TrimStart('v').Split('-')[0],out var version)||new Version(version.Major,version.Minor,Math.Max(0,version.Build),0)<=new Version(current.Major,current.Minor,Math.Max(0,current.Build),0))continue;
            var asset=release["assets"]?.AsArray().FirstOrDefault(a=>a?["name"]?.GetValue<string>() is {} name&&name.StartsWith("simplebanking",StringComparison.OrdinalIgnoreCase)&&name.EndsWith("Setup.exe",StringComparison.OrdinalIgnoreCase));
            string digest=asset?["digest"]?.GetValue<string>()??"";
            if(!digest.StartsWith("sha256:")||digest.Length!=71||!digest[7..].All(Uri.IsHexDigit))continue;
            if(!Uri.TryCreate(asset?["browser_download_url"]?.GetValue<string>(),UriKind.Absolute,out var url)||url.Scheme!="https"||url.Host!="github.com"||!url.AbsolutePath.StartsWith("/"+Repository+"/releases/download/",StringComparison.Ordinal)||url.UserInfo.Length>0)continue;
            long size=asset?["size"]?.GetValue<long>()??0;if(size<=0||size>300*1024*1024)continue;
            available.Add(new(version,tag,release["body"]?.GetValue<string>()??"",url,size,digest[7..]));
        }
        return available.OrderByDescending(a=>a.Version).FirstOrDefault();
    }
    static HttpClient Client(HttpMessageHandler? handler=null){var client=handler==null?new HttpClient():new HttpClient(handler,false);client.Timeout=TimeSpan.FromMinutes(5);client.DefaultRequestHeaders.UserAgent.ParseAdd("simplebanking-windows/"+Current.ToString(3));client.DefaultRequestHeaders.Accept.ParseAdd("application/vnd.github+json");return client;}
    internal static async Task<AppUpdate?> Check(bool beta,CancellationToken cancellation){using var client=Client();var releases=await client.GetFromJsonAsync<JsonArray>("https://api.github.com/repos/"+Repository+"/releases?per_page=100",cancellation)??new();return Select(releases,Current,beta);}
    internal static async Task Verify(string path,AppUpdate update,CancellationToken cancellation){using var stream=File.OpenRead(path);if(stream.Length!=update.Size||!Convert.ToHexString(await SHA256.HashDataAsync(stream,cancellation)).Equals(update.Sha256,StringComparison.OrdinalIgnoreCase))throw new InvalidDataException("Das Update ist unvollständig oder seine Prüfsumme stimmt nicht.");}
    internal static async Task<string> Download(AppUpdate update,IProgress<int> progress,CancellationToken cancellation,HttpMessageHandler? handler=null){
        string directory=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"SimplebankingWindows","Updates");Directory.CreateDirectory(directory);
        string target=Path.Combine(directory,"simplebanking-"+update.Version+"-"+Guid.NewGuid().ToString("N")+"-Setup.exe"),partial=target+".part";
        try{
            using var client=Client(handler);using var response=await client.GetAsync(update.Download,HttpCompletionOption.ResponseHeadersRead,cancellation);response.EnsureSuccessStatusCode();
            if(response.Content.Headers.ContentLength is {} length&&length!=update.Size)throw new InvalidDataException("Update-Dateigröße stimmt nicht.");
            await using(var output=new FileStream(partial,FileMode.CreateNew,FileAccess.Write,FileShare.None)){
                using var input=await response.Content.ReadAsStreamAsync(cancellation);byte[] buffer=new byte[81920];long total=0;int read;
                while((read=await input.ReadAsync(buffer,cancellation))>0){total+=read;if(total>update.Size)throw new InvalidDataException("Update überschreitet die erwartete Größe.");await output.WriteAsync(buffer.AsMemory(0,read),cancellation);progress.Report((int)(total*100/update.Size));}
            }
            await Verify(partial,update,cancellation);File.Move(partial,target);return target;
        }finally{if(File.Exists(partial))File.Delete(partial);}
    }
}
internal sealed class UpdateWindow : Form
{
    readonly Label state=new(){Dock=DockStyle.Top,AutoSize=true,Padding=new Padding(0,0,0,12)};
    readonly TextBox notes=new(){Dock=DockStyle.Fill,Multiline=true,ReadOnly=true,ScrollBars=ScrollBars.Vertical,BorderStyle=BorderStyle.None,Visible=false};
    readonly ProgressBar progress=new(){Dock=DockStyle.Bottom,Height=6,Visible=false};
    readonly Button action=DashboardWindow.ActionButton("Nach Updates suchen",()=>{});
    readonly CancellationTokenSource lifetime=new();
    AppUpdate? update;string? downloaded;bool busy;
    internal UpdateWindow(bool demo=false,Func<bool>? canInstall=null){
        Text="simplebanking aktualisieren";ClientSize=new(590,160);Padding=new(20);StartPosition=FormStartPosition.CenterParent;state.MaximumSize=new(550,0);state.Text="Installiert: "+AppUpdater.Current.ToString(3);
        var beta=new CheckBox{Text="Beta-Versionen berücksichtigen",Checked=true,AutoSize=true};
        TransientScrollbar.Attach(notes);
        var footer=new FlowLayoutPanel{Dock=DockStyle.Bottom,AutoSize=true,Padding=new(0,12,0,0)};footer.Controls.AddRange([action,beta]);Controls.AddRange([notes,state,progress,footer]);
        action.Click+=async(_,_)=>{
            if(busy)return;busy=true;action.Enabled=false;beta.Enabled=false;
            try{
                if(downloaded!=null&&update!=null){
                    await AppUpdater.Verify(downloaded,update,lifetime.Token);
                    if(canInstall?.Invoke()==false){state.Text="Bitte den laufenden Bankvorgang abschließen. Das Update ist bereits heruntergeladen.";return;}
                    Process.Start(new ProcessStartInfo(downloaded){UseShellExecute=true});Application.Exit();return;
                }
                if(update==null){
                    state.Text="Versionen werden geprüft …";update=demo?null:await AppUpdater.Check(beta.Checked,lifetime.Token);if(IsDisposed)return;
                    state.Text=demo?"Demo · Keine Netzwerkanfrage. Installiert: "+AppUpdater.Current.ToString(3):update==null?"Du verwendest die aktuelle Version dieses Kanals.":"Neue Version: "+update.Tag;
                    notes.Text=update?.Notes??"";notes.Visible=update!=null;ClientSize=new(590,update==null?160:400);action.Text=update==null?"Erneut prüfen":"Update herunterladen";
                }else{
                    progress.Visible=true;state.Text="Update wird heruntergeladen …";
                    downloaded=await AppUpdater.Download(update,new Progress<int>(value=>{if(!IsDisposed)progress.Value=value;}),lifetime.Token);if(IsDisposed)return;
                    state.Text="Download geprüft. Das Setup führt dich durch die Aktualisierung.";action.Text="App schließen und installieren";
                }
            }catch(OperationCanceledException){}catch(Exception ex){if(!IsDisposed)state.Text=ex is InvalidDataException?ex.Message:"Update nicht verfügbar. Bitte später erneut versuchen.";}
            finally{busy=false;if(!IsDisposed){action.Enabled=true;beta.Enabled=update==null;}}
        };
        FormClosed+=(_,_)=>lifetime.Cancel();
    }
    protected override void Dispose(bool disposing){if(disposing)lifetime.Cancel();base.Dispose(disposing);}
}
internal sealed partial class MainWindow
{
    internal void OpenUpdater(){using var window=new UpdateWindow(demo,()=>!bankBusy);window.ShowDialog(this);}
}
