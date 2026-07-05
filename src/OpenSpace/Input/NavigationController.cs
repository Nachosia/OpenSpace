using OpenSpace.Core;
using System.Windows.Input;

namespace OpenSpace.Input;

internal sealed class NavigationController
{
    private readonly SpatialCanvas _canvas;
    private readonly Camera _camera;

    public event Action? WindowActivated;
    public event Action? ExitRequested;

    public NavigationController(SpatialCanvas canvas, Camera camera)
    {
        _canvas = canvas;
        _camera = camera;
    }

    public void HandleKey(Key key, ModifierKeys modifiers)
    {
        bool shift = (modifiers & ModifierKeys.Shift) == ModifierKeys.Shift;

        switch (key)
        {
            case Key.Tab:
                if (shift)
                    _canvas.SelectPrevious();
                else
                    _canvas.SelectNext();
                FocusSelected();
                break;

            case Key.Left:
                _camera.PanBy(new System.Numerics.Vector2(-100, 0));
                break;

            case Key.Right:
                _camera.PanBy(new System.Numerics.Vector2(100, 0));
                break;

            case Key.Up:
                _camera.PanBy(new System.Numerics.Vector2(0, -100));
                break;

            case Key.Down:
                _camera.PanBy(new System.Numerics.Vector2(0, 100));
                break;

            case Key.Enter:
                WindowActivated?.Invoke();
                break;

            case Key.Escape:
                ExitRequested?.Invoke();
                break;
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
