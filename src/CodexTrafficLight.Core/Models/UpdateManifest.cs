namespace CodexTrafficLight.Core.Models;

public sealed record UpdateManifest
{
    public string Version { get; init; } = string.Empty;
    public string Title { get; init; } = string.Empty;
    public IReadOnlyList<string> Notes { get; init; } = Array.Empty<string>();
    public string DownloadUrl { get; init; } = string.Empty;
}
