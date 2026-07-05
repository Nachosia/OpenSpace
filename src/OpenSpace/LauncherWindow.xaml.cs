using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Forms;
using System.Windows.Input;
using OpenSpace.Core;
using OpenSpace.Input;
using OpenSpace.Win32;
using MessageBox = System.Windows.MessageBox;

namespace OpenSpace;

public partial class LauncherWindow : Window
{
    private readonly AppConfig _config;
    private readonly MainWindow _overlay;
    private readonly HotKeyHelper _toggleHotKey;
    private readonly NotifyIcon _trayIcon;

    private readonly List<HotkeyRow> _hotkeyRows = new();

    public LauncherWindow()
    {
        InitializeComponent();

        _config = ConfigService.Load();
        _overlay = new MainWindow();

        var helper = new WindowInteropHelper(this);
        helper.EnsureHandle();
        _toggleHotKey = new HotKeyHelper(this, 1);

        BuildHotkeyRows();
        ApplyConfigToUi();

        _trayIcon = new NotifyIcon
        {
            Icon = SystemIcons.Application,
            Text = "OpenSpace",
            Visible = true
        };
        _trayIcon.DoubleClick += TrayIcon_DoubleClick;
        _trayIcon.ContextMenuStrip = new ContextMenuStrip();
        _trayIcon.ContextMenuStrip.Items.Add("Открыть", null, (_, _) => Show());
        _trayIcon.ContextMenuStrip.Items.Add("Запустить OpenSpace", null, (_, _) => LaunchOverlay());
        _trayIcon.ContextMenuStrip.Items.Add(new ToolStripSeparator());
        _trayIcon.ContextMenuStrip.Items.Add("Выход", null, (_, _) => ShutdownApp());

        RegisterToggleHotkey();

        if (_config.StartMinimizedToTray)
        {
            Hide();
        }
    }

    private void BuildHotkeyRows()
    {
        AddHotkeyRow("Показать/скрыть оверлей", _config.ToggleOverlay);
        AddHotkeyRow("Следующее окно", _config.NextWindow);
        AddHotkeyRow("Предыдущее окно", _config.PreviousWindow);
        AddHotkeyRow("Активировать окно", _config.ActivateWindow);
        AddHotkeyRow("Закрыть оверлей", _config.CloseOverlay);
        AddHotkeyRow("Панорама влево", _config.PanLeft);
        AddHotkeyRow("Панорама вправо", _config.PanRight);
        AddHotkeyRow("Панорама вверх", _config.PanUp);
        AddHotkeyRow("Панорама вниз", _config.PanDown);
    }

    private void AddHotkeyRow(string label, HotkeySetting setting)
    {
        var row = new HotkeyRow(label, setting);
        _hotkeyRows.Add(row);
        HotkeysPanel.Children.Add(row.Panel);
    }

    private void ApplyConfigToUi()
    {
        StartMinimizedCheckBox.IsChecked = _config.StartMinimizedToTray;
        CloseToTrayCheckBox.IsChecked = _config.CloseToTray;
    }

    private void SaveConfigFromUi()
    {
        foreach (var row in _hotkeyRows)
            row.Sync();

        _config.ToggleOverlay = _hotkeyRows[0].Setting.Clone();
        _config.NextWindow = _hotkeyRows[1].Setting.Clone();
        _config.PreviousWindow = _hotkeyRows[2].Setting.Clone();
        _config.ActivateWindow = _hotkeyRows[3].Setting.Clone();
        _config.CloseOverlay = _hotkeyRows[4].Setting.Clone();
        _config.PanLeft = _hotkeyRows[5].Setting.Clone();
        _config.PanRight = _hotkeyRows[6].Setting.Clone();
        _config.PanUp = _hotkeyRows[7].Setting.Clone();
        _config.PanDown = _hotkeyRows[8].Setting.Clone();

        _config.StartMinimizedToTray = StartMinimizedCheckBox.IsChecked == true;
        _config.CloseToTray = CloseToTrayCheckBox.IsChecked == true;

        ConfigService.Save(_config);
    }

