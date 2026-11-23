$ErrorActionPreference = 'Stop'
$localRuntime = Join-Path $PSScriptRoot '.runtime'
$ollamaExe = Join-Path $localRuntime 'ollama\ollama.exe'
$localHostAddress = '127.0.0.1:17434'

function Start-LocalOllama {
    if (!(Test-Path -LiteralPath $ollamaExe)) { throw 'Ollama is missing. Run Install.cmd.' }
    $env:OLLAMA_HOST = $localHostAddress
    $env:OLLAMA_MODELS = Join-Path $localRuntime 'models'
    $env:OLLAMA_NO_CLOUD = '1'
    $env:OLLAMA_NUM_PARALLEL = '1'
    $env:OLLAMA_MAX_LOADED_MODELS = '1'
    $env:OLLAMA_MAX_QUEUE = '8'
    $env:OLLAMA_CONTEXT_LENGTH = '2048'
    $env:OLLAMA_ORIGINS = 'chrome-extension://nphopjbohdkdjapgjmgnpbpmmcgmdimo'
    $endpoint = "http://$localHostAddress/api/version"
    $localOllama = Get-CimInstance Win32_Process -Filter "Name = 'ollama.exe'" -ErrorAction SilentlyContinue | 
        Where-Object { $_.ExecutablePath -eq $ollamaExe -and $_.CommandLine -match '\bserve\b' }
    if ($localOllama) {
        try { $null = Invoke-RestMethod $endpoint -TimeoutSec 2; return } catch {}
    }
    try {
        $null = Invoke-RestMethod $endpoint -TimeoutSec 2
        throw 'The local Ollama port is occupied by a different process.'
    } catch {
        if ($_.Exception.Message -eq 'The local Ollama port is occupied by a different process.') { throw }
    }
    $server = Start-Process -FilePath $ollamaExe -ArgumentList 'serve' -WindowStyle Hidden -PassThru `
        -RedirectStandardOutput (Join-Path $localRuntime 'ollama-stdout.log') `
        -RedirectStandardError (Join-Path $localRuntime 'ollama-stderr.log')
    for ($attempt = 0; $attempt -lt 24; $attempt++) {
        if ($server.HasExited) { throw 'Ollama did not start. Check .runtime\ollama-stderr.log.' }
        try { $null = Invoke-RestMethod $endpoint -TimeoutSec 1; return } catch {}
        Start-Sleep -Milliseconds 400
    }
    throw 'Ollama start timed out.'
}
