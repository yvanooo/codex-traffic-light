using CodexTrafficLight.Core.Models;
using CodexTrafficLight.Core.Services;

namespace CodexTrafficLight.Tests;

public sealed class AppSettingsStoreTests
{
    [Fact]
    public void LoadReturnsDefaultsWhenMissing()
    {
        var store = new AppSettingsStore(new CodexPaths(CreateTempRoot()));

        var settings = store.Load();

        Assert.Equal("dark", settings.Theme);
        Assert.Equal("triple", settings.Style);
        Assert.False(settings.Muted);
        Assert.True(settings.AutoOpenDrawerOnYellow);
        Assert.False(settings.ShowEndedSessions);
        Assert.True(settings.Topmost);
        Assert.Equal("breath", settings.LampEffect);
        Assert.Equal("standard", settings.LampSpeed);
        Assert.Null(settings.WindowLeft);
        Assert.Null(settings.WindowTop);
    }

    [Fact]
    public void SaveAndLoadRoundTrips()
    {
        var store = new AppSettingsStore(new CodexPaths(CreateTempRoot()));
        var expected = new AppSettings
        {
            WindowLeft = 100,
            WindowTop = 80,
            Theme = "light",
            Style = "single",
            Muted = true,
            AutoOpenDrawerOnYellow = false,
            ShowEndedSessions = true,
            Topmost = false,
            LampEffect = "steady",
            LampSpeed = "slow"
        };

        store.Save(expected);
        var actual = store.Load();

        Assert.Equal(expected.WindowLeft, actual.WindowLeft);
        Assert.Equal(expected.WindowTop, actual.WindowTop);
        Assert.Equal("light", actual.Theme);
        Assert.Equal("single", actual.Style);
        Assert.True(actual.Muted);
        Assert.False(actual.AutoOpenDrawerOnYellow);
        Assert.True(actual.ShowEndedSessions);
        Assert.False(actual.Topmost);
        Assert.Equal("steady", actual.LampEffect);
        Assert.Equal("slow", actual.LampSpeed);
    }

    private static string CreateTempRoot()
    {
        var path = Path.Combine(Path.GetTempPath(), "CodexTrafficLightTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }
}
