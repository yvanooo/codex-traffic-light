using System.Text.Json;
using System.Text.Json.Nodes;

namespace CodexTrafficLight.Core.Services;

public sealed class CodexHookInstaller
{
    private const string Marker = "codex_traffic_light_write_status.ps1";
    private readonly CodexPaths _paths;

    public CodexHookInstaller(CodexPaths paths)
    {
        _paths = paths;
    }

    public string InstallOrUpdate()
    {
        _paths.EnsureCodexDirectory();
        EnsureHookScript();

        var root = LoadRoot();
        var hooks = root["hooks"] as JsonObject ?? new JsonObject();
        root["hooks"] = hooks;

        AddOwnedEvent(hooks, "UserPromptSubmit", "red");
        AddOwnedEvent(hooks, "PermissionRequest", "yellow");
        AddOwnedEvent(hooks, "Stop", "green");
        AddOwnedEvent(hooks, "SessionStart", "green");

        var json = root.ToJsonString(new JsonSerializerOptions { WriteIndented = true });
        if (!File.Exists(_paths.HooksPath) || File.ReadAllText(_paths.HooksPath) != json)
        {
            File.WriteAllText(_paths.HooksPath, json);
        }

        return _paths.HooksPath;
    }

    private JsonObject LoadRoot()
    {
        if (!File.Exists(_paths.HooksPath))
        {
            return new JsonObject();
        }

        try
        {
            return JsonNode.Parse(File.ReadAllText(_paths.HooksPath)) as JsonObject ?? new JsonObject();
        }
        catch
        {
            var backupPath = _paths.HooksPath + ".invalid-" + DateTime.Now.ToString("yyyyMMddHHmmss") + ".bak";
            File.Copy(_paths.HooksPath, backupPath, overwrite: false);
            return new JsonObject();
        }
    }

    private void AddOwnedEvent(JsonObject hooks, string eventName, string state)
    {
        var existing = hooks[eventName] as JsonArray ?? new JsonArray();
        var cleaned = new JsonArray();

        foreach (var item in existing)
        {
            if (item is not JsonObject obj || !ContainsOwnedCommand(obj))
            {
                cleaned.Add(item?.DeepClone());
            }
        }

        cleaned.Add(CreateHookEntry(eventName, state));
        hooks[eventName] = cleaned;
    }

    private static bool ContainsOwnedCommand(JsonObject entry)
    {
        var handlers = entry["hooks"] as JsonArray;
        if (handlers is null)
        {
            return false;
        }

        return handlers.Any(handler =>
            handler is JsonObject obj &&
            obj["command"]?.GetValue<string>().Contains(Marker, StringComparison.OrdinalIgnoreCase) == true);
    }

    private JsonObject CreateHookEntry(string eventName, string state)
    {
        return new JsonObject
        {
            ["hooks"] = new JsonArray
            {
                new JsonObject
                {
                    ["type"] = "command",
                    ["command"] = BuildPowerShellCommand(state, eventName),
                    ["statusMessage"] = $"Codex 红绿灯：{state}"
                }
            }
        };
    }

    private string BuildPowerShellCommand(string state, string eventName)
    {
        return $"powershell -NoProfile -ExecutionPolicy Bypass -File {QuoteArg(_paths.HookScriptPath)} -State {QuoteArg(state)} -EventName {QuoteArg(eventName)}";
    }

    private static string QuoteArg(string value)
    {
        return "\"" + value.Replace("\"", "\\\"") + "\"";
    }

