using System.Numerics;
using OpenSpace.Win32;

namespace OpenSpace.Core;

public sealed class SpatialWindow
{
    public IntPtr Hwnd { get; }
    public string Title { get; }
    public string ClassName { get; }
    public RECT ScreenBounds { get; set; }
    public Vector2 PlanePosition { get; set; }
    public Vector2 PlaneSize { get; set; }
    public IntPtr ThumbnailHandle { get; set; }
    public bool IsSelected { get; set; }

    public SpatialWindow(IntPtr hwnd, string title, string className, RECT screenBounds)
    {
        Hwnd = hwnd;
        Title = title;
        ClassName = className;
        ScreenBounds = screenBounds;
        PlanePosition = Vector2.Zero;
        PlaneSize = new Vector2(screenBounds.Width, screenBounds.Height);
    }

    public Vector2 Center => PlanePosition + PlaneSize / 2;
}
