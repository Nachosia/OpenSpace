using System.Numerics;
using OpenSpace.Win32;

namespace OpenSpace.Core;

public sealed class Camera
{
    private Vector2 _targetPosition;
    private float _targetZoom = 1.0f;
    private double _elapsed;
    private double _duration;
    private Vector2 _startPosition;
    private float _startZoom = 1.0f;
    private bool _isAnimating;

    public Vector2 Position { get; private set; }
    public float Zoom { get; private set; } = 1.0f;

    public float PanSpeed { get; set; } = 0.15f;
    public float ZoomSpeed { get; set; } = 0.1f;

    public void SetPosition(Vector2 position)
    {
        Position = position;
        _targetPosition = position;
        _isAnimating = false;
    }

    public void SetZoom(float zoom)
    {
        Zoom = zoom;
        _targetZoom = zoom;
        _isAnimating = false;
    }

    public void PanTo(Vector2 target, float durationMs = 350f)
    {
        PanTo(target, Zoom, durationMs);
    }

    public void PanTo(Vector2 target, float targetZoom, float durationMs = 350f)
    {
        _startPosition = Position;
        _targetPosition = target;
        _startZoom = Zoom;
        _targetZoom = targetZoom;
        _elapsed = 0;
        _duration = durationMs;
        _isAnimating = true;
    }

    public void PanBy(Vector2 delta)
    {
        _targetPosition += delta;
        Position += delta;
        _isAnimating = false;
    }

    public void FitToBounds(Rect2 bounds, int viewportWidth, int viewportHeight, float padding = 0.1f)
    {
        if (bounds.Width <= 0 || bounds.Height <= 0 || viewportWidth <= 0 || viewportHeight <= 0)
            return;

        var center = bounds.Center;
        float zoomX = viewportWidth / (bounds.Width * (1 + padding));
        float zoomY = viewportHeight / (bounds.Height * (1 + padding));
        float targetZoom = Math.Min(zoomX, zoomY);

        SetPosition(center);
        SetZoom(Math.Clamp(targetZoom, 0.1f, 3.0f));
    }

    public void ZoomBy(float factor)
    {
        _targetZoom = Math.Clamp(_targetZoom * factor, 0.1f, 3.0f);
        Zoom = _targetZoom;
        _isAnimating = false;
    }

    public void Update(double deltaTimeMs)
    {
        if (!_isAnimating)
        {
            // Smooth follow when not animating
            Position = Vector2.Lerp(Position, _targetPosition, PanSpeed);
            Zoom = MathHelper.Lerp(Zoom, _targetZoom, ZoomSpeed);
            return;
        }

        _elapsed += deltaTimeMs;
        double t = Math.Clamp(_elapsed / _duration, 0.0, 1.0);
        float eased = Easing.EaseOutCubic((float)t);

        Position = Vector2.Lerp(_startPosition, _targetPosition, eased);
        Zoom = MathHelper.Lerp(_startZoom, _targetZoom, eased);

        if (t >= 1.0)
            _isAnimating = false;
    }

    public RECT WorldToScreen(Vector2 worldPosition, Vector2 worldSize, int viewportWidth, int viewportHeight)
    {
        float halfW = viewportWidth / 2f;
        float halfH = viewportHeight / 2f;

        float x = (worldPosition.X - Position.X) * Zoom + halfW;
        float y = (worldPosition.Y - Position.Y) * Zoom + halfH;
        float w = worldSize.X * Zoom;
        float h = worldSize.Y * Zoom;

        return new RECT((int)x, (int)y, (int)(x + w), (int)(y + h));
    }

    public Vector2 ScreenToWorld(System.Windows.Point screenPoint, int viewportWidth, int viewportHeight)
    {
        float halfW = viewportWidth / 2f;
        float halfH = viewportHeight / 2f;

        float x = (float)((screenPoint.X - halfW) / Zoom + Position.X);
        float y = (float)((screenPoint.Y - halfH) / Zoom + Position.Y);

        return new Vector2(x, y);
    }
}

internal static class MathHelper
{
    public static float Lerp(float a, float b, float t)
    {
        return a + (b - a) * Math.Clamp(t, 0f, 1f);
    }
}

internal static class Easing
{
    public static float EaseOutCubic(float t)
    {
        float f = 1 - t;
        return 1 - f * f * f;
    }
}
