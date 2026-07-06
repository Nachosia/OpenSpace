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
    private System.Windows.Threading.DispatcherTimer? _hoverTimer;
    private SpatialWindow? _hoverWindow;
    private bool _isDragging;
    private bool _isResizing;
    private Point _lastMousePosition;
    private bool _allowClose;
    private SpatialWindow? _resizeWindow;
    private ResizeHandle _resizeHandle;
    private Vector2 _resizeStartPosition;
    private Vector2 _resizeStartSize;
    private readonly Dictionary<DesktopIcon, FrameworkElement> _iconControls = new();

    private const double ResizeHandleSize = 12;

    public MainWindow()
    {
        InitializeComponent();
        _navigation = new NavigationController(_canvas, _camera, _config);
        _navigation.WindowActivated += OnWindowActivated;
        _navigation.ExitRequested += HideOverlay;
        _navigation.MaximizeSelected += OnMaximizeSelected;
        ApplyConfig(_config);
    }

    public void ApplyConfig(AppConfig config)
    {
        _config = config;
        _navigation?.UpdateConfig(config);
    }

    public void TrackWindow(IntPtr hwnd)
    {
        _enumerator.TrackOwnWindow(hwnd);
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

        _hoverTimer = new System.Windows.Threading.DispatcherTimer(System.Windows.Threading.DispatcherPriority.Background)
        {
            Interval = TimeSpan.FromMilliseconds(_config.HoverFocusDelayMs)
        };
        _hoverTimer.Tick += OnHoverTimerTick;

        Hide();
    }

    public void ShowOverlay()
    {
        try
        {
            _renderTimer?.Start();
            RefreshWindows();

            Show();
            Activate();
            UpdateLayout();

            int width = (int)ActualWidth;
            int height = (int)ActualHeight;
            if (_canvas.Windows.Count > 0 && width > 0 && height > 0)
            {
                _camera.FitToBounds(_canvas.GetBounds(), width, height);
            }

            UpdateThumbnails();
            BuildIconControls();
        }
        catch (Exception ex)
        {
            App.LogException(ex);
            throw;
        }
    }

    public void HideOverlay()
    {
        _renderTimer?.Stop();
        foreach (var window in _canvas.Windows)
        {
            _thumbnailManager.UnregisterThumbnail(window);
        }
        _iconControls.Clear();
        IconCanvas.Children.Clear();
        Hide();
    }

    public void RefreshWindows()
    {
        try
        {
            foreach (var window in _canvas.Windows)
            {
                _thumbnailManager.UnregisterThumbnail(window);
            }

            DesktopIconArea? iconArea = null;
            if (_config.ShowDesktopIcons)
            {
                string folder = string.IsNullOrWhiteSpace(_config.DesktopIconsFolder)
                    ? Environment.GetFolderPath(Environment.SpecialFolder.Desktop)
                    : _config.DesktopIconsFolder;
                iconArea = DesktopIconArea.Load(folder);
            }

            _canvas.LayoutMode = _config.WindowLayoutMode;
            _canvas.LoadWindows(_enumerator.Enumerate(), iconArea);

            foreach (var window in _canvas.Windows)
            {
                _thumbnailManager.RegisterThumbnail(window);
            }
        }
        catch (Exception ex)
        {
            App.LogException(ex);
        }
    }

    private void OnRenderTick(object? sender, EventArgs e)
    {
        _camera.Update(_renderTimer!.Interval.TotalMilliseconds);
        UpdateThumbnails();
        UpdateIconTransforms();
    }

    private void UpdateThumbnails()
    {
        int width = (int)ActualWidth;
        int height = (int)ActualHeight;
        if (width <= 0 || height <= 0)
            return;

        foreach (var window in _canvas.Windows)
        {
            _thumbnailManager.UpdateThumbnail(window, _camera, width, height, _config.MaintainAspectRatio);
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

    private void BuildIconControls()
    {
        IconCanvas.Children.Clear();
        _iconControls.Clear();

        var area = _canvas.IconArea;
        if (area == null)
            return;

        foreach (var icon in area.Icons)
        {
            var button = new System.Windows.Controls.Button
            {
                Background = Brushes.Transparent,
                BorderThickness = new Thickness(0),
                Padding = new Thickness(4),
                Cursor = Cursors.Hand,
                RenderTransformOrigin = new System.Windows.Point(0, 0)
            };

            var panel = new System.Windows.Controls.StackPanel
            {
                Orientation = Orientation.Vertical,
                HorizontalAlignment = System.Windows.HorizontalAlignment.Center
            };

            if (icon.IconSource != null)
            {
                panel.Children.Add(new System.Windows.Controls.Image
                {
                    Source = icon.IconSource,
                    Width = 48,
                    Height = 48,
                    Stretch = System.Windows.Media.Stretch.Uniform
                });
            }

            panel.Children.Add(new System.Windows.Controls.TextBlock
            {
                Text = icon.Name,
                Foreground = Brushes.White,
                TextTrimming = System.Windows.TextTrimming.CharacterEllipsis,
                TextAlignment = System.Windows.TextAlignment.Center,
                MaxWidth = 90,
                FontSize = 12
            });

            button.Content = panel;
            button.Click += (_, _) => OnIconClicked(icon);
            IconCanvas.Children.Add(button);
            _iconControls[icon] = button;
        }

        UpdateIconTransforms();
    }

    private void UpdateIconTransforms()
    {
        int width = (int)ActualWidth;
        int height = (int)ActualHeight;
        if (width <= 0 || height <= 0)
            return;

        float halfW = width / 2f;
        float halfH = height / 2f;
        float zoom = _camera.Zoom;

        foreach (var pair in _iconControls)
        {
            var icon = pair.Key;
            var control = pair.Value;

            float x = (icon.Position.X - _camera.Position.X) * zoom + halfW;
            float y = (icon.Position.Y - _camera.Position.Y) * zoom + halfH;

            Canvas.SetLeft(control, x);
            Canvas.SetTop(control, y);
            control.RenderTransform = new ScaleTransform(zoom, zoom);

            bool visible = x + icon.Size.X * zoom > 0 &&
                           y + icon.Size.Y * zoom > 0 &&
                           x < width &&
                           y < height;
            control.Visibility = visible ? Visibility.Visible : Visibility.Collapsed;
        }
    }

    private void OnIconClicked(DesktopIcon icon)
    {
        try
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(icon.Path)
            {
                UseShellExecute = true
            });
            HideOverlay();
        }
        catch (Exception ex)
        {
            App.LogException(ex);
        }
    }

    private void OnWindowActivated()
    {
        var selected = _canvas.SelectedWindow;
        if (selected == null)
            return;

        HideOverlay();
        WindowActivator.Activate(selected.Hwnd);
    }

    private void OnMaximizeSelected()
    {
        var selected = _canvas.SelectedWindow;
        if (selected == null)
            return;

        int width = (int)ActualWidth;
        int height = (int)ActualHeight;
        if (width <= 0 || height <= 0)
            return;

        // Center on camera, make window fill the viewport while maintaining aspect ratio.
        var worldCenter = _camera.ScreenToWorld(new Point(width / 2.0, height / 2.0), width, height);

        float sourceAspect = selected.PlaneSize.X > 0 && selected.PlaneSize.Y > 0
            ? selected.PlaneSize.X / selected.PlaneSize.Y
            : 1.0f;

        float viewportWidth = width / _camera.Zoom;
        float viewportHeight = height / _camera.Zoom;

        Vector2 newSize;
        float viewportAspect = viewportWidth / viewportHeight;
        if (sourceAspect > viewportAspect)
        {
            newSize = new Vector2(viewportWidth, viewportWidth / sourceAspect);
        }
        else
        {
            newSize = new Vector2(viewportHeight * sourceAspect, viewportHeight);
        }

        selected.PlaneSize = newSize;
        selected.PlanePosition = worldCenter - newSize / 2;
        _camera.PanTo(worldCenter, durationMs: 200);
    }

    private void Window_KeyDown(object sender, KeyEventArgs e)
    {
        _navigation.HandleKey(e);
        e.Handled = true;
    }

    private void Window_MouseWheel(object sender, MouseWheelEventArgs e)
    {
        // Mouse wheel is now free: vertical pan.
        _camera.PanBy(new Vector2(0, -e.Delta));
    }

    private void Window_MouseMove(object sender, MouseEventArgs e)
    {
        var pos = e.GetPosition(this);

        if (_isResizing && _resizeWindow != null)
        {
            ResizeWindow(pos);
            return;
        }

        if (_isDragging)
        {
            var delta = pos - _lastMousePosition;
            _camera.PanBy(new Vector2((float)-delta.X, (float)-delta.Y));
            _lastMousePosition = pos;
            return;
        }

        UpdateCursor(pos);
        UpdateHover(pos);
    }

    private void UpdateHover(Point screenPos)
    {
        if (_config.HoverFocusDelayMs <= 0)
            return;

        var worldPos = _camera.ScreenToWorld(screenPos, (int)ActualWidth, (int)ActualHeight);
        var window = _canvas.FindWindowAt(worldPos);

        if (window == null || window == _hoverWindow || window == _canvas.SelectedWindow)
        {
            _hoverTimer?.Stop();
            _hoverWindow = null;
            return;
        }

        if (_config.HoverFocusRequiresCtrl && !Keyboard.IsKeyDown(Key.LeftCtrl) && !Keyboard.IsKeyDown(Key.RightCtrl))
        {
            _hoverTimer?.Stop();
            _hoverWindow = null;
            return;
        }

        _hoverWindow = window;
        _hoverTimer?.Stop();
        _hoverTimer?.Start();
    }

    private void OnHoverTimerTick(object? sender, EventArgs e)
    {
        _hoverTimer?.Stop();
        if (_hoverWindow != null)
        {
            _navigation.FocusWindow(_hoverWindow);
        }
    }

    private void Window_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        _hoverTimer?.Stop();
        _hoverWindow = null;

        var pos = e.GetPosition(this);
        var worldPos = _camera.ScreenToWorld(pos, (int)ActualWidth, (int)ActualHeight);
        var window = _canvas.FindWindowAt(worldPos);

        if (window != null)
        {
            var handle = GetResizeHandle(window, worldPos);
            if (handle != ResizeHandle.None)
            {
                _isResizing = true;
                _resizeWindow = window;
                _resizeHandle = handle;
                _resizeStartPosition = window.PlanePosition;
                _resizeStartSize = window.PlaneSize;
                _lastMousePosition = pos;
                CaptureMouse();
                return;
            }

            _navigation.FocusWindow(window);
        }

        _isDragging = true;
        _lastMousePosition = pos;
        CaptureMouse();
    }

    private void Window_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        _isDragging = false;
        _isResizing = false;
        _resizeWindow = null;
        ReleaseMouseCapture();
    }

    private void ResizeWindow(Point screenPos)
    {
        if (_resizeWindow == null)
            return;

        var delta = screenPos - _lastMousePosition;
        var worldDelta = new Vector2((float)(delta.X / _camera.Zoom), (float)(delta.Y / _camera.Zoom));

        var newPos = _resizeStartPosition;
        var newSize = _resizeStartSize;

        if ((_resizeHandle & ResizeHandle.Left) != 0)
        {
            newPos.X += worldDelta.X;
            newSize.X -= worldDelta.X;
        }
        if ((_resizeHandle & ResizeHandle.Right) != 0)
        {
            newSize.X += worldDelta.X;
        }
        if ((_resizeHandle & ResizeHandle.Top) != 0)
        {
            newPos.Y += worldDelta.Y;
            newSize.Y -= worldDelta.Y;
        }
        if ((_resizeHandle & ResizeHandle.Bottom) != 0)
        {
            newSize.Y += worldDelta.Y;
        }

        const float minSize = 50;
        if (newSize.X < minSize)
        {
            if ((_resizeHandle & ResizeHandle.Left) != 0)
                newPos.X = _resizeStartPosition.X + _resizeStartSize.X - minSize;
            newSize.X = minSize;
        }
        if (newSize.Y < minSize)
        {
            if ((_resizeHandle & ResizeHandle.Top) != 0)
                newPos.Y = _resizeStartPosition.Y + _resizeStartSize.Y - minSize;
            newSize.Y = minSize;
        }

        _resizeWindow.PlanePosition = newPos;
        _resizeWindow.PlaneSize = newSize;
    }

    private void UpdateCursor(Point screenPos)
    {
        var worldPos = _camera.ScreenToWorld(screenPos, (int)ActualWidth, (int)ActualHeight);
        var window = _canvas.FindWindowAt(worldPos);
        if (window == null)
        {
            Cursor = Cursors.Arrow;
            return;
        }

        var handle = GetResizeHandle(window, worldPos);
        Cursor = handle switch
        {
            ResizeHandle.TopLeft or ResizeHandle.BottomRight => Cursors.SizeNWSE,
            ResizeHandle.TopRight or ResizeHandle.BottomLeft => Cursors.SizeNESW,
            ResizeHandle.Left or ResizeHandle.Right => Cursors.SizeWE,
            ResizeHandle.Top or ResizeHandle.Bottom => Cursors.SizeNS,
            _ => Cursors.SizeAll
        };
    }

    private ResizeHandle GetResizeHandle(SpatialWindow window, Vector2 worldPos)
    {
        double handleSizeWorld = ResizeHandleSize / _camera.Zoom;
        bool left = Math.Abs(worldPos.X - window.PlanePosition.X) < handleSizeWorld;
        bool right = Math.Abs(worldPos.X - (window.PlanePosition.X + window.PlaneSize.X)) < handleSizeWorld;
        bool top = Math.Abs(worldPos.Y - window.PlanePosition.Y) < handleSizeWorld;
        bool bottom = Math.Abs(worldPos.Y - (window.PlanePosition.Y + window.PlaneSize.Y)) < handleSizeWorld;

        if (top && left) return ResizeHandle.TopLeft;
        if (top && right) return ResizeHandle.TopRight;
        if (bottom && left) return ResizeHandle.BottomLeft;
        if (bottom && right) return ResizeHandle.BottomRight;
        if (left) return ResizeHandle.Left;
        if (right) return ResizeHandle.Right;
        if (top) return ResizeHandle.Top;
        if (bottom) return ResizeHandle.Bottom;

        return ResizeHandle.None;
    }

    protected override void OnClosing(CancelEventArgs e)
    {
        if (!_allowClose)
        {
            e.Cancel = true;
            HideOverlay();
        }
        base.OnClosing(e);
    }

    public void ForceClose()
    {
        _allowClose = true;
        Close();
    }

    protected override void OnClosed(EventArgs e)
    {
        _renderTimer?.Stop();
        _hoverTimer?.Stop();

        foreach (var window in _canvas.Windows)
        {
            _thumbnailManager.UnregisterThumbnail(window);
        }

        _thumbnailManager.Dispose();
        _enumerator.Dispose();
        base.OnClosed(e);
    }
}

[Flags]
internal enum ResizeHandle
{
    None = 0,
    Left = 1,
    Right = 2,
    Top = 4,
    Bottom = 8,
    TopLeft = Top | Left,
    TopRight = Top | Right,
    BottomLeft = Bottom | Left,
    BottomRight = Bottom | Right
}
