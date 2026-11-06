param([string]$Executable,[string]$Assembly)
$ErrorActionPreference='Stop'
if (!$Executable) { throw 'Executable is required (installed EXE or dotnet host).' }
foreach ($demo in @($true,$false)) {
    $info=[Diagnostics.ProcessStartInfo]::new($Executable)
    $info.UseShellExecute=$false
    $info.CreateNoWindow=$true
    $info.RedirectStandardInput=$true
    $info.RedirectStandardOutput=$true
    $info.RedirectStandardError=$true
    if ($Assembly) { $info.ArgumentList.Add($Assembly) }
    $info.ArgumentList.Add('--mcp')
    if ($demo) { $info.ArgumentList.Add('--demo') }
    $info.Environment.Remove('SIMPLEBANKING_TOKEN') | Out-Null
    $info.Environment.Remove('SIMPLEBANKING_MCP_TOKEN') | Out-Null
    $process=[Diagnostics.Process]::Start($info)
    try {
        $stdout=$process.StandardOutput.ReadToEndAsync()
        $stderr=$process.StandardError.ReadToEndAsync()
        $messages=@(
            @{jsonrpc='2.0';id=1;method='initialize';params=@{protocolVersion='2025-06-18';capabilities=@{};clientInfo=@{name='simplebanking-process-test';version='1'}}},
            @{jsonrpc='2.0';method='notifications/initialized'},
            @{jsonrpc='2.0';id=2;method='tools/list'},
            @{jsonrpc='2.0';id=3;method='tools/call';params=@{name='get_accounts';arguments=@{}}}
        )
        foreach ($message in $messages) { $process.StandardInput.WriteLine(($message | ConvertTo-Json -Depth 12 -Compress)) }
        $process.StandardInput.Close()
        if (!$process.WaitForExit(20000)) { throw 'MCP process did not exit on EOF.' }
        if ($process.ExitCode -ne 0) { throw 'MCP process failed.' }
        $lines=@($stdout.GetAwaiter().GetResult() -split '\r?\n' | Where-Object { $_ })
        if ($lines.Count -ne 3) { throw 'Unexpected output or notification response on stdout.' }
        $responses=@($lines | ForEach-Object { $_ | ConvertFrom-Json })
        if ($responses[0].result.protocolVersion -ne '2025-06-18') { throw 'Initialization failed.' }
        if ($demo) {
            if (!$responses[1].result.tools -or $responses[2].result.isError) { throw 'Demo tool query failed.' }
            $accounts=@($responses[2].result.content[0].text | ConvertFrom-Json)
            if (!$accounts.Count -or $accounts[0].id -notlike 'demo-*') { throw 'Expected synthetic demo accounts.' }
        } else {
            if (!$responses[1].error -or !$responses[2].result.isError) { throw 'Unauthenticated live access accepted.' }
        }
        Write-Output "MCP subprocess passed (demo=$demo, clean stdout, EOF shutdown)."
    } finally {
        if (!$process.HasExited) { $process.Kill($true) }
        $process.Dispose()
    }
}
