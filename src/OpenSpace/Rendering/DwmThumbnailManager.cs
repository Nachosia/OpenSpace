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
        {
            App.LogException(new Exception($"[DwmThumbnailManager] Failed to register thumbnail for HWND={window.Hwnd}, Title={window.Title}, HR=0x{hr:X8}"));
            return;
        }

        App.LogException(new Exception($"[DwmThumbnailManager] Registered thumbnail for HWND={window.Hwnd}, Title={window.Title}, Handle={thumb}"));
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

        // Maintain aspect ratio using the source window size.
        var aspectRect = MaintainAspectRatio(window, destRect);

        var props = new DWM_THUMBNAIL_PROPERTIES
        {
            dwFlags = DwmApi.DWM_TNP_RECTDESTINATION | DwmApi.DWM_TNP_VISIBLE | DwmApi.DWM_TNP_OPACITY | DwmApi.DWM_TNP_SOURCECLIENTAREAONLY,
            rcDestination = aspectRect,
            opacity = 255,
            fVisible = true,
            fSourceClientAreaOnly = true
        };

        DwmApi.DwmUpdateThumbnailProperties(window.ThumbnailHandle, ref props);
    }

    private RECT MaintainAspectRatio(SpatialWindow window, RECT targetRect)
    {
        int sourceWidth = window.ScreenBounds.Width;
        int sourceHeight = window.ScreenBounds.Height;

        if (sourceWidth <= 0 || sourceHeight <= 0)
            return targetRect;

        float sourceAspect = (float)sourceWidth / sourceHeight;
        int targetWidth = targetRect.Width;
        int targetHeight = targetRect.Height;

        if (targetHeight == 0)
            return targetRect;

        float targetAspect = (float)targetWidth / targetHeight;

        int newWidth, newHeight;
        if (targetAspect > sourceAspect)
        {
            newHeight = targetHeight;
            newWidth = (int)(targetHeight * sourceAspect);
        }
        else
        {
            newWidth = targetWidth;
            newHeight = (int)(targetWidth / sourceAspect);
        }

        int x = targetRect.Left + (targetWidth - newWidth) / 2;
        int y = targetRect.Top + (targetHeight - newHeight) / 2;

        return new RECT(x, y, x + newWidth, y + newHeight);
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
    }
}
