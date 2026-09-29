using System.Security.Cryptography;
using System.Text;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Win32.SafeHandles;

namespace SimpleBanking;

internal sealed class YaxiHandle : SafeHandleZeroOrMinusOneIsInvalid
{
    public YaxiHandle() : base(true) { }
    protected override bool ReleaseHandle() { NativeYaxi.Destroy(handle); return true; }
}
internal static class NativeYaxi
{
    [DllImport("sb_yaxi", EntryPoint = "sb_yaxi_create", CallingConvention = CallingConvention.Cdecl)]
    internal static extern YaxiHandle Create([MarshalAs(UnmanagedType.LPUTF8Str)] string url);
    [DllImport("sb_yaxi", EntryPoint = "sb_yaxi_destroy", CallingConvention = CallingConvention.Cdecl)]
    internal static extern void Destroy(IntPtr handle);
    [DllImport("sb_yaxi", EntryPoint = "sb_yaxi_call", CallingConvention = CallingConvention.Cdecl)]
    internal static extern IntPtr Call(YaxiHandle handle, [MarshalAs(UnmanagedType.LPUTF8Str)] string request);
    [DllImport("sb_yaxi", EntryPoint = "sb_yaxi_free", CallingConvention = CallingConvention.Cdecl)]
    internal static extern void Free(IntPtr pointer);
}

public sealed class YaxiException(string type, string message, bool outcomeUnknown = false) : Exception(message)
{
    public string ErrorType { get; } = type;
    public bool PaymentOutcomeUnknown { get; } = outcomeUnknown;
}

// One instance per bank connection; all native calls for that connection are serialized.
public sealed class YaxiClient : IDisposable
{
    readonly YaxiHandle handle;
    readonly SemaphoreSlim queue = new(1, 1);
    bool disposed;
    public YaxiClient(bool integration = true)
    {
        handle = NativeYaxi.Create(integration ? "https://integration.yaxi.tech" : "");
        if (handle.IsInvalid) { handle.Dispose(); throw new InvalidOperationException("Yaxi-Client konnte nicht initialisiert werden."); }
    }
    internal JsonObject Invoke(JsonObject request)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        var ptr = NativeYaxi.Call(handle, request.ToJsonString());
        if (ptr == IntPtr.Zero) throw new YaxiException("BridgeError", "Yaxi lieferte keine Antwort.");
        try
        {
            return JsonNode.Parse(Marshal.PtrToStringUTF8(ptr) ?? "")?.AsObject()
                ?? throw new YaxiException("BridgeError", "Ungültige Yaxi-Antwort.");
        }
        finally { NativeYaxi.Free(ptr); }
    }
    public async Task<JsonObject> CallAsync(JsonObject request, CancellationToken cancellation = default)
    {
        await queue.WaitAsync(cancellation);
        try
        {
            cancellation.ThrowIfCancellationRequested();
            // The synchronous SDK has no cancellation ABI. Never free its client while a request runs.
            var result = await Task.Run(() => Invoke(request));
            if (result["kind"]?.GetValue<string>() == "error")
                throw new YaxiException(result["type"]?.GetValue<string>() ?? "Unknown",
                    result["userMessage"]?.GetValue<string>() ?? result["message"]?.GetValue<string>() ?? "Bankanfrage fehlgeschlagen.");
            return result;
        }
        finally { queue.Release(); }
    }
    public void Dispose()
    {
        queue.Wait();
        try { if (!disposed) { disposed = true; handle.Dispose(); } }
        finally { queue.Release(); }
    }
}

public sealed record YaxiSettings(bool Integration = true);
public sealed record BankConnection(string Id, string ProviderId, string Name, string UserId, string Password, int[]? ConnectionData = null,DateTimeOffset? ConnectionDataUpdatedAt=null);

public sealed class TicketClient : IDisposable
{
    public TicketClient(YaxiSettings settings) { }
    public Task<string> IssueAsync(string service, JsonNode? data, CancellationToken cancellation)
    {
        cancellation.ThrowIfCancellationRequested();
        return Task.FromResult(LocalTicketMaker.Issue(EmbeddedYaxiKeys.Load(), service, data, false));
    }
    // Activation must come from the actual simplesend licensing flow, not a settings checkbox.
    public Task<string> IssueTransferAsync(bool simplesendActivated, CancellationToken cancellation)
    {
        cancellation.ThrowIfCancellationRequested();
        return Task.FromResult(LocalTicketMaker.Issue(EmbeddedYaxiKeys.Load(), "Transfer", null, simplesendActivated));
    }
    internal static JsonObject DecodeJwt(string jwt)
    {
        var parts = jwt.Split('.'); if (parts.Length != 3) throw new InvalidDataException("Ungültiges JWT.");
        var b64 = parts[1].Replace('-', '+').Replace('_', '/'); b64 += new string('=', (4 - b64.Length % 4) % 4);
        return JsonNode.Parse(Convert.FromBase64String(b64))?.AsObject() ?? throw new InvalidDataException("JWT ohne Daten.");
    }
    public void Dispose() { }
}

