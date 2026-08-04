using System.Collections.ObjectModel;
using System.ComponentModel;
using Ngan.Ime.Core.Infra;
using Ngan.Ime.ViewModels;

namespace Ngan.Ime.UI.Views.Clipboard;

public class VmClipboard : ViewModelBase
	, IMk<VmClipboard>
	, IDisposable {
	public ImeUiState UiState { get; }
	public IClipboardService ClipboardService { get; }
	public IKeyboardHost KeyboardHost { get; }

	public ObservableCollection<VmClipboardItem> Items {
		get;
		set => SetProperty(ref field, value);
	} = [];

	readonly PropertyChangedEventHandler _uiStatePropertyChangedHandler;

	public VmClipboard(ImeUiState UiState, IClipboardService ClipboardService, IKeyboardHost KeyboardHost) {
		this.UiState = UiState;
		this.ClipboardService = ClipboardService;
		this.KeyboardHost = KeyboardHost;
		_uiStatePropertyChangedHandler = async (_, e) => {
			if (e.PropertyName == nameof(ImeUiState.IsClipboardVisible) && UiState.IsClipboardVisible) {
				await RefreshAsy();
			}
		};
		UiState.PropertyChanged += _uiStatePropertyChangedHandler;
	}

	/// <summary>
	/// 剪貼板 VM 的兜底創建路徑。
	/// </summary>
	public static VmClipboard Mk() {
		return new VmClipboard(
			Di.GetRSvc<ImeUiState>(),
			Di.GetRSvc<IClipboardService>(),
			Di.GetRSvc<IKeyboardHost>()
		);
	}

	public async Task RefreshAsy(CT Ct = default) {
		var texts = await ClipboardService.GetItemsAsy(Ct);
		var items = new ObservableCollection<VmClipboardItem>();
		foreach (var text in texts.Distinct()) {
			items.Add(new VmClipboardItem {
				Text = text,
				Click = () => {
					KeyboardHost.CommitText(text);
					UiState.ExitClipboard();
				}
			});
		}
		Items = items;
	}

	public void Dispose() {
		UiState.PropertyChanged -= _uiStatePropertyChangedHandler;
	}
}

public class VmClipboardItem : ViewModelBase {
	public str Text {
		get;
		set => SetProperty(ref field, value);
	} = "";

	public Action? Click { get; set; }
}

