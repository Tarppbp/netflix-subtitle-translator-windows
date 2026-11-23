param([switch]$EngineOnly)
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'engine.ps1')
Start-LocalOllama
if ($EngineOnly) { return }
$appPath = Join-Path $PSScriptRoot 'bin\NetflixSubtitles.exe'
if (!(Test-Path -LiteralPath $appPath)) { & (Join-Path $PSScriptRoot 'build.ps1') }
Start-Process -FilePath $appPath
