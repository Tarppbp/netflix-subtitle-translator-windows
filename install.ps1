param([ValidateSet('qwen3:4b-instruct','translategemma:4b')][string]$Model='qwen3:4b-instruct', [switch]$NoLaunch)
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'engine.ps1')
$version = '0.40.17'
$checksum = '3dd80f6e3a9699f2eadf4ac418f00529cee594df9557541806eda1f1f3f5588a'
if (!(Test-Path -LiteralPath $ollamaExe)) {
    $archive = Join-Path $env:TEMP "netflix-subtitle-ollama-$version.zip"
    if (!(Test-Path -LiteralPath $archive) -or (Get-FileHash -LiteralPath $archive).Hash -ne $checksum) {
        $url = "https://github.com/ollama/ollama/releases/download/v$version/ollama-windows-amd64.zip"
        & curl.exe --fail --show-error --location --retry 2 --output $archive $url
        if ($LASTEXITCODE -ne 0) { throw 'Ollama download failed.' }
    }
    if ((Get-FileHash -LiteralPath $archive).Hash -ne $checksum) { throw 'Ollama SHA-256 verification failed.' }
    $target = Split-Path -Parent $ollamaExe
    $staging = Join-Path $localRuntime ('extract-' + [Guid]::NewGuid().ToString('N'))
    New-Item -ItemType Directory -Force $staging | Out-Null
    & tar.exe -xf $archive -C $staging
    if ($LASTEXITCODE -ne 0 -or !(Test-Path -LiteralPath (Join-Path $staging 'ollama.exe'))) { throw 'Ollama extraction failed.' }
    Move-Item -LiteralPath $staging -Destination $target
}
& (Join-Path $PSScriptRoot 'build.ps1')
Start-LocalOllama
$existing = (Invoke-RestMethod "http://$localHostAddress/api/tags" -TimeoutSec 4).models
if ($Model -notin $existing.name) {
    & $ollamaExe pull $Model
    if ($LASTEXITCODE -ne 0) { throw "Model download failed: $Model" }
}
if (!$NoLaunch) { & (Join-Path $PSScriptRoot 'start.ps1') }
