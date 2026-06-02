using System.Windows;
using System.Windows.Controls;
using CodexTrafficLight.Core.Models;
using WpfComboBox = System.Windows.Controls.ComboBox;

namespace CodexTrafficLight.App;

public partial class SettingsWindow : Window
{
    public SettingsWindow(AppSettings settings)
    {
        InitializeComponent();
        Settings = settings;

        SelectByTag(StyleComboBox, settings.Style);
        SelectByTag(ThemeComboBox, settings.Theme);
        SelectByTag(LampEffectComboBox, settings.LampEffect);
        SelectByTag(LampSpeedComboBox, settings.LampSpeed);
        TopmostCheckBox.IsChecked = settings.Topmost;
        AutoOpenDrawerCheckBox.IsChecked = settings.AutoOpenDrawerOnYellow;
        ShowEndedSessionsCheckBox.IsChecked = settings.ShowEndedSessions;
        MutedCheckBox.IsChecked = settings.Muted;
    }

    public AppSettings Settings { get; private set; }

    private void SaveButton_Click(object sender, RoutedEventArgs e)
    {
        Settings = Settings with
        {
            Style = GetSelectedTag(StyleComboBox, Settings.Style),
            Theme = GetSelectedTag(ThemeComboBox, Settings.Theme),
            LampEffect = GetSelectedTag(LampEffectComboBox, Settings.LampEffect),
            LampSpeed = GetSelectedTag(LampSpeedComboBox, Settings.LampSpeed),
            Topmost = TopmostCheckBox.IsChecked == true,
            AutoOpenDrawerOnYellow = AutoOpenDrawerCheckBox.IsChecked == true,
            ShowEndedSessions = ShowEndedSessionsCheckBox.IsChecked == true,
            Muted = MutedCheckBox.IsChecked == true
        };

        DialogResult = true;
    }

    private static void SelectByTag(WpfComboBox comboBox, string value)
    {
        foreach (var item in comboBox.Items.OfType<ComboBoxItem>())
        {
            if (string.Equals(item.Tag?.ToString(), value, StringComparison.OrdinalIgnoreCase))
            {
                comboBox.SelectedItem = item;
                return;
            }
        }

        comboBox.SelectedIndex = 0;
    }

    private static string GetSelectedTag(WpfComboBox comboBox, string fallback)
    {
        return (comboBox.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? fallback;
    }
}
