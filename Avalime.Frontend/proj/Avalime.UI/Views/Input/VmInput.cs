using Avalime.Core.Infra;
using Avalime.Core.Infra.Log;
using Avalime.Core.Keys;
using Avalime.ViewModels;
using Avalonia.Threading;

namespace Avalime.UI.Views.Input;
using Ctx = VmInput;

public class VmInput : ViewModelBase
	, IMk<VmInput>
	, IDisposable
{
	public str Text{
		get => field;
		set => SetProperty(ref field, value);
	} = "";

	public ISvcIme ImeState{get;}

	readonly EventHandler<IEnumerable<IKeyEvent>> _afterInputHandler;

	public VmInput(ISvcIme ImeState){
		this.ImeState = ImeState;
		AppLog.Info($"[Life] VmInput ctor vm#{GetHashCode()}");
		_afterInputHandler = (sender, args)=>{
			var sw = System.Diagnostics.Stopwatch.StartNew();
			AppLog.Debug($"[Perf] VmInput.AfterInput start: {sw.ElapsedMilliseconds}ms");
			str preedit = ImeState.Preedit;
			Dispatcher.UIThread.Post(() => Text = preedit);
			AppLog.Debug($"[Perf] VmInput.AfterInput done: {sw.ElapsedMilliseconds}ms, preedit: {preedit}");
		};
		ImeState.AfterInput += _afterInputHandler;
	}

	/// <summary>
	/// 輸入區 VM 的兜底創建路徑。
	/// </summary>
	public static Ctx Mk()
	{
		return new Ctx(Di.GetRSvc<ISvcIme>());
	}

	public void Dispose()
	{
		AppLog.Info($"[Life] VmInput dispose vm#{GetHashCode()}");
		ImeState.AfterInput -= _afterInputHandler;
	}
}
