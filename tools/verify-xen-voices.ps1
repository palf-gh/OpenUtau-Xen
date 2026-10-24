param(
    [Parameter(Mandatory=$true)][string]$VoiceRoot,
    [Parameter(Mandatory=$true)][string]$UtauEngine,
    [string]$OutputDirectory = 'work/voice-smoke'
)
$ErrorActionPreference = 'Stop'
$repoRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
Push-Location -LiteralPath $repoRoot
try {
    $env:OPENUTAU_XEN_VOICE_ROOT = [IO.Path]::GetFullPath($VoiceRoot)
    $env:OPENUTAU_XEN_UTAU_ENGINE = [IO.Path]::GetFullPath($UtauEngine)
    $env:OPENUTAU_XEN_SMOKE_OUTPUT = [IO.Path]::GetFullPath($OutputDirectory)
    $env:OPENUTAU_XEN_SMOKE_ISOLATED = '1'
    dotnet test OpenUtau.Test -o work/voice-test-host --filter FullyQualifiedName~XenVoiceSmokeTest -v minimal -p:WarningLevel=0 -clp:ErrorsOnly
    $testExit = $LASTEXITCODE
} finally {
    Pop-Location
}
exit $testExit
