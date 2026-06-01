param(
    [Parameter(Mandatory = $false)]
    [Alias("Name")]
    [string]$TaskName,

    [Parameter(ValueFromRemainingArguments = $true)]
    [string[]]$CodexArgs
)

$env:CODEX_TRAFFIC_LIGHT_SESSION_ID = [guid]::NewGuid().ToString("N")
$env:CODEX_TRAFFIC_LIGHT_SESSION_NAME = if ([string]::IsNullOrWhiteSpace($TaskName)) { Split-Path -Leaf (Get-Location) } else { $TaskName }
$env:CODEX_TRAFFIC_LIGHT_TASK_NAME = $env:CODEX_TRAFFIC_LIGHT_SESSION_NAME
$env:CODEX_TRAFFIC_LIGHT_WORKING_DIRECTORY = (Get-Location).Path

codex @CodexArgs
