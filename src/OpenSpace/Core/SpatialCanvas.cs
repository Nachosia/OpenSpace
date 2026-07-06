using System.Numerics;
using System.Windows;

namespace OpenSpace.Core;

public sealed class SpatialCanvas
{
    private readonly List<SpatialWindow> _windows = new();

    public IReadOnlyList<SpatialWindow> Windows => _windows;
    public DesktopIconArea? IconArea { get; private set; }
    public LayoutMode LayoutMode { get; set; } = LayoutMode.FreeGrid;

    public SpatialWindow? SelectedWindow { get; private set; }

    public void LoadWindows(IEnumerable<SpatialWindow> windows, DesktopIconArea? iconArea = null)
    {
        _windows.Clear();
        _windows.AddRange(windows);
        IconArea = iconArea;

        if (_windows.Count > 0)
        {
            if (LayoutMode == LayoutMode.FreeGrid)
                ArrangeFreeGrid();
            else
                ArrangeFromScreenCoordinates();
        }

        SelectedWindow = _windows.FirstOrDefault();
        if (SelectedWindow != null)
            SelectedWindow.IsSelected = true;
    }

    private void ArrangeFromScreenCoordinates()
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

    private void ArrangeFreeGrid()
    {
        float maxW = Math.Min(_windows.Max(w => w.ScreenBounds.Width), 1400f);
        float maxH = Math.Min(_windows.Max(w => w.ScreenBounds.Height), 900f);

        const float hGap = 200f;
        const float vGap = 150f;

        float cellW = maxW + hGap;
        float cellH = maxH + vGap;

        int count = _windows.Count;
        double aspect = SystemParameters.PrimaryScreenWidth / SystemParameters.PrimaryScreenHeight;

        int holeCols = 0;
        int holeRows = 0;
        if (IconArea != null && IconArea.Size.X > 0 && IconArea.Size.Y > 0)
        {
            holeCols = Math.Max(1, (int)Math.Ceiling(IconArea.Size.X / cellW));
            holeRows = Math.Max(1, (int)Math.Ceiling(IconArea.Size.Y / cellH));
        }

        int columns = Math.Max(holeCols + 2, (int)Math.Ceiling(Math.Sqrt(count * aspect)));
        int cellsNeeded = count + holeCols * holeRows;
        int rows = (int)Math.Ceiling(cellsNeeded / (double)columns);

        float gridW = columns * cellW;
        float gridH = rows * cellH;
        var origin = new Vector2(-gridW / 2, -gridH / 2);

        int leftHole = (columns - holeCols) / 2;
        int topHole = (rows - holeRows) / 2;

        int index = 0;
        for (int r = 0; r < rows && index < count; r++)
        {
            for (int c = 0; c < columns && index < count; c++)
            {
                if (c >= leftHole && c < leftHole + holeCols &&
                    r >= topHole && r < topHole + holeRows)
                {
                    continue;
                }

                var window = _windows[index++];
                window.PlanePosition = new Vector2(
                    origin.X + c * cellW + hGap / 2,
                    origin.Y + r * cellH + vGap / 2);
                window.PlaneSize = new Vector2(
                    window.ScreenBounds.Width,
                    window.ScreenBounds.Height);
            }
        }
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
        IconArea = null;
    }

    public Rect2 GetBounds()
    {
        float minX = float.MaxValue;
        float minY = float.MaxValue;
        float maxX = float.MinValue;
        float maxY = float.MinValue;
        bool has = false;

        foreach (var window in _windows)
        {
            minX = Math.Min(minX, window.PlanePosition.X);
            minY = Math.Min(minY, window.PlanePosition.Y);
            maxX = Math.Max(maxX, window.PlanePosition.X + window.PlaneSize.X);
            maxY = Math.Max(maxY, window.PlanePosition.Y + window.PlaneSize.Y);
            has = true;
        }

        if (IconArea != null && IconArea.Size.X > 0 && IconArea.Size.Y > 0)
        {
            minX = Math.Min(minX, IconArea.Position.X);
            minY = Math.Min(minY, IconArea.Position.Y);
            maxX = Math.Max(maxX, IconArea.Position.X + IconArea.Size.X);
            maxY = Math.Max(maxY, IconArea.Position.Y + IconArea.Size.Y);
            has = true;
        }

        if (!has)
            return new Rect2(Vector2.Zero, Vector2.Zero);

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
