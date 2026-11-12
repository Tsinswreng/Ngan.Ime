namespace Ngan.Ime.Android;

using Tsinswreng.CsCore;

[Doc($$"""
Android 鍵盤宿主：把「隱藏鍵盤」與「上屏」轉給當前輸入法服務。

*為何透過 {{nameof(AndroidImeRuntime)}}*：容器在 `Application` 期間建立，
而服務實例在之後才有；用執行期句柄取得，避免把服務塞進容器或改寫單例的屬性。

#Eg[
	```cs
	Svc.AddSingleton<IKeyboardHost, AndroidKeyboardHost>();
	```
]
""")]
public class AndroidKeyboardHost : Ngan.Ime.UI.IKeyboardHost{

	[Doc("有活動服務時才可能上屏。")]
	public bool CanCommitText => AndroidImeRuntime.Inst.GetService() is not null;

	[Doc("在主線程請求隱藏鍵盤。")]
	public void HideKeyboard(){
		var Ime = AndroidImeRuntime.Inst.GetService();
		if(Ime is null){
			return;
		}
		AndroidImeRuntime.Inst.Post(Ime.HideKeyboard);
	}

	[Doc("把文字交給當前輸入法服務上屏。")]
	public void CommitText(str Text){
		AndroidImeRuntime.Inst.GetService()?.CommitText(Text);
	}
}
