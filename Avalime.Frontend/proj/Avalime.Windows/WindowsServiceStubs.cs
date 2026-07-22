namespace Avalime.Windows;

using Avalime.Core.Keys;
using Avalime.UI;

class StubImeKeyProcessor : IImeKeyProcessor {
	public event ErrHandler? OnErr;
	public Task<IRespOnKeyEvent> OnKeyEvents(IEnumerable<IKeyEvent> keyEvents, CT Ct)
		=> Task.FromResult<IRespOnKeyEvent>(new RespOnKeyEvent());
}

class StubKeyboardHost : IKeyboardHost {
	public bool CanCommitText => false;
	public void HideKeyboard() { }
	public void CommitText(str text) { }
}

class StubClipboardService : IClipboardService {
	public Task<IReadOnlyList<str>> GetItemsAsy(CT ct = default)
		=> Task.FromResult<IReadOnlyList<str>>([]);
}
