#nullable enable

using System;
using System.ComponentModel;
using System.IO;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

using SkiaSharp;

using Windows.ApplicationModel;
using Windows.Foundation;
using Windows.Graphics.Display;
using Windows.UI.Core;
using Windows.UI.ViewManagement;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media.Imaging;

using Window = Microsoft.UI.Xaml.Window;

using Uno.Foundation.Logging;
using Uno.UI.Hosting;
using Uno.UI.Xaml;
using Uno.UI.Xaml.Controls;

namespace Uno.UI.Runtime.Skia.MacOS;

internal class MacOSWindowHost : IXamlRootHost
{
	private readonly MacOSWindowNative _nativeWindow;
	private readonly Window _winUIWindow;
	private readonly DisplayInformation _displayInformation;
	private readonly GRContext? _context;
	private SKBitmap? _bitmap;
	private SKSurface? _surface;
	private int _rowBytes;
	private bool _initializationNotCompleted = true; // FIXME

	internal static XamlRootMap<IXamlRootHost> XamlRootMap { get; } = new();

	public MacOSWindowHost(MacOSWindowNative nativeWindow, Window winUIWindow)
	{
		_nativeWindow = nativeWindow ?? throw new ArgumentNullException(nameof(nativeWindow));
		_winUIWindow = winUIWindow ?? throw new ArgumentNullException(nameof(winUIWindow));

		_displayInformation = DisplayInformation.GetForCurrentView();

		// RegisterForBackgroundColor();

		var host = MacSkiaHost.Current!;
		switch (host.RenderSurfaceType)
		{
			case RenderSurfaceType.Metal:
				var ctx = NativeUno.uno_window_get_metal_context(_nativeWindow.Handle);
				_context = MacOSMetalRenderer.CreateContext(ctx);
				break;
			case RenderSurfaceType.Software:
				break;
		}
	}

	// public MacOSWindowNative NativeWindow => _nativeWindow;
	// public Window Window => _winUIWindow;

	internal event EventHandler<Size>? SizeChanged;

	private void UpdateWindowSize(double nativeWidth, double nativeHeight)
	{
		SizeChanged?.Invoke(this, new Size(nativeWidth, nativeHeight));
	}

	private void Draw(SKSurface surface)
	{
		using var canvas = surface.Canvas;
		using (new SKAutoCanvasRestore(canvas, true))
		{
			canvas.Clear(SKColors.White);

			if (RootElement?.Visual is { } rootVisual)
			{
				RootElement.XamlRoot?.Compositor.RenderRootVisual(surface, rootVisual);
			}
		}

		canvas.Flush();
		surface.Flush();
	}

	private unsafe void MetalDraw(double nativeWidth, double nativeHeight, nint texture)
	{
		if (this.Log().IsEnabled(LogLevel.Trace))
		{
			this.Log().Trace($"Window {_nativeWindow.Handle} drawing {nativeWidth}x{nativeHeight} texture: {texture} FullScreen: {NativeUno.uno_application_is_full_screen()}");
		}

		var scale = (float)_displayInformation.RawPixelsPerViewPixel;

		// FIXME: we get the first (native) updates for window sizes before we have completed the (managed) host initialization
		if (_initializationNotCompleted)
		{
			UpdateWindowSize(nativeWidth / scale, nativeHeight / scale);
			_initializationNotCompleted = SizeChanged is null;
			if (_initializationNotCompleted)
			{
				return; // not yet...
			}
		}

		// we can't cache anything since the texture will be different on next calls
		using var target = MacOSMetalRenderer.CreateTarget(_context!, nativeWidth, nativeHeight, texture);
		using var surface = SKSurface.Create(_context, target, GRSurfaceOrigin.TopLeft, SKColorType.Bgra8888);

		surface.Canvas.Scale(scale, scale);

		Draw(surface);

		_context?.Flush();
	}