    private void RegisterToggleHotkey()
    {
        _toggleHotKey.Unregister();

        var vk = KeyInterop.VirtualKeyFromKey(_config.ToggleOverlay.Key);
        uint modifiers = 0;
        if ((_config.ToggleOverlay.Modifiers & ModifierKeys.Alt) == ModifierKeys.Alt) modifiers |= NativeMethods.MOD_ALT;
        if ((_config.ToggleOverlay.Modifiers & ModifierKeys.Control) == ModifierKeys.Control) modifiers |= NativeMethods.MOD_CONTROL;
        if ((_config.ToggleOverlay.Modifiers & ModifierKeys.Shift) == ModifierKeys.Shift) modifiers |= NativeMethods.MOD_SHIFT;
        if ((_config.ToggleOverlay.Modifiers & ModifierKeys.Windows) == ModifierKeys.Windows) modifiers |= NativeMethods.MOD_WIN;

        if (!_toggleHotKey.Register(modifiers | NativeMethods.MOD_NOREPEAT, (uint)vk))
        {
            MessageBox.Show("Не удалось зарегистрировать горячую клавишу для показа оверлея. Попробуйте другую комбинацию.",
                "OpenSpace", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
        else
        {
            _toggleHotKey.HotKeyPressed -= OnToggleHotKeyPressed;
            _toggleHotKey.HotKeyPressed += OnToggleHotKeyPressed;
        }
    }

    private void OnToggleHotKeyPressed()
    {
        Dispatcher.Invoke(() =>
        {
            if (_overlay.IsVisible)
                _overlay.HideOverlay();
            else
                LaunchOverlay();
        });
    }

    private void LaunchOverlay()
    {
        _overlay.ApplyConfig(_config);
        _overlay.ShowOverlay();
    }

    private void RefreshButton_Click(object sender, RoutedEventArgs e)
    {
        SaveConfigFromUi();
        RegisterToggleHotkey();
        CheckConflicts();
        _overlay.RefreshWindows();
        MessageBox.Show("Окна обновлены и настройки сохранены.", "OpenSpace", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private void LaunchButton_Click(object sender, RoutedEventArgs e)
    {
        SaveConfigFromUi();
        RegisterToggleHotkey();
        CheckConflicts();
        LaunchOverlay();
    }

    private void ExitButton_Click(object sender, RoutedEventArgs e)
    {
        ShutdownApp();
    }

    private void TrayIcon_DoubleClick(object? sender, EventArgs e)
    {
        Show();
        WindowState = WindowState.Normal;
        Activate();
    }

    private void Window_Closing(object sender, CancelEventArgs e)
    {
        if (_config.CloseToTray && !_isShuttingDown)
        {
            e.Cancel = true;
            Hide();
        }
    }

    private bool _isShuttingDown;

    private void ShutdownApp()
    {
        _isShuttingDown = true;
        _toggleHotKey.Dispose();
        _trayIcon.Visible = false;
        _trayIcon.Dispose();
        _overlay.Close();
        Application.Current.Shutdown();
    }

    private void CheckConflicts()
    {
        var conflicts = new List<string>();

        foreach (var row in _hotkeyRows)
        {
            var conflict = GetKnownConflict(row.Setting);
            if (conflict != null)
            {
                conflicts.Add($"{row.Label}: {conflict}");
            }
        }

        if (conflicts.Count > 0)
        {
            ConflictText.Text = "Возможные конфликты:\n" + string.Join("\n", conflicts);
            ConflictText.Visibility = Visibility.Visible;
        }
        else
        {
            ConflictText.Visibility = Visibility.Collapsed;
        }
    }

    private static string? GetKnownConflict(HotkeySetting setting)
    {
        if (setting.Modifiers == ModifierKeys.Windows && setting.Key == Key.Tab)
            return "Win+Tab используется Windows для просмотра задач и виртуальных рабочих столов";

        if (setting.Modifiers == ModifierKeys.Windows && (setting.Key == Key.Left || setting.Key == Key.Right || setting.Key == Key.Up || setting.Key == Key.Down))
            return "Win+стрелки используются Windows для привязки и перемещения окон";

        if ((setting.Modifiers & ModifierKeys.Windows) == ModifierKeys.Windows &&
            (setting.Modifiers & ModifierKeys.Control) == ModifierKeys.Control &&
            (setting.Key == Key.Left || setting.Key == Key.Right))
            return "Win+Ctrl+стрелки используются Windows для переключения виртуальных рабочих столов";

        if (setting.Modifiers == ModifierKeys.Windows && setting.Key == Key.D)
            return "Win+D сворачивает/восстанавливает все окна";

        if (setting.Modifiers == ModifierKeys.Windows && setting.Key == Key.O)
            return "Win+O открывает панель быстрых настроек (Windows 11)";

        return null;
    }
}

internal sealed class HotkeyRow
{
    public string Label { get; }
    public HotkeySetting Setting { get; }
    public StackPanel Panel { get; }

    private readonly ComboBox _modifiersBox;
    private readonly ComboBox _keyBox;

    public HotkeyRow(string label, HotkeySetting setting)
    {
        Label = label;
        Setting = setting;

        Panel = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 4, 0, 4) };
        Panel.Children.Add(new TextBlock { Text = label, Width = 180, VerticalAlignment = VerticalAlignment.Center });

        _modifiersBox = new ComboBox { Width = 140, Margin = new Thickness(0, 0, 10, 0) };
        _modifiersBox.ItemsSource = new[]
        {
            new { Text = "Нет", Value = ModifierKeys.None },
            new { Text = "Alt", Value = ModifierKeys.Alt },
            new { Text = "Ctrl", Value = ModifierKeys.Control },
            new { Text = "Shift", Value = ModifierKeys.Shift },
            new { Text = "Win", Value = ModifierKeys.Windows },
            new { Text = "Ctrl+Alt", Value = ModifierKeys.Control | ModifierKeys.Alt },
            new { Text = "Win+Alt", Value = ModifierKeys.Windows | ModifierKeys.Alt },
            new { Text = "Win+Ctrl", Value = ModifierKeys.Windows | ModifierKeys.Control },
            new { Text = "Win+Shift", Value = ModifierKeys.Windows | ModifierKeys.Shift },
        };
        _modifiersBox.DisplayMemberPath = "Text";
        _modifiersBox.SelectedValuePath = "Value";
        _modifiersBox.SelectedValue = setting.Modifiers;
        Panel.Children.Add(_modifiersBox);

        _keyBox = new ComboBox { Width = 120 };
        _keyBox.ItemsSource = Enum.GetValues<Key>().Where(k => k != Key.None).ToList();
        _keyBox.SelectedItem = setting.Key;
        Panel.Children.Add(_keyBox);
    }

    public void Sync()
    {
        Setting.Modifiers = (ModifierKeys)_modifiersBox.SelectedValue;
        Setting.Key = (Key)_keyBox.SelectedItem;
    }
}

internal static class HotkeyExtensions
{
    public static HotkeySetting Clone(this HotkeySetting setting)
    {
        return new HotkeySetting(setting.Modifiers, setting.Key);
    }
}
