using System.Text.Json;
using CodexTrafficLight.Core.Models;

namespace CodexTrafficLight.Core.Services;

public sealed class UpdateChecker
{
    private static readonly JsonSerializerOptions JsonOptions = JsonOptionsFactory.Create();
    private readonly HttpClient _httpClient;

    public UpdateChecker(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<UpdateCheckResult> CheckAsync(
        string currentVersion,
        string manifestUrl,
        TimeSpan timeout,
        CancellationToken cancellationToken = default)
    {
        if (!TryParseVersion(currentVersion, out var current))
        {
            return Fail("当前版本号无效。", currentVersion);
        }

        if (!Uri.TryCreate(manifestUrl, UriKind.Absolute, out var manifestUri) ||
            manifestUri.Scheme != Uri.UriSchemeHttps)
        {
            return Fail("更新地址未配置或不是 HTTPS。", currentVersion);
        }

        try
        {
            using var timeoutSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeoutSource.CancelAfter(timeout);

            using var response = await _httpClient.GetAsync(manifestUri, timeoutSource.Token);
            if (!response.IsSuccessStatusCode)
            {
                return Fail("暂时无法检查更新。", currentVersion);
            }

            await using var stream = await response.Content.ReadAsStreamAsync(timeoutSource.Token);
            var manifest = await JsonSerializer.DeserializeAsync<UpdateManifest>(stream, JsonOptions, timeoutSource.Token);
            if (manifest is null)
            {
                return Fail("更新信息格式无效。", currentVersion);
            }

            return BuildResult(currentVersion, current, manifest);
        }
        catch
        {
            return Fail("暂时无法检查更新。", currentVersion);
        }
    }

    private static UpdateCheckResult BuildResult(string currentVersion, Version current, UpdateManifest manifest)
    {
        if (!TryParseVersion(manifest.Version, out var latest))
        {
            return Fail("远程版本号无效。", currentVersion);
        }

        if (!Uri.TryCreate(manifest.DownloadUrl, UriKind.Absolute, out var downloadUri) ||
            downloadUri.Scheme != Uri.UriSchemeHttps)
        {
            return Fail("下载地址无效。", currentVersion);
        }

        var hasUpdate = latest > current;
        return new UpdateCheckResult
        {
            IsSuccess = true,
            HasUpdate = hasUpdate,
            CurrentVersion = currentVersion,
            LatestVersion = manifest.Version,
            Title = string.IsNullOrWhiteSpace(manifest.Title)
                ? $"Codex 红绿灯 {manifest.Version}"
                : manifest.Title.Trim(),
            Notes = manifest.Notes
                .Where(note => !string.IsNullOrWhiteSpace(note))
                .Select(note => note.Trim())
                .Take(8)
                .ToArray(),
            DownloadUrl = manifest.DownloadUrl,
            Message = hasUpdate ? "发现新版本。" : "当前已是最新版本。"
        };
    }

    private static bool TryParseVersion(string value, out Version version)
    {
        version = new Version();
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        var parts = value.Trim().Split('.');
        if (parts.Length is < 2 or > 3)
        {
            return false;
        }

        if (parts.Any(part => part.Length == 0 || part.Any(ch => !char.IsDigit(ch))))
        {
            return false;
        }

        return Version.TryParse(string.Join('.', parts), out version!);
    }

    private static UpdateCheckResult Fail(string message, string currentVersion)
    {
        return new UpdateCheckResult
        {
            IsSuccess = false,
            HasUpdate = false,
            CurrentVersion = currentVersion,
            Message = message
        };
    }
}
