namespace CodexTrafficLight.Core.Models;

public sealed record UpdateCheckResult
{
    public bool IsSuccess { get; init; }
    public bool HasUpdate { get; init; }
    public string CurrentVersion { get; init; } = string.Empty;
    public string LatestVersion { get; init; } = string.Empty;
    public string Title { get; init; } = string.Empty;
    public IReadOnlyList<string> Notes { get; init; } = Array.Empty<string>();
    public string DownloadUrl { get; init; } = string.Empty;
    public string Message { get; init; } = string.Empty;
}
