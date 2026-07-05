using System.Numerics;
using OpenSpace.Core;
using OpenSpace.Win32;

namespace OpenSpace.Rendering;

internal sealed class DwmThumbnailManager : IDisposable
{
    private IntPtr _destinationHwnd;

    public void SetDestinationHwnd(IntPtr hwnd)
    {
        _destinationHwnd = hwnd;
    }

    public void RegisterThumbnail(SpatialWindow window)
    {
        if (_destinationHwnd == IntPtr.Zero)
            throw new InvalidOperationException("Destination HWND not set.");

        if (window.ThumbnailHandle != IntPtr.Zero)
            return;

        int hr = DwmApi.DwmRegisterThumbnail(_destinationHwnd, window.Hwnd, out IntPtr thumb);
        if (hr < 0)
            return;

        window.ThumbnailHandle = thumb;
    }

    public void UpdateThumbnail(SpatialWindow window, Camera camera, int viewportWidth, int viewportHeight)
    {
        if (window.ThumbnailHandle == IntPtr.Zero)
            return;

        var destRect = camera.WorldToScreen(window.PlanePosition, window.PlaneSize, viewportWidth, viewportHeight);

        // Clamp to viewport
        if (destRect.Right <= 0 || destRect.Bottom <= 0 || destRect.Left >= viewportWidth || destRect.Top >= viewportHeight)
        {
            SetVisible(window, false);
            return;
        }

        var props = new DWM_THUMBNAIL_PROPERTIES
        {
            dwFlags = DwmApi.DWM_TNP_RECTDESTINATION | DwmApi.DWM_TNP_VISIBLE | DwmApi.DWM_TNP_OPACITY,
            rcDestination = destRect,
            opacity = 255,
            fVisible = true,
            fSourceClientAreaOnly = false
        };

        DwmApi.DwmUpdateThumbnailProperties(window.ThumbnailHandle, ref props);
    }

    public void UnregisterThumbnail(SpatialWindow window)
    {
        if (window.ThumbnailHandle == IntPtr.Zero)
            return;

        DwmApi.DwmUnregisterThumbnail(window.ThumbnailHandle);
        window.ThumbnailHandle = IntPtr.Zero;
    }

    private void SetVisible(SpatialWindow window, bool visible)
    {
        if (window.ThumbnailHandle == IntPtr.Zero)
            return;

        var props = new DWM_THUMBNAIL_PROPERTIES
        {
            dwFlags = DwmApi.DWM_TNP_VISIBLE,
            fVisible = visible
        };

        DwmApi.DwmUpdateThumbnailProperties(window.ThumbnailHandle, ref props);
    }

    public void Dispose()
    {
        // Thumbnails are automatically unregistered when destination window closes,
        // but explicit cleanup is good practice.
    }
}
