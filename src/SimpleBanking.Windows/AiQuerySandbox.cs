using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
namespace SimpleBanking;

// Only redacted rows for the selected account enter this ephemeral database.
// SQLite's authorizer and VM budget enforce isolation independently of the prompt.
internal static class AiQuerySandbox
{
    internal const string Schema="llm_tx(date TEXT, amount REAL, currency TEXT, merchant TEXT, purpose TEXT, category TEXT). Only booked transactions of the selected account. No IBAN or other tables. SQLite SELECT only, no recursive queries; limit 200. Amount is signed; expenses are negative. Dates use yyyy-MM-dd.";
    internal static string Validate(string sql){
        sql=sql.Trim();if(sql.StartsWith("```")){int start=sql.IndexOf('\n');if(start>=0)sql=sql[(start+1)..].TrimEnd('`','\r','\n',' ');}sql=sql.Trim().TrimEnd(';').Trim();
        if(sql.Length==0||sql.Length>4000||sql.Contains(';')||!Regex.IsMatch(sql,@"\ASELECT\b",RegexOptions.IgnoreCase)||!Regex.IsMatch(sql,@"\bllm_tx\b",RegexOptions.IgnoreCase))throw new InvalidDataException("Nur eine SELECT-Abfrage auf llm_tx ist erlaubt.");
        return sql;
    }
    internal static List<Dictionary<string,object?>> Execute(IEnumerable<Entry> entries,string accountId,string sql,CancellationToken cancellation=default){
        sql=Validate(sql);IntPtr db=IntPtr.Zero;int result=Open(":memory:",out db);
        if(result!=0){if(db!=IntPtr.Zero)Close(db);throw new InvalidOperationException("Lokale Auswertung nicht verfügbar.");}
        Authorize? authorize=null;Progress? progress=null;
        try{
            Execute(db,"CREATE TABLE llm_tx(date TEXT,amount REAL,currency TEXT,merchant TEXT,purpose TEXT,category TEXT);");
            Execute(db,"BEGIN;");
            Check(Prepare(db,"INSERT INTO llm_tx VALUES(?,?,?,?,?,?)",-1,out var insert,out _));
            try{foreach(var entry in entries.Where(e=>e.AccountId==accountId&&!e.Pending&&e.Date<=DateOnly.FromDateTime(DateTime.Today))){
                cancellation.ThrowIfCancellationRequested();
                BindText(insert,1,entry.Date.ToString("yyyy-MM-dd"),-1,new IntPtr(-1));BindDouble(insert,2,(double)entry.Amount);
                string[] values=[entry.Currency,AiAssistant.Redact(entry.Merchant),AiAssistant.Redact(entry.Purpose),entry.Category];
                for(int i=0;i<values.Length;i++)BindText(insert,i+3,values[i],-1,new IntPtr(-1));
                if(Step(insert)!=101)throw new InvalidDataException("Lokale Auswertung konnte nicht vorbereitet werden.");Reset(insert);
            }}finally{Finalize(insert);}
            Execute(db,"COMMIT;");Execute(db,"PRAGMA query_only=ON;");
            var functions=new HashSet<string>(["sum","total","count","avg","min","max","abs","round","strftime","date","datetime","julianday","coalesce","ifnull","nullif","lower","upper","length","substr","substring","like","glob","trim","ltrim","rtrim"],StringComparer.OrdinalIgnoreCase);
            authorize=(_,action,a,b,_,_)=>action switch{21=>0,20 when Text(a)=="llm_tx"=>0,31 when functions.Contains(Text(b))=>0,_=>1};
            SetAuthorizer(db,authorize,IntPtr.Zero);int steps=0;var watch=Stopwatch.StartNew();
            progress=_=>cancellation.IsCancellationRequested||++steps>10000||watch.Elapsed>TimeSpan.FromSeconds(2)?1:0;SetProgress(db,1000,progress,IntPtr.Zero);
            Check(Prepare(db,"SELECT * FROM ("+sql+") LIMIT 200",-1,out var query,out _));
            try{
                if(ReadOnly(query)==0)throw new InvalidDataException("Schreibende Abfrage abgelehnt.");
                var rows=new List<Dictionary<string,object?>>();int bytes=0,step;
                while((step=Step(query))==100){
                    var row=new Dictionary<string,object?>();int columns=ColumnCount(query);if(columns>20)throw new InvalidDataException("Zu viele Ergebnisspalten.");
                    for(int i=0;i<columns;i++){
                        string name=Text(ColumnName(query,i));if(name.Length>100)throw new InvalidDataException("Ungültige Ergebnisspalte.");
                        object? value=ColumnType(query,i) switch{1=>ColumnInt64(query,i),2=>ColumnDouble(query,i),5=>null,_=>AiAssistant.Redact(Text(ColumnText(query,i)))};
                        row[name]=value;bytes+=Encoding.UTF8.GetByteCount(value?.ToString()??"");if(bytes>100000)throw new InvalidDataException("Abfrageergebnis zu groß.");
                    }rows.Add(row);
                }
                cancellation.ThrowIfCancellationRequested();if(step!=101)throw new InvalidDataException("Abfrage abgelehnt oder Zeitlimit erreicht.");return rows;
            }finally{Finalize(query);}
        }finally{Close(db);GC.KeepAlive(authorize);GC.KeepAlive(progress);}
    }
    static string Text(IntPtr value)=>Marshal.PtrToStringUTF8(value)??"";
    static void Check(int result){if(result!=0)throw new InvalidDataException("Diese Datenabfrage ist nicht erlaubt oder ungültig.");}
    static void Execute(IntPtr db,string sql){Check(Prepare(db,sql,-1,out var statement,out _));try{if(Step(statement)!=101)throw new InvalidDataException("Lokale Abfrage fehlgeschlagen.");}finally{Finalize(statement);}}
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]delegate int Authorize(IntPtr state,int action,IntPtr a,IntPtr b,IntPtr database,IntPtr trigger);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]delegate int Progress(IntPtr state);
    const string Dll="winsqlite3.dll";
    [DllImport(Dll,EntryPoint="sqlite3_open",CallingConvention=CallingConvention.Cdecl)]static extern int Open([MarshalAs(UnmanagedType.LPUTF8Str)]string filename,out IntPtr db);
    [DllImport(Dll,EntryPoint="sqlite3_close",CallingConvention=CallingConvention.Cdecl)]static extern int Close(IntPtr db);
    [DllImport(Dll,EntryPoint="sqlite3_prepare_v2",CallingConvention=CallingConvention.Cdecl)]static extern int Prepare(IntPtr db,[MarshalAs(UnmanagedType.LPUTF8Str)]string sql,int length,out IntPtr statement,out IntPtr tail);
    [DllImport(Dll,EntryPoint="sqlite3_step",CallingConvention=CallingConvention.Cdecl)]static extern int Step(IntPtr statement);
    [DllImport(Dll,EntryPoint="sqlite3_reset",CallingConvention=CallingConvention.Cdecl)]static extern int Reset(IntPtr statement);
    [DllImport(Dll,EntryPoint="sqlite3_finalize",CallingConvention=CallingConvention.Cdecl)]static extern int Finalize(IntPtr statement);
    [DllImport(Dll,EntryPoint="sqlite3_bind_text",CallingConvention=CallingConvention.Cdecl)]static extern int BindText(IntPtr statement,int index,[MarshalAs(UnmanagedType.LPUTF8Str)]string value,int bytes,IntPtr destructor);
    [DllImport(Dll,EntryPoint="sqlite3_bind_double",CallingConvention=CallingConvention.Cdecl)]static extern int BindDouble(IntPtr statement,int index,double value);
    [DllImport(Dll,EntryPoint="sqlite3_set_authorizer",CallingConvention=CallingConvention.Cdecl)]static extern int SetAuthorizer(IntPtr db,Authorize callback,IntPtr state);
    [DllImport(Dll,EntryPoint="sqlite3_progress_handler",CallingConvention=CallingConvention.Cdecl)]static extern void SetProgress(IntPtr db,int instructions,Progress callback,IntPtr state);
    [DllImport(Dll,EntryPoint="sqlite3_stmt_readonly",CallingConvention=CallingConvention.Cdecl)]static extern int ReadOnly(IntPtr statement);
    [DllImport(Dll,EntryPoint="sqlite3_column_count",CallingConvention=CallingConvention.Cdecl)]static extern int ColumnCount(IntPtr statement);
    [DllImport(Dll,EntryPoint="sqlite3_column_name",CallingConvention=CallingConvention.Cdecl)]static extern IntPtr ColumnName(IntPtr statement,int column);
    [DllImport(Dll,EntryPoint="sqlite3_column_type",CallingConvention=CallingConvention.Cdecl)]static extern int ColumnType(IntPtr statement,int column);
    [DllImport(Dll,EntryPoint="sqlite3_column_text",CallingConvention=CallingConvention.Cdecl)]static extern IntPtr ColumnText(IntPtr statement,int column);
    [DllImport(Dll,EntryPoint="sqlite3_column_double",CallingConvention=CallingConvention.Cdecl)]static extern double ColumnDouble(IntPtr statement,int column);
    [DllImport(Dll,EntryPoint="sqlite3_column_int64",CallingConvention=CallingConvention.Cdecl)]static extern long ColumnInt64(IntPtr statement,int column);
}
