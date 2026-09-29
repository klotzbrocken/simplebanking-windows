using System.Globalization;
using System.Xml.Linq;
namespace SimpleBanking;
internal static class StatementExport
{
    internal static string Ofx(Account account,IEnumerable<Entry> input){
        var entries=input.Where(e=>e.AccountId==account.Id&&e.Currency==account.Currency&&!e.Pending).OrderBy(e=>e.Date).ToArray();
        string Date(DateOnly value)=>value.ToString("yyyyMMdd",CultureInfo.InvariantCulture)+"120000";
        var transactions=new XElement("BANKTRANLIST",new XElement("DTSTART",Date(entries.FirstOrDefault()?.Date??DateOnly.FromDateTime(DateTime.Today))),new XElement("DTEND",Date(entries.LastOrDefault()?.Date??DateOnly.FromDateTime(DateTime.Today))),entries.Select(e=>new XElement("STMTTRN",new XElement("TRNTYPE",e.Amount<0?"DEBIT":"CREDIT"),new XElement("DTPOSTED",Date(e.Date)),new XElement("TRNAMT",e.Amount.ToString("0.00",CultureInfo.InvariantCulture)),new XElement("FITID",e.Id),new XElement("NAME",e.OriginalMerchant??e.Merchant),new XElement("MEMO",e.Purpose))));
        var statement=new XElement("STMTRS",new XElement("CURDEF",account.Currency),new XElement("BANKACCTFROM",new XElement("BANKID",account.Iban.Length>=12?account.Iban.Substring(4,8):""),new XElement("ACCTID",account.Iban),new XElement("ACCTTYPE","CHECKING")),transactions);
        if(account.BalanceKnown)statement.Add(new XElement("LEDGERBAL",new XElement("BALAMT",account.Balance.ToString("0.00",CultureInfo.InvariantCulture)),new XElement("DTASOF",Date(DateOnly.FromDateTime(account.BalanceFetchedAt?.LocalDateTime??DateTime.Today)))));
        var root=new XElement("OFX",new XElement("SIGNONMSGSRSV1",new XElement("SONRS",new XElement("STATUS",new XElement("CODE",0),new XElement("SEVERITY","INFO")),new XElement("DTSERVER",Date(DateOnly.FromDateTime(DateTime.Today))),new XElement("LANGUAGE","DEU"))),new XElement("BANKMSGSRSV1",new XElement("STMTTRNRS",new XElement("TRNUID","1"),new XElement("STATUS",new XElement("CODE",0),new XElement("SEVERITY","INFO")),statement)));
        return "<?xml version=\"1.0\" encoding=\"UTF-8\"?>\r\n<?OFX OFXHEADER=\"200\" VERSION=\"211\" SECURITY=\"NONE\" OLDFILEUID=\"NONE\" NEWFILEUID=\"NONE\"?>\r\n"+root;
    }
}
internal sealed partial class MainWindow
{
    void ExportOfx(){
        if(!RequireUnlocked()||SelectedAccount is not {} account)return;using var dialog=new SaveFileDialog{Filter="OFX-Kontoauszug|*.ofx",FileName="umsaetze.ofx"};if(dialog.ShowDialog(this)!=DialogResult.OK)return;
        try{File.WriteAllText(dialog.FileName,StatementExport.Ofx(account,data.Entries),new System.Text.UTF8Encoding(false));status.Text="OFX exportiert";}catch(IOException){MessageBox.Show(this,"Die Datei konnte nicht gespeichert werden.");}
    }
}
