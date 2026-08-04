using Ngan.Ime.Core.Keys;

namespace Ngan.Ime.Windows;


public class WindowsKeyProcessor
	:IOsKeyProcessor
	,IImeKeyProcessor//TODO test
{

	protected static WindowsKeyProcessor? _inst = null;
	public static WindowsKeyProcessor inst => _inst??= new WindowsKeyProcessor();

	public WindowsKeyProcessor() {
	}


	public event ErrHandler? OnErr;

	public Task<IRespOnKeyEvent> OnKeyEvents(IEnumerable<IKeyEvent> keyEvents, CT Ct) {
		foreach (var keyEvent in keyEvents) {
			var ans = KeyEventConverter.inst.ConvertKeyEvent(keyEvent);
			KeySender.keybd_event(
				ans.Item1,
				ans.Item2,
				ans.Item3,
				ans.Item4
			);
		}
		return Task.FromResult<IRespOnKeyEvent>(new RespOnKeyEvent());
	}
}
