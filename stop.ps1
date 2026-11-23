$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'engine.ps1')
Get-CimInstance Win32_Process -Filter "Name = 'ollama.exe'" |
    Where-Object { $_.ExecutablePath -eq $ollamaExe -and $_.CommandLine -match '\bserve\b' } |
    ForEach-Object { Stop-Process -Id $_.ProcessId }
