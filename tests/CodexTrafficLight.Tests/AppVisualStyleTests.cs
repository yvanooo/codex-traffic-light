using System.Text.RegularExpressions;

namespace CodexTrafficLight.Tests;

public sealed class AppVisualStyleTests
{
    [Fact]
    public void MainWindow_lamps_use_reference_style_well_lens_and_pulse_ring()
    {
        var root = FindRepositoryRoot();
        var xaml = File.ReadAllText(Path.Combine(root, "src", "CodexTrafficLight.App", "MainWindow.xaml"));
        var code = File.ReadAllText(Path.Combine(root, "src", "CodexTrafficLight.App", "MainWindow.xaml.cs"));

        Assert.DoesNotContain("CreateGlowBrush", code);
        Assert.DoesNotContain("lampBrush.Opacity = 0", code);
        Assert.DoesNotContain("wellBrush.Opacity = 0", code);

        Assert.Equal(3, Regex.Matches(xaml, "<Ellipse x:Name=\"(?:Red|Yellow|Green)Ring\"").Count);
        Assert.Equal(3, Regex.Matches(xaml, "<Ellipse x:Name=\"(?:Red|Yellow|Green)Well\"").Count);
        Assert.Equal(3, Regex.Matches(xaml, "<Ellipse x:Name=\"(?:Red|Yellow|Green)Lamp\"").Count);
        Assert.Equal(3, Regex.Matches(xaml, "<ScaleTransform ScaleX=\"1\" ScaleY=\"1\"").Count);

        Assert.Contains("MediaColor.FromRgb(255, 59, 48)", code);
        Assert.Contains("MediaColor.FromRgb(255, 159, 10)", code);
        Assert.Contains("MediaColor.FromRgb(48, 209, 88)", code);
        Assert.Contains("CreateLampBrush", code);
        Assert.Contains("CreateWellBrush", code);
        Assert.Contains("StartBreath", code);
        Assert.Contains("StartReferencePulse", code);
        Assert.Contains("Width=\"100\"", xaml);
        Assert.Contains("Height=\"220\"", xaml);
        Assert.Contains("Width=\"80\"", xaml);
        Assert.Contains("Height=\"200\"", xaml);
        Assert.Contains("SessionCountBadge", xaml);
        Assert.Contains("SessionDrawer", xaml);
        Assert.Contains("SessionDrawerTitle", xaml);
        Assert.Contains("Codex 任务", xaml);
        Assert.DoesNotContain("Codex Sessions", xaml);
        Assert.Contains("SessionDrawerSortLabel", xaml);
        Assert.Contains("SessionDrawerDivider", xaml);
        Assert.Contains("SessionListPanel", xaml);
        Assert.Contains("SessionScrollViewer", xaml);
        Assert.Contains("GetPrimaryTextBrush", code);
        Assert.Contains("GetSecondaryTextBrush", code);
        Assert.Contains("GetDrawerBackgroundBrush", code);
        Assert.Contains("GetSettingsTextBrush", code);
        Assert.Contains("状态说明", code);
        Assert.Contains("版本：", code);
        Assert.Contains("愿每一次红灯，都是通往绿灯的一步。", code);
        Assert.Contains("作者：Gyk", code);
        Assert.Contains("显示已结束会话", code);
        Assert.Contains("隐藏已结束会话", code);
        Assert.Contains("ToggleShowEndedSessions", code);
        Assert.DoesNotContain("sessions.Take(5)", code);
        Assert.Contains("_refreshTimer", File.ReadAllText(Path.Combine(root, "src", "CodexTrafficLight.App", "SessionStatusDirectoryWatcher.cs")));
        Assert.Equal(6, Regex.Matches(xaml, "Width=\"44\" Height=\"44\"").Count);
        Assert.Equal(3, Regex.Matches(xaml, "Width=\"34\" Height=\"34\"").Count);
        Assert.Contains("TimeSpan.FromMilliseconds(275)", code);
        Assert.Contains("TimeSpan.FromSeconds(1)", code);
    }

    [Fact]
    public void AppUsesProjectIconForExeWindowAndTray()
    {
        var root = FindRepositoryRoot();
        var project = File.ReadAllText(Path.Combine(root, "src", "CodexTrafficLight.App", "CodexTrafficLight.App.csproj"));
        var code = File.ReadAllText(Path.Combine(root, "src", "CodexTrafficLight.App", "MainWindow.xaml.cs"));
        var iconPath = Path.Combine(root, "src", "CodexTrafficLight.App", "Assets", "app-icon.ico");

        Assert.True(File.Exists(iconPath), "Expected app-icon.ico to exist.");
        Assert.Contains("<ApplicationIcon>Assets\\app-icon.ico</ApplicationIcon>", project);
        Assert.Contains("LoadApplicationIcon()", code);
        Assert.Contains("ExtractAssociatedIcon", code);
        Assert.DoesNotContain("System.Drawing.SystemIcons.Application", code);
    }

    [Fact]
    public void AppHasVersionAndRemoteUpdateEntryPoint()
    {
        var root = FindRepositoryRoot();
        var project = File.ReadAllText(Path.Combine(root, "src", "CodexTrafficLight.App", "CodexTrafficLight.App.csproj"));
        var code = File.ReadAllText(Path.Combine(root, "src", "CodexTrafficLight.App", "MainWindow.xaml.cs"));

        Assert.Contains("<Version>1.0.0</Version>", project);
        Assert.Contains("<IncludeNativeLibrariesForSelfExtract>true</IncludeNativeLibrariesForSelfExtract>", project);
        Assert.Contains("UpdateManifestUrl", code);
        Assert.Contains("CheckForUpdatesAsync", code);
        Assert.Contains("UpdateChecker", code);
        Assert.DoesNotContain("本地版不使用在线更新检查", code);
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "CodexTrafficLight.sln")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Could not locate CodexTrafficLight.sln.");
    }
}
