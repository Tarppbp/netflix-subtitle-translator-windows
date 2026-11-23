$ErrorActionPreference = 'Stop'
$framework = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319'
$compiler = Join-Path $framework 'csc.exe'
if (!(Test-Path -LiteralPath $compiler)) { throw '.NET Framework 4.8 compiler is missing.' }
$wpf = Join-Path $framework 'WPF'
$references = @(
    (Join-Path $wpf 'PresentationCore.dll'),
    (Join-Path $wpf 'PresentationFramework.dll'),
    (Join-Path $wpf 'WindowsBase.dll'),
    (Join-Path $wpf 'UIAutomationClient.dll'),
    (Join-Path $wpf 'UIAutomationTypes.dll'),
    'System.Windows.Forms.dll', 'System.Drawing.dll', 'System.Net.Http.dll', 'System.Web.Extensions.dll', 'System.Xaml.dll'
) | ForEach-Object { '/reference:' + $_ }
$outDir = Join-Path $PSScriptRoot 'bin'
New-Item -ItemType Directory -Force $outDir | Out-Null
$reader = Join-Path $PSScriptRoot 'src\NetflixReader.cs'
if ($args -contains '-Benchmark') {
    & $compiler /nologo /codepage:65001 /target:winexe /optimize+ ("/out:" + (Join-Path $outDir 'Benchmark.exe')) $references (Join-Path $PSScriptRoot 'src\Engine.cs') (Join-Path $PSScriptRoot 'tests\Benchmark.cs')
} elseif ($args -contains '-Probe') {
    & $compiler /nologo /codepage:65001 /target:winexe /optimize+ ("/out:" + (Join-Path $outDir 'Probe.exe')) $references $reader (Join-Path $PSScriptRoot 'src\Probe.cs')
} else {
    $sources = @('App.cs','Engine.cs','Overlay.cs','PlaybackWatch.cs','DesktopControl.cs') | ForEach-Object { Join-Path $PSScriptRoot ('src\' + $_) }
    & $compiler /nologo /codepage:65001 /target:winexe /optimize+ ("/win32manifest:" + (Join-Path $PSScriptRoot 'src\app.manifest')) ("/out:" + (Join-Path $outDir 'NetflixSubtitles.exe')) $references $reader $sources
}
if ($LASTEXITCODE -ne 0) { throw "Compilation failed ($LASTEXITCODE)." }
