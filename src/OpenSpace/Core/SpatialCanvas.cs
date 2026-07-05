using System.Numerics;

namespace OpenSpace.Core;

public sealed class SpatialCanvas
{
    private readonly List<SpatialWindow> _windows = new();

    public IReadOnlyList<SpatialWindow> Windows => _windows;

    public SpatialWindow? SelectedWindow { get; private set; }

    public void LoadWindows(IEnumerable<SpatialWindow> windows)
    {
        _windows.Clear();
        _windows.AddRange(windows);

        // Normalize positions so the minimum is near (0,0) while preserving relative layout.
        if (_windows.Count > 0)
        {
            float minX = _windows.Min(w => w.ScreenBounds.Left);
            float minY = _windows.Min(w => w.ScreenBounds.Top);

            foreach (var window in _windows)
            {
                window.PlanePosition = new Vector2(
                    window.ScreenBounds.Left - minX,
                    window.ScreenBounds.Top - minY);
                window.PlaneSize = new Vector2(
                    window.ScreenBounds.Width,
                    window.ScreenBounds.Height);
            }
        }

        SelectedWindow = _windows.FirstOrDefault();
        SelectedWindow ??= _windows.FirstOrDefault();
        if (SelectedWindow != null)
            SelectedWindow.IsSelected = true;
    }

    public void SelectNext()
    {
        if (_windows.Count == 0)
            return;

        int index = SelectedWindow != null ? _windows.IndexOf(SelectedWindow) : -1;
        int nextIndex = (index + 1) % _windows.Count;
        SelectAt(nextIndex);
    }

    public void SelectPrevious()
    {
        if (_windows.Count == 0)
            return;

        int index = SelectedWindow != null ? _windows.IndexOf(SelectedWindow) : 0;
        int prevIndex = (index - 1 + _windows.Count) % _windows.Count;
        SelectAt(prevIndex);
    }

    public void SelectAt(int index)
    {
        if (index < 0 || index >= _windows.Count)
            return;

        if (SelectedWindow != null)
            SelectedWindow.IsSelected = false;

        SelectedWindow = _windows[index];
        SelectedWindow.IsSelected = true;
    }

    public void Select(SpatialWindow window)
    {
        if (!_windows.Contains(window))
            return;

        if (SelectedWindow != null)
            SelectedWindow.IsSelected = false;

        SelectedWindow = window;
        SelectedWindow.IsSelected = true;
    }

    public SpatialWindow? FindWindowAt(Vector2 worldPoint)
    {
        // Search in reverse to find top-most (last drawn) first
        for (int i = _windows.Count - 1; i >= 0; i--)
        {
            var w = _windows[i];
            if (worldPoint.X >= w.PlanePosition.X &&
                worldPoint.X <= w.PlanePosition.X + w.PlaneSize.X &&
                worldPoint.Y >= w.PlanePosition.Y &&
                worldPoint.Y <= w.PlanePosition.Y + w.PlaneSize.Y)
            {
                return w;
            }
        }

        return null;
    }

    public void Clear()
    {
        _windows.Clear();
        SelectedWindow = null;
    }

    public Rect2 GetBounds()
    {
        if (_windows.Count == 0)
            return new Rect2(Vector2.Zero, Vector2.Zero);

        float minX = _windows.Min(w => w.PlanePosition.X);
        float minY = _windows.Min(w => w.PlanePosition.Y);
        float maxX = _windows.Max(w => w.PlanePosition.X + w.PlaneSize.X);
        float maxY = _windows.Max(w => w.PlanePosition.Y + w.PlaneSize.Y);

        return new Rect2(new Vector2(minX, minY), new Vector2(maxX - minX, maxY - minY));
    }
}

public readonly struct Rect2
{
    public Vector2 Position { get; }
    public Vector2 Size { get; }
    public float Width => Size.X;
    public float Height => Size.Y;
    public Vector2 Center => Position + Size / 2;

    public Rect2(Vector2 position, Vector2 size)
    {
        Position = position;
        Size = size;
    }
}