internal sealed record YaxiKeys(string KeyId, string SecretBase64, string? TransferKeyId = null, string? TransferSecretBase64 = null);
internal static class EmbeddedYaxiKeys
{
    public static YaxiKeys Load()
    {
        using var stream = typeof(EmbeddedYaxiKeys).Assembly.GetManifestResourceStream("SimpleBanking.YaxiKeys.json");
        if (stream == null) throw new InvalidOperationException("In diesem Build fehlen die Yaxi-Schlüssel. Bitte beim Erstellen der App hinterlegen.");
        return JsonSerializer.Deserialize<YaxiKeys>(stream) ?? throw new InvalidOperationException("Yaxi-Buildkonfiguration fehlt.");
    }
}
internal static class LocalTicketMaker
{
    static string Base64Url(byte[] bytes) => Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
    public static string Issue(YaxiKeys keys, string service, JsonNode? data, bool simplesendActivated,
        DateTimeOffset? timestamp = null, Guid? ticketId = null)
    {
        if (service is not ("Accounts" or "Balances" or "Transactions" or "Transfer")) throw new ArgumentException("Unbekannter Yaxi-Dienst.");
        if (service != "Transactions" && data != null) throw new ArgumentException("Dieser Dienst erwartet keine Ticketdaten.");
        if (service == "Transactions" && (data?["account"]?["iban"] == null || data?["account"]?["currency"] == null || data?["range"] == null))
            throw new ArgumentException("Kontobezug und Zeitraum fehlen.");
        bool transfer = service == "Transfer" && simplesendActivated;
        var kid = transfer ? keys.TransferKeyId : keys.KeyId;
        var secret = transfer ? keys.TransferSecretBase64 : keys.SecretBase64;
        if (string.IsNullOrWhiteSpace(kid) || string.IsNullOrWhiteSpace(secret)) throw new InvalidOperationException("Yaxi-Schlüsselpaar fehlt im Build.");
        byte[] key;
        try { key = Convert.FromBase64String(secret); }
        catch (FormatException) { throw new InvalidOperationException("Yaxi-Secret muss Base64-kodiert sein."); }
        try
        {
            if (key.Length == 0) throw new InvalidOperationException("Leeres Yaxi-Secret.");
            var now = (timestamp ?? DateTimeOffset.UtcNow).ToUnixTimeSeconds();
            var header = new JsonObject { ["alg"] = "HS256", ["kid"] = kid, ["typ"] = "JWT" };
            var payload = new JsonObject { ["data"] = new JsonObject { ["data"] = data?.DeepClone(),
                ["id"] = (ticketId ?? Guid.NewGuid()).ToString("D"), ["service"] = service }, ["exp"] = now + 600, ["iat"] = now };
            var input = Base64Url(Encoding.UTF8.GetBytes(header.ToJsonString())) + "." + Base64Url(Encoding.UTF8.GetBytes(payload.ToJsonString()));
            return input + "." + Base64Url(HMACSHA256.HashData(key, Encoding.UTF8.GetBytes(input)));
        }
        finally { CryptographicOperations.ZeroMemory(key); }
    }
}

public sealed record BankReply(bool Canceled, string? Value = null);
public sealed class BankFlow(YaxiClient client)
{
    // The UI must explicitly handle every confirmation, including VoP and account selection.
    public async Task<JsonObject> RunAsync(JsonObject start, string service,
        Func<JsonObject, Task<BankReply>> prompt, CancellationToken cancellation)
    {
        var ticket = start["ticket"]?.GetValue<string>() ?? throw new ArgumentException("Ticket missing");
        var result = await client.CallAsync(start, cancellation);
        for (int step = 0; step < 100; step++)
        {
            var kind = result["kind"]?.GetValue<string>();
            if (kind == "result")
            {
                var expected = TicketClient.DecodeJwt(ticket)["data"]?["id"]?.GetValue<string>();
                var actual = TicketClient.DecodeJwt(result["jwt"]!.GetValue<string>())["data"]?["ticketId"]?.GetValue<string>();
                if (expected == null || expected != actual) throw new YaxiException("Protocol", "Bankantwort gehört nicht zu dieser Anfrage.");
                return result;
            }
            if (kind is not ("dialog" or "redirect" or "redirectHandle")) throw new YaxiException("Protocol", "Unbekannte Bankantwort.");
            var answer = await prompt(result);
            if (answer.Canceled) throw new OperationCanceledException("Bankfreigabe abgebrochen.");
            var input = result["input"];
            var confirmation = kind != "dialog" || input?["kind"]?.GetValue<string>() == "confirmation";
            var context = (kind == "dialog" ? input?["context"] : result["context"])?.DeepClone()
                ?? throw new YaxiException("Protocol", "Freigabekontext fehlt.");
            result = await client.CallAsync(new JsonObject { ["op"] = confirmation ? "confirm" : "respond", ["service"] = service,
                ["ticket"] = ticket, ["context"] = context, ["input"] = answer.Value ?? "" }, cancellation);
        }
        throw new TimeoutException("Zu viele Freigabeschritte. Bitte Bankstatus prüfen.");
    }
}
