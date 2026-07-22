using Android.OS;

namespace Avalime.Android;

public class AndroidKeyboardHost : Avalime.UI.IKeyboardHost
{
	public bool CanCommitText => _getIme() is not null;

	readonly Func<AvalimeInputMethodService?> _getIme;

	public AndroidKeyboardHost(Func<AvalimeInputMethodService?> getIme){
		_getIme = getIme;
	}

	public void HideKeyboard(){
		var ime = _getIme();
		if(ime is null){
			return;
		}
		var handler = new Handler(Looper.MainLooper!);
		handler.Post(ime.HideKeyboard);
	}

	public void CommitText(str text){
		var ime = _getIme();
		ime?.CommitText(text);
	}
}
