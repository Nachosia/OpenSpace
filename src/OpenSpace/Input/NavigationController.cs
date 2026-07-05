using OpenSpace.Core;
using System.Numerics;
using System.Windows.Input;

namespace OpenSpace.Input;

internal sealed class NavigationController
{
    private readonly SpatialCanvas _canvas;
    private readonly Camera _camera;
    private AppConfig _config;

    public event Action? WindowActivated;
    public event Action? ExitRequested;
    public event Action? MaximizeSelected;

    public NavigationController(SpatialCanvas canvas, Camera camera, AppConfig config)
    {
        _canvas = canvas;
        _camera = camera;
        _config = config;
    }

    public void UpdateConfig(AppConfig config)
    {
        _config = config;
    }

    public void HandleKey(KeyEventArgs e)
    {
        var key = e.Key;
        var modifiers = Keyboard.Modifiers;
        bool isRightCtrl = e.Key == Key.RightCtrl;
        bool isRightCtrlHeld = Keyboard.IsKeyDown(Key.RightCtrl);

        if (_config.CloseOverlay.Matches(key, modifiers))
        {
            ExitRequested?.Invoke();
            return;
        }

        if (_config.ActivateWindow.Matches(key, modifiers))
        {
            WindowActivated?.Invoke();
            return;
        }

        if (_config.NextWindow.Matches(key, modifiers))
        {
            _canvas.SelectNext();
            FocusSelected();
            return;
        }

        if (_config.PreviousWindow.Matches(key, modifiers))
        {
            _canvas.SelectPrevious();
            FocusSelected();
            return;
        }

        if (_config.PanLeft.Matches(key, modifiers))
        {
            _camera.PanBy(new Vector2(-100, 0));
            return;
        }

        if (_config.PanRight.Matches(key, modifiers))
        {
            _camera.PanBy(new Vector2(100, 0));
            return;
        }

        if (_config.PanUp.Matches(key, modifiers))
        {
            _camera.PanBy(new Vector2(0, -100));
            return;
        }

        if (_config.PanDown.Matches(key, modifiers))
        {
            _camera.PanBy(new Vector2(0, 100));
            return;
        }

        // Zoom with Right Ctrl + Up/Down
        if (isRightCtrlHeld)
        {
            if (key == Key.Up)
            {
                _camera.ZoomBy(1.1f);
                return;
            }
            if (key == Key.Down)
            {
                _camera.ZoomBy(0.9f);
                return;
            }
        }

        // Maximize selected window to viewport with Ctrl+Shift+A
        if (key == Key.A && (modifiers & ModifierKeys.Control) == ModifierKeys.Control && (modifiers & ModifierKeys.Shift) == ModifierKeys.Shift)
        {
            MaximizeSelected?.Invoke();
            return;
        }
    }

    public void FocusSelected()
    {
        if (_canvas.SelectedWindow != null)
        {
            _camera.PanTo(_canvas.SelectedWindow.Center);
        }
    }

    public void FocusWindow(SpatialWindow window)
    {
        _canvas.Select(window);
        _camera.PanTo(window.Center);
    }
}