	private unsafe void SoftDraw(double nativeWidth, double nativeHeight, nint* data, int* rowBytes, int* size)
	{
		if (this.Log().IsEnabled(LogLevel.Trace))
		{
			this.Log().Trace($"Window {_nativeWindow.Handle} drawing {nativeWidth}x{nativeHeight} FullScreen: {NativeUno.uno_application_is_full_screen()}");
		}

		// FIXME: we get the first (native) updates for window sizes before we have completed the (managed) host initialization
		if (_initializationNotCompleted)
		{
			UpdateWindowSize(nativeWidth, nativeHeight);
			_initializationNotCompleted = SizeChanged is null;
			if (_initializationNotCompleted)
			{
				return; // not yet...
			}
		}

		int width = (int)nativeWidth;
		int height = (int)nativeHeight;
		if (_bitmap == null || width != _bitmap.Width || height != _bitmap.Height)
		{
			_bitmap?.Dispose();
			_surface?.Dispose();

			var info = new SKImageInfo(width, height, SKColorType.Rgba8888, SKAlphaType.Premul);
			_bitmap = new SKBitmap(info);
			_surface = SKSurface.Create(info, _bitmap.GetPixels());
			_rowBytes = info.RowBytes;
		}

		Draw(_surface!);

		*data = (nint)_bitmap.GetPixels(out var bitmapSize);
		*size = (int)bitmapSize;
		*rowBytes = _rowBytes;
	}

	internal static Dictionary<nint, WeakReference<MacOSWindowHost>> windows = new();

	public static unsafe void Register()
	{
		// FIXME: ugly but this loads libSkiaSharp into memory (because it looks for @rpath/libSkiaSharp.dylib)
		NativeSkia.gr_direct_context_make_metal(0, 0);

		NativeUno.uno_set_draw_callback(&MetalDraw);
		NativeUno.uno_set_soft_draw_callback(&SoftDraw);
		NativeUno.uno_set_resize_callback(&Resize);
	}

	public UIElement? RootElement => _winUIWindow.RootElement;

	void IXamlRootHost.InvalidateRender()
	{
		if (this.Log().IsEnabled(LogLevel.Trace))
		{
			this.Log().Trace($"Window {_nativeWindow.Handle} invalidated.");
		}
		_winUIWindow.RootElement?.XamlRoot?.InvalidateOverlays();
		NativeUno.uno_window_invalidate(_nativeWindow.Handle);
	}

	public static void Register(nint handle, MacOSWindowHost host)
	{
		windows.Add(handle, new WeakReference<MacOSWindowHost>(host));
	}

	public static void Unregister(nint handle)
	{
		windows.Remove(handle);
	}

	private static MacOSWindowHost? GetWindowHost(nint handle)
	{
		if (windows.TryGetValue(handle, out var weak))
		{
			weak.TryGetTarget(out var window);
			return window;
		}
		return null;
	}

	[UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]
	private static void MetalDraw(nint handle, double width, double height, nint texture)
	{
		var window = GetWindowHost(handle);
		if (window is not null)
		{
			window.MetalDraw(width, height, texture);
		}
		else if (typeof(MacOSWindowHost).Log().IsEnabled(LogLevel.Warning))
		{
			typeof(MacOSWindowHost).Log().Warn($"MacOSWindowHost.MetalDraw could not map 0x{handle:X} with an NSWindow");
		}
	}

	[UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]
	private static unsafe void SoftDraw(nint handle, double width, double height, nint *data, int *rowBytes, int *size)
	{
		var window = GetWindowHost(handle);
		if (window is not null)
		{
			window.SoftDraw(width, height, data, rowBytes, size);
		}
		else if (typeof(MacOSWindowHost).Log().IsEnabled(LogLevel.Warning))
		{
			// there are some legit times that this can happen, e.g. the NSView.window might not yet be set when the view is created but not yet assigned
			typeof(MacOSWindowHost).Log().Warn($"MacOSWindowHost.SoftDraw could not map 0x{handle:X} with an NSWindow");
		}
	}

	[UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]
	private static void Resize(nint handle, double width, double height)
	{
		var window = GetWindowHost(handle);
		if (window is not null)
		{
			window.UpdateWindowSize(width, height);
		}
		else if (typeof(MacOSWindowHost).Log().IsEnabled(LogLevel.Warning))
		{
			// there are some legit times that this can happen, e.g. the NSView.window might not yet be set when the view is created but not yet assigned
			typeof(MacOSWindowHost).Log().Warn($"MacOSWindowHost.Resize could not map 0x{handle:X} with an NSWindow");
		}
	}
}
