param(
    # Folder of a UTAU voicebank (containing character.txt) with a sustained Japanese /a/.
    [Parameter(Mandatory=$true)][string]$ClassicBank,
    # DiffSinger configuration folder (containing character.txt and dsconfig.yaml).
    [Parameter(Mandatory=$true)][string]$DiffSingerBank,
    # DiffSinger vocoder model (.onnx) matching the voicebank.
    [Parameter(Mandatory=$true)][string]$DiffSingerVocoder,
    # Folder containing resampler.exe and resampler.dll.
    [Parameter(Mandatory=$true)][string]$UtauEngine,
    [string]$OutputDirectory = 'work/voice-smoke'
)
$ErrorActionPreference = 'Stop'
$repoRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
Push-Location -LiteralPath $repoRoot
try {
    $env:OPENUTAU_XEN_CLASSIC_BANK = [IO.Path]::GetFullPath($ClassicBank)
    $env:OPENUTAU_XEN_DIFFSINGER_BANK = [IO.Path]::GetFullPath($DiffSingerBank)
    $env:OPENUTAU_XEN_DIFFSINGER_VOCODER = [IO.Path]::GetFullPath($DiffSingerVocoder)
    $env:OPENUTAU_XEN_UTAU_ENGINE = [IO.Path]::GetFullPath($UtauEngine)
    $env:OPENUTAU_XEN_SMOKE_OUTPUT = [IO.Path]::GetFullPath($OutputDirectory)
    $env:OPENUTAU_XEN_SMOKE_ISOLATED = '1'
    dotnet test OpenUtau.Test -o work/voice-test-host --filter FullyQualifiedName~XenVoiceSmokeTest -v minimal -p:WarningLevel=0 -clp:ErrorsOnly
    $testExit = $LASTEXITCODE
} finally {
    Pop-Location
}
exit $testExit