    private void EnsureHookScript()
    {
        Directory.CreateDirectory(_paths.HookScriptDirectory);

        const string script = """
param(
    [Parameter(Mandatory=$true)]
    [ValidateSet('red','yellow','green','unknown')]
    [string]$State,

    [Parameter(Mandatory=$false)]
    [string]$EventName = 'unknown'
)

$codexDir = if ($env:CODEX_HOME) { $env:CODEX_HOME } else { Join-Path $env:USERPROFILE '.codex' }
if (-not (Test-Path -LiteralPath $codexDir)) {
    New-Item -ItemType Directory -Path $codexDir -Force | Out-Null
}

$toolDir = Join-Path $codexDir 'codex-traffic-light'
$sessionsDir = Join-Path $toolDir 'sessions'
$diagnosticsDir = Join-Path $toolDir 'diagnostics'
New-Item -ItemType Directory -Path $sessionsDir -Force | Out-Null
New-Item -ItemType Directory -Path $diagnosticsDir -Force | Out-Null

try {
    [Console]::InputEncoding = [Text.Encoding]::UTF8
    [Console]::OutputEncoding = [Text.Encoding]::UTF8
} catch {
}

$rawHookInput = ''
try {
    $rawHookInput = [Console]::In.ReadToEnd()
    $rawHookInput = $rawHookInput.TrimStart([char]0xFEFF)
} catch {
    $rawHookInput = ''
}

$hookInput = $null
if (-not [string]::IsNullOrWhiteSpace($rawHookInput)) {
    try {
        $hookInput = $rawHookInput | ConvertFrom-Json -ErrorAction Stop
    } catch {
        $hookInput = $null
    }
}

$currentProcess = Get-CimInstance Win32_Process -Filter "ProcessId = $PID"
$parentProcess = $null
if ($currentProcess.ParentProcessId) {
    $parentProcess = Get-CimInstance Win32_Process -Filter "ProcessId = $($currentProcess.ParentProcessId)" -ErrorAction SilentlyContinue
}

$codexProcess = $parentProcess
$cursor = $parentProcess
for ($i = 0; $i -lt 6 -and $cursor; $i++) {
    if ($cursor.Name -match 'codex') {
        $codexProcess = $cursor
        break
    }

    if (-not $cursor.ParentProcessId) {
        break
    }

    $cursor = Get-CimInstance Win32_Process -Filter "ProcessId = $($cursor.ParentProcessId)" -ErrorAction SilentlyContinue
}

$processId = if ($codexProcess) { [int]$codexProcess.ProcessId } else { [int]$PID }
$processStart = $null
try {
    if ($codexProcess -and $codexProcess.CreationDate) {
        if ($codexProcess.CreationDate -is [datetime]) {
            $processStart = $codexProcess.CreationDate.ToString('o')
        } else {
            $processStart = [Management.ManagementDateTimeConverter]::ToDateTime($codexProcess.CreationDate).ToString('o')
        }
    }
} catch {
    $processStart = $null
}

$inputSessionId = $null
if ($hookInput -and $hookInput.session_id) {
    $inputSessionId = [string]$hookInput.session_id
} elseif ($hookInput -and $hookInput.conversation_id) {
    $inputSessionId = [string]$hookInput.conversation_id
}

$inputPrompt = $null
if ($hookInput -and $hookInput.prompt) {
    $inputPrompt = ([string]$hookInput.prompt) -replace '\s+', ' '
    $inputPrompt = $inputPrompt.Trim()
    if ($inputPrompt.Length -gt 36) {
        $inputPrompt = $inputPrompt.Substring(0, 36)
    }
}

$sessionId = $env:CODEX_TRAFFIC_LIGHT_SESSION_ID
if ([string]::IsNullOrWhiteSpace($sessionId)) {
    if (-not [string]::IsNullOrWhiteSpace($inputSessionId)) {
        $sessionId = "codex-$inputSessionId"
    } else {
        $startToken = if ($processStart) { $processStart } else { 'unknown-start' }
        $sessionId = "pid-$processId-" + ($startToken -replace '[^0-9A-Za-z]', '')
    }
}
$safeSessionId = $sessionId -replace '[^0-9A-Za-z_-]', '_'

$source = if ($codexProcess -and $codexProcess.CommandLine -match 'app-server') { 'vscode-plugin' } else { 'cli' }

$workingDirectory = if ($env:CODEX_TRAFFIC_LIGHT_WORKING_DIRECTORY) {
    $env:CODEX_TRAFFIC_LIGHT_WORKING_DIRECTORY
} elseif ($hookInput -and $hookInput.cwd) {
    [string]$hookInput.cwd
} else {
    (Get-Location).Path
}

$sessionPath = Join-Path $sessionsDir "$safeSessionId.json"
$sessionTempPath = Join-Path $sessionsDir "$safeSessionId.json.tmp"
$previousDisplayName = $null
if (Test-Path -LiteralPath $sessionPath) {
    try {
        $previousSession = Get-Content -Raw -LiteralPath $sessionPath -Encoding UTF8 | ConvertFrom-Json -ErrorAction Stop
        if ($previousSession.displayName) {
            $previousDisplayName = [string]$previousSession.displayName
        }
    } catch {
        $previousDisplayName = $null
    }
}

$displayName = if ($env:CODEX_TRAFFIC_LIGHT_TASK_NAME) {
    $env:CODEX_TRAFFIC_LIGHT_TASK_NAME
} elseif ($env:CODEX_TRAFFIC_LIGHT_SESSION_NAME) {
    $env:CODEX_TRAFFIC_LIGHT_SESSION_NAME
} elseif (-not [string]::IsNullOrWhiteSpace($inputPrompt)) {
    $inputPrompt
} elseif (-not [string]::IsNullOrWhiteSpace($previousDisplayName)) {
    $previousDisplayName
} else {
    Split-Path -Leaf $workingDirectory
}
if ([string]::IsNullOrWhiteSpace($displayName)) {
    $displayName = 'Codex'
}

$statusPath = Join-Path $codexDir 'codex_traffic_light_state.json'
$payload = [ordered]@{
    state = $State
    event = $EventName
    updatedAt = (Get-Date).ToString('o')
} | ConvertTo-Json -Compress

Set-Content -LiteralPath $statusPath -Value $payload -Encoding UTF8

$sessionPayload = [ordered]@{
    sessionId = $safeSessionId
    displayName = $displayName
    workingDirectory = $workingDirectory
    source = $source
    state = $State
    event = $EventName
    processId = $processId
    processStartTime = $processStart
    updatedAt = (Get-Date).ToString('o')
} | ConvertTo-Json -Compress

Set-Content -LiteralPath $sessionTempPath -Value $sessionPayload -Encoding UTF8
Move-Item -LiteralPath $sessionTempPath -Destination $sessionPath -Force

$diagnosticPath = Join-Path $diagnosticsDir 'latest-hook-context.json'
$diagnosticPayload = [ordered]@{
    event = $EventName
    state = $State
    hookProcessId = [int]$PID
    parentProcessId = if ($parentProcess) { [int]$parentProcess.ProcessId } else { $null }
    parentCommandLine = if ($parentProcess) { $parentProcess.CommandLine } else { $null }
    codexProcessId = $processId
    codexCommandLine = if ($codexProcess) { $codexProcess.CommandLine } else { $null }
    workingDirectory = $workingDirectory
    source = $source
    environmentSessionId = $env:CODEX_TRAFFIC_LIGHT_SESSION_ID
    inputSessionId = $inputSessionId
    promptPreview = $inputPrompt
    rawHookInput = if ($rawHookInput.Length -gt 2000) { $rawHookInput.Substring(0, 2000) } else { $rawHookInput }
    updatedAt = (Get-Date).ToString('o')
} | ConvertTo-Json -Compress -Depth 4

Set-Content -LiteralPath $diagnosticPath -Value $diagnosticPayload -Encoding UTF8
""";

        if (!File.Exists(_paths.HookScriptPath) || File.ReadAllText(_paths.HookScriptPath) != script)
        {
            File.WriteAllText(_paths.HookScriptPath, script);
        }
    }
}
