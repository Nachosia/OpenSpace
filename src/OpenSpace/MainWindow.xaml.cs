using System.ComponentModel;
using System.Numerics;
using System.Windows;
using System.Windows.Input;
using OpenSpace.Core;
using OpenSpace.Input;
using OpenSpace.Rendering;
using OpenSpace.Win32;

namespace OpenSpace;

public partial class MainWindow : Window
{
    private readonly SpatialCanvas _canvas = new();
    private readonly Camera _camera = new();
    private readonly WindowEnumerator _enumerator = new();
    private readonly DwmThumbnailManager _thumbnailManager = new();
    private NavigationController _navigation = null!;
    private AppConfig _config = new();

    private System.Windows.Threading.DispatcherTimer? _renderTimer;
    private bool _isDragging;
    private Point _lastMousePosition;

    public MainWindow()
    {
        InitializeComponent();
        _navigation = new NavigationController(_canvas, _camera, _config);
        _navigation.WindowActivated += OnWindowActivated;
        _navigation.ExitRequested += HideOverlay;
        ApplyConfig(_config);
    }

    public void ApplyConfig(AppConfig config)
    {
        _config = config;
        _navigation?.UpdateConfig(config);
    }

    private void Window_Loaded(object sender, RoutedEventArgs e)
    {
        var helper = new WindowInteropHelper(this);
        IntPtr hwnd = helper.Handle;

        _enumerator.TrackOwnWindow(hwnd);
        _thumbnailManager.SetDestinationHwnd(hwnd);

        _renderTimer = new System.Windows.Threading.DispatcherTimer(System.Windows.Threading.DispatcherPriority.Render)
        {
            Interval = TimeSpan.FromMilliseconds(16)
        };
        _renderTimer.Tick += OnRenderTick;
        _renderTimer.Start();

        Hide();
    }

    public void ShowOverlay()
    {
        RefreshWindows();
        _navigation.FocusSelected();

        Show();
        Activate();
        UpdateThumbnails();
    }

    public void HideOverlay()
    {
        foreach (var window in _canvas.Windows)
        {
            _thumbnailManager.UnregisterThumbnail(window);
        }
        Hide();
    }

    public void RefreshWindows()
    {
        foreach (var window in _canvas.Windows)
        {
            _thumbnailManager.UnregisterThumbnail(window);
        }

        _canvas.LoadWindows(_enumerator.Enumerate());

        foreach (var window in _canvas.Windows)
        {
            _thumbnailManager.RegisterThumbnail(window);
        }
    }

    private void OnRenderTick(object? sender, EventArgs e)
    {
        _camera.Update(_renderTimer!.Interval.TotalMilliseconds);
        UpdateThumbnails();
    }

    private void UpdateThumbnails()
    {
        int width = (int)ActualWidth;
        int height = (int)ActualHeight;
        if (width <= 0 || height <= 0)
            return;

        foreach (var window in _canvas.Windows)
        {
            _thumbnailManager.UpdateThumbnail(window, _camera, width, height);
        }

        UpdateSelectionRectangle(width, height);
    }

    private void UpdateSelectionRectangle(int width, int height)
    {
        var selected = _canvas.SelectedWindow;
        if (selected == null)
        {
            SelectionRectangle.Visibility = Visibility.Collapsed;
            return;
        }

        var rect = _camera.WorldToScreen(selected.PlanePosition, selected.PlaneSize, width, height);
        SelectionRectangle.Margin = new Thickness(rect.Left, rect.Top, 0, 0);
        SelectionRectangle.Width = Math.Max(0, rect.Width);
        SelectionRectangle.Height = Math.Max(0, rect.Height);
        SelectionRectangle.Visibility = Visibility.Visible;
    }

    private void OnWindowActivated()
    {
        var selected = _canvas.SelectedWindow;
        if (selected == null)
            return;

        HideOverlay();
        WindowActivator.Activate(selected.Hwnd);
    }

    private void Window_KeyDown(object sender, KeyEventArgs e)
    {
        _navigation.HandleKey(e.Key, Keyboard.Modifiers);
        e.Handled = true;
    }

    private void Window_MouseWheel(object sender, MouseWheelEventArgs e)
    {
        float factor = e.Delta > 0 ? 1.1f : 0.9f;
        _camera.ZoomBy(factor);
    }

    private void Window_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        _isDragging = true;
        _lastMousePosition = e.GetPosition(this);
        CaptureMouse();

        var worldPos = _camera.ScreenToWorld(_lastMousePosition, (int)ActualWidth, (int)ActualHeight);
        var window = _canvas.FindWindowAt(worldPos);
        if (window != null)
        {
            _navigation.FocusWindow(window);
        }
    }

    private void Window_MouseMove(object sender, MouseEventArgs e)
    {
        if (!_isDragging)
            return;

        var pos = e.GetPosition(this);
        var delta = pos - _lastMousePosition;
        _camera.PanBy(new Vector2((float)-delta.X, (float)-delta.Y));
        _lastMousePosition = pos;
    }

    private void Window_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        _isDragging = false;
        ReleaseMouseCapture();
    }

    protected override void OnClosing(CancelEventArgs e)
    {
        e.Cancel = true;
        HideOverlay();
        base.OnClosing(e);
    }

    protected override void OnClosed(EventArgs e)
    {
        _renderTimer?.Stop();

        foreach (var window in _canvas.Windows)
        {
            _thumbnailManager.UnregisterThumbnail(window);
        }

        _thumbnailManager.Dispose();
        _enumerator.Dispose();
        base.OnClosed(e);
    }
}
