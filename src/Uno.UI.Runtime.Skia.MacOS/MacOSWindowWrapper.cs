using System.ComponentModel;

using Microsoft.UI.Windowing;
using Windows.Foundation;
using Windows.UI.Core.Preview;

using Uno.Disposables;
using Uno.UI.Xaml.Controls;

namespace Uno.UI.Runtime.Skia.MacOS;

internal class MacOSWindowWrapper : NativeWindowWrapperBase
{
	private readonly MacOSWindowNative _window;

	public MacOSWindowWrapper(MacOSWindowNative window)
	{
		_window = window;

		window.Host.SizeChanged += OnHostSizeChanged;
		window.Host.Closing += OnWindowClosing;
		window.Host.Closed += OnWindowClosed;
	}

	public override object NativeWindow => _window;

	public override string Title
	{
		get => NativeUno.uno_window_get_title(_window.Handle);
		set => NativeUno.uno_window_set_title(_window.Handle, value);
	}

	private void OnHostSizeChanged(object? sender, Size size)
	{
		Bounds = new Rect(default, size);
		VisibleBounds = Bounds;
	}

	private void OnWindowClosing(object? sender, CancelEventArgs e)
	{
		var closingArgs = RaiseClosing();
		if (closingArgs.Cancel)
		{
			e.Cancel = true;
		}

		var manager = SystemNavigationManagerPreview.GetForCurrentView();
		if (!manager.HasConfirmedClose)
		{
			if (!manager.RequestAppClose())
			{
				e.Cancel = true;
				return;
			}
		}

		// All prerequisites passed, can safely close.
		e.Cancel = false;
	}

	private void OnWindowClosed(object? sender, EventArgs e) => RaiseClosed();

	protected override IDisposable ApplyFullScreenPresenter()
	{
		NativeUno.uno_window_enter_full_screen(_window.Handle);
		return Disposable.Create(() => NativeUno.uno_window_exit_full_screen(_window.Handle));
	}

	protected override IDisposable ApplyOverlappedPresenter(OverlappedPresenter presenter)
	{
		presenter.SetNative(new MacOSNativeOverlappedPresenter(_window));
		return Disposable.Create(() => presenter.SetNative(null));
	}
}
