namespace Ngan.Ime.Android;

using Ngan.Ime.Core.Infra.Log;
using Avalonia.Android;
using Microsoft.Extensions.Logging;

public class LoggingAvaloniaView : AvaloniaView
{
	public LoggingAvaloniaView(global::Android.Content.Context context)
		: base(context)
	{
		LogLifecycle("ctor");
	}

	protected override void OnAttachedToWindow()
	{
		base.OnAttachedToWindow();
		LogLifecycle("OnAttachedToWindow");
	}

	protected override void OnDetachedFromWindow()
	{
		LogLifecycle("OnDetachedFromWindow");
		base.OnDetachedFromWindow();
	}

	protected override void OnWindowVisibilityChanged(global::Android.Views.ViewStates visibility)
	{
		base.OnWindowVisibilityChanged(visibility);
		LogLifecycle($"OnWindowVisibilityChanged visibility={visibility}");
	}

	protected override void OnVisibilityChanged(global::Android.Views.View changedView, global::Android.Views.ViewStates visibility)
	{
		base.OnVisibilityChanged(changedView, visibility);
		LogLifecycle($"OnVisibilityChanged changed={changedView.GetType().Name} visibility={visibility}");
	}

	protected override void OnSizeChanged(int w, int h, int oldw, int oldh)
	{
		base.OnSizeChanged(w, h, oldw, oldh);
		LogLifecycle($"OnSizeChanged {oldw}x{oldh} -> {w}x{h}");
	}

	public override void RequestLayout()
	{
		base.RequestLayout();
		LogLifecycle("RequestLayout");
	}

	protected override void OnLayout(bool changed, int left, int top, int right, int bottom)
	{
		base.OnLayout(changed, left, top, right, bottom);
		LogLifecycle($"OnLayout changed={changed} bounds=[{left},{top},{right},{bottom}]");
	}

	void LogLifecycle(string message)
	{
		var tag = Tag?.ToString() ?? "untagged";
		AppLog.Inst.Log(
			LogLevel.Information,
			new EventId(0, "ImeView"),
			$"[ImeView:{tag}] {message}; attached={IsAttachedToWindow}; shown={IsShown}; visibility={Visibility}; alpha={Alpha}; size={Width}x{Height}",
			null,
			static (state, _) => state
		);
	}
}
