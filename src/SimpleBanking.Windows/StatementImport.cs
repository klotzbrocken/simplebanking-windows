using System.Globalization;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Linq;
using System.Security.Cryptography;
using System.Text;
namespace SimpleBanking;
internal static class StatementImport
{
    internal static List<Entry> Parse(string content,Account account,bool ofx) => ofx?Ofx(content,account):Camt(content,account);
    static string? Child(XElement element,string name)=>element.Elements().FirstOrDefault(e=>e.Name.LocalName==name)?.Value;
    static XElement? Node(XElement element,string name)=>element.Descendants().FirstOrDefault(e=>e.Name.LocalName==name);
    static DateOnly Date(string? text){if(text==null||!DateOnly.TryParse(text.Length>=10?text[..10]:text,CultureInfo.InvariantCulture,out var date))throw new InvalidDataException("Buchungsdatum fehlt oder ist ungültig.");return date;}
    internal static List<Entry> Camt(string content,Account account){
        using var reader=XmlReader.Create(new StringReader(content),new XmlReaderSettings{DtdProcessing=DtdProcessing.Prohibit,XmlResolver=null,MaxCharactersInDocument=30_000_000});
        var doc=XDocument.Load(reader);var result=new List<Entry>();
        foreach(var statement in doc.Descendants().Where(e=>e.Name.LocalName=="Stmt")){
            var acct=statement.Elements().FirstOrDefault(e=>e.Name.LocalName=="Acct");string? iban=acct==null?null:Node(acct,"IBAN")?.Value;
            if(iban!=null && BankingRules.NormalizeIban(iban)!=account.Iban)throw new InvalidDataException("Die Datei enthält ein anderes Konto. Bitte das passende Konto auswählen.");
            foreach(var item in statement.Elements().Where(e=>e.Name.LocalName=="Ntry")) {
                var amount=item.Elements().FirstOrDefault(e=>e.Name.LocalName=="Amt")??throw new InvalidDataException("Betrag fehlt.");
                string currency=amount.Attribute("Ccy")?.Value??(acct==null?null:Child(acct,"Ccy"))??account.Currency;
                if(currency!=account.Currency)throw new InvalidDataException("Die Währung stimmt nicht mit dem Konto überein.");
                decimal value=decimal.Parse(amount.Value,CultureInfo.InvariantCulture);if(Child(item,"CdtDbtInd")=="DBIT")value=-value;
                var dateNode=item.Elements().FirstOrDefault(e=>e.Name.LocalName=="BookgDt");var valueNode=item.Elements().FirstOrDefault(e=>e.Name.LocalName=="ValDt");var date=Date(dateNode?.Value??valueNode?.Value);
                DateOnly? valueDate=valueNode!=null?Date(valueNode.Value):null;
                var details=item.Descendants().Where(e=>e.Name.LocalName=="TxDtls").ToArray();
                if(details.Length>1)throw new InvalidDataException("Diese Datei enthält Sammelbuchungen mit mehreren Einzelposten. Bitte einen Export mit Einzelbuchungen verwenden.");
                var party=Node(item,value<0?"Cdtr":"Dbtr");string name=party==null?"Unbekannt":Node(party,"Nm")?.Value??"Unbekannt";
                string memo=string.Join(" ",item.Descendants().Where(e=>e.Name.LocalName=="Ustrd").Select(e=>e.Value));
                string reference=Node(item,"EndToEndId")?.Value??"";string status=Node(item,"Sts")?.Value??"BOOK";
                string partyIban=Node(item,value<0?"CdtrAcct":"DbtrAcct") is {} partyAccount?Node(partyAccount,"IBAN")?.Value??"":"";
                result.Add(new("",account.Id,date,value,currency,name,memo,"Unkategorisiert",Pending:status=="PDNG",ValueDate:valueDate,CounterpartyIban:partyIban,AdditionalInformation:Node(item,"AddtlNtryInf")?.Value??"",PurposeCode:Node(item,"Purp")?.Value??"",BankCounterpartyName:name,EndToEndId:reference=="NOTPROVIDED"?"":reference));
            }
        }
        if(!doc.Descendants().Any(e=>e.Name.LocalName=="Stmt"))throw new InvalidDataException("Keine CAMT-Kontoauszüge gefunden.");return Identify(result);
    }
    static string Field(string text,string tag)=>Regex.Match(text,"<"+tag+@">\s*([^<\r\n]*)",RegexOptions.IgnoreCase).Groups[1].Value.Trim();
    internal static List<Entry> Ofx(string text,Account account){
        string currency=Field(text,"CURDEF");if(currency.Length==0)currency=account.Currency;if(currency!=account.Currency)throw new InvalidDataException("Die Währung stimmt nicht mit dem Konto überein.");
        string id=Field(text,"ACCTID");if(id.StartsWith("DE",StringComparison.OrdinalIgnoreCase)&&BankingRules.NormalizeIban(id)!=account.Iban)throw new InvalidDataException("Die Datei enthält ein anderes Konto.");
        var blocks=Regex.Matches(text,@"<STMTTRN>(.*?)(?:</STMTTRN>|(?=<STMTTRN>)|$)",RegexOptions.Singleline|RegexOptions.IgnoreCase);
        if(blocks.Count==0)throw new InvalidDataException("Keine OFX/QFX-Buchungen gefunden.");var result=new List<Entry>();
        foreach(Match match in blocks){string block=match.Groups[1].Value,dateText=Field(block,"DTPOSTED");if(dateText.Length<8||!DateOnly.TryParseExact(dateText[..8],"yyyyMMdd",CultureInfo.InvariantCulture,DateTimeStyles.None,out var date))throw new InvalidDataException("OFX-Buchungsdatum ungültig.");
            decimal value=decimal.Parse(Field(block,"TRNAMT").Replace(',','.'),CultureInfo.InvariantCulture);string name=System.Net.WebUtility.HtmlDecode(Field(block,"NAME"));
            result.Add(new("",account.Id,date,value,currency,name,System.Net.WebUtility.HtmlDecode(Field(block,"MEMO")),"Unkategorisiert",BankCounterpartyName:name,EndToEndId:Field(block,"FITID")));
        }return Identify(result);
    }
    internal static string Fingerprint(Entry e)=>e.AccountId+"|"+e.Date.ToString("yyyy-MM-dd")+"|"+e.Amount.ToString(CultureInfo.InvariantCulture)+"|"+e.Currency+"|"+e.Merchant.Trim()+"|"+e.Purpose.Trim();
    static List<Entry> Identify(List<Entry> entries){var counts=new Dictionary<string,int>();return entries.Select(e=>{string key=e.EndToEndId.Length>0?e.AccountId+"|"+e.EndToEndId:Fingerprint(e);int occurrence=counts.GetValueOrDefault(key)+1;counts[key]=occurrence;return e with{Id="import/"+Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(key)))+"/"+occurrence};}).ToList();}
    internal static List<Entry> NewEntries(IEnumerable<Entry> existing,IEnumerable<Entry> incoming){var old=existing.ToArray();var ids=old.Select(e=>e.AccountId+"/"+e.Id).ToHashSet();var counts=old.GroupBy(Fingerprint).ToDictionary(g=>g.Key,g=>g.Count());var seen=new Dictionary<string,int>();var result=new List<Entry>();foreach(var e in incoming){string key=Fingerprint(e);int n=seen.GetValueOrDefault(key)+1;seen[key]=n;if(ids.Contains(e.AccountId+"/"+e.Id)||n<=counts.GetValueOrDefault(key))continue;result.Add(e);}return result;}
}
internal sealed partial class MainWindow
{
    void ImportStatements(){
        if(!RequireUnlocked()||SelectedAccount is not {} account)return;
        using var dialog=new OpenFileDialog{Filter="Kontoauszüge|*.xml;*.ofx;*.qfx|CAMT 053|*.xml|OFX / QFX|*.ofx;*.qfx",Multiselect=false};if(dialog.ShowDialog(this)!=DialogResult.OK)return;
        try{
            string text=File.ReadAllText(dialog.FileName);var parsed=StatementImport.Parse(text,account,Path.GetExtension(dialog.FileName).ToLowerInvariant() is ".ofx" or ".qfx");var additions=StatementImport.NewEntries(data.Entries,parsed);
            using var preview=new Form{Text="Umsätze importieren · "+account.Name,ClientSize=new Size(700,460),StartPosition=FormStartPosition.CenterParent,Font=Font};var grid=DashboardWindow.Table(["Datum","Händler","Betrag","Zweck"]);foreach(var e in additions)grid.Rows.Add(e.Date.ToString("dd.MM.yyyy"),e.Merchant,UiFormat.Money(e.Amount,e.Currency),e.Purpose);preview.Controls.Add(grid);
            var apply=DashboardWindow.ActionButton(additions.Count+" Umsätze importieren",()=>preview.DialogResult=DialogResult.OK);apply.Dock=DockStyle.Bottom;preview.Controls.Add(apply);
            if(preview.ShowDialog(this)!=DialogResult.OK)return;
            data.Entries.AddRange(additions);ApplyAnalysisRules(new HashSet<string>{account.Id},true);Save();Populate();status.Text=$"{additions.Count} importiert · {parsed.Count-additions.Count} bereits vorhanden";
        }catch(Exception e) when(e is IOException or XmlException or FormatException or InvalidDataException){MessageBox.Show(this,e.Message,"Import nicht abgeschlossen");}
    }
}
