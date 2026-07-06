using System.Numerics;

namespace OpenSpace.Core;

public sealed class DesktopIcon
{
    public string Path { get; }
    public string Name { get; }
    public Imaging.BitmapSource? IconSource { get; }
    public Vector2 Position { get; set; }
    public Vector2 Size { get; set; }

    public DesktopIcon(string path, string name, Imaging.BitmapSource? iconSource)
    {
        Path = path;
        Name = name;
        IconSource = iconSource;
        Position = Vector2.Zero;
        Size = Vector2.Zero;
    }
}

public sealed class DesktopIconArea
{
    private readonly List<DesktopIcon> _icons = new();

    public Vector2 Position { get; private set; }
    public Vector2 Size { get; private set; }
    public IReadOnlyList<DesktopIcon> Icons => _icons;
    public Rect2 Bounds => new Rect2(Position, Size);

    public static DesktopIconArea Load(string folderPath, float cellWidth = 110f, float cellHeight = 130f, float padding = 60f)
    {
        var area = new DesktopIconArea();

        try
        {
            if (!Directory.Exists(folderPath))
                return area;

            var entries = Directory
                .EnumerateFileSystemEntries(folderPath)
                .Where(p => !IsHidden(p) &&
                           !string.Equals(Path.GetFileName(p), "desktop.ini", StringComparison.OrdinalIgnoreCase))
                .OrderBy(Path.GetFileName)
                .Take(200);

            foreach (var entry in entries)
            {
                var name = Path.GetFileNameWithoutExtension(entry) ?? entry;
                var icon = Win32.ShellIconLoader.LoadIcon(entry, small: true);
                area._icons.Add(new DesktopIcon(entry, name, icon));
            }
        }
        catch (Exception ex)
        {
            App.LogException(ex);
        }

        area.Arrange(cellWidth, cellHeight, padding);
        return area;
    }

    private static bool IsHidden(string path)
    {
        try
        {
            var attributes = File.GetAttributes(path);
            return (attributes & FileAttributes.Hidden) == FileAttributes.Hidden;
        }
        catch
        {
            return false;
        }
    }

    private void Arrange(float cellWidth, float cellHeight, float padding)
    {
        int count = _icons.Count;
        if (count == 0)
        {
            Position = Vector2.Zero;
            Size = Vector2.Zero;
            return;
        }

        int columns = Math.Max(1, (int)Math.Ceiling(Math.Sqrt(count)));
        int rows = (int)Math.Ceiling(count / (double)columns);

        float width = columns * cellWidth + padding * 2;
        float height = rows * cellHeight + padding * 2;

        Position = new Vector2(-width / 2, -height / 2);
        Size = new Vector2(width, height);

        for (int i = 0; i < count; i++)
        {
            int row = i / columns;
            int col = i % columns;
            _icons[i].Position = new Vector2(
                Position.X + padding + col * cellWidth,
                Position.Y + padding + row * cellHeight);
            _icons[i].Size = new Vector2(cellWidth, cellHeight);
        }
    }
}
