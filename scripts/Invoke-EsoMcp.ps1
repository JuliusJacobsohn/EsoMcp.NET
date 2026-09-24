param(
    [Parameter(Mandatory)][string]$ToolName,
    [string]$ArgumentsJson = '{}',
    [string]$ServerDll = (Join-Path $PSScriptRoot '../src/EsoMcp.Server/bin/Release/net10.0/eso-mcp.dll'),
    [string]$ConfigPath = (Join-Path $env:LOCALAPPDATA 'EsoMcp/settings.json')
)

$start = [System.Diagnostics.ProcessStartInfo]::new('dotnet')
$start.ArgumentList.Add([System.IO.Path]::GetFullPath($ServerDll))
$start.ArgumentList.Add('--config')
$start.ArgumentList.Add([System.IO.Path]::GetFullPath($ConfigPath))
$start.UseShellExecute = $false
$start.CreateNoWindow = $true
$start.RedirectStandardInput = $true
$start.RedirectStandardOutput = $true
$start.RedirectStandardError = $true
$process = [System.Diagnostics.Process]::Start($start)
try {
    function Send-Request($id, $method, $parameters) {
        $request = @{ jsonrpc = '2.0'; id = $id; method = $method; params = $parameters } |
            ConvertTo-Json -Depth 32 -Compress
        $process.StandardInput.WriteLine($request)
        $process.StandardInput.Flush()
        do {
            $line = $process.StandardOutput.ReadLine()
            if ($null -eq $line) { throw "MCP server exited: $($process.StandardError.ReadToEnd())" }
            $response = $line | ConvertFrom-Json -Depth 32
        } until ($response.id -eq $id)
        if ($response.error) { throw ($response.error | ConvertTo-Json -Depth 8) }
        return $response.result
    }
    $null = Send-Request 1 'initialize' @{
        protocolVersion = '2025-06-18'; capabilities = @{};
        clientInfo = @{ name = 'EsoMcp.PowerShell'; version = '1.0' }
    }
    $process.StandardInput.WriteLine('{"jsonrpc":"2.0","method":"notifications/initialized"}')
    $arguments = $ArgumentsJson | ConvertFrom-Json -AsHashtable -Depth 32
    $result = Send-Request 2 'tools/call' @{ name = $ToolName; arguments = $arguments }
    if ($result.isError) { throw ($result.content | ConvertTo-Json -Depth 8) }
    $result.content | Where-Object type -eq 'text' | ForEach-Object text
}
finally {
    $process.StandardInput.Close()
    if (-not $process.WaitForExit(1000)) { $process.Kill() }
    $process.Dispose()
}
