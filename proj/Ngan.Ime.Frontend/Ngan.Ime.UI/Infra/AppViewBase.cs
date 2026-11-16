namespace Ngan.Ime.UI.Infra;

using Avalonia.Markup.Declarative;
using Ngan.Ime.Infra;

[Doc($$"""
所有視圖的基類：`Avalonia.Markup.Declarative` 的 ViewBase<TVm> 加上本專案的取用慣例。

*子類要覆寫的只有 `Build(TVm vm)`*：回傳控件樹的根。控件樹在第一次掛到可視樹時才建立，
故建構子只負責取得 ViewModel，不建控件、不跑耗時工作。
`OnAfterInitialized()` 在控件樹與樣式都建好後觸發，耗時初始化放那裡。

*為何有 `vm` 欄位*：`Build(vm)` 的參數就是它；另開一個欄位是為了讓
`OnAfterInitialized`、事件處理器也能直接取用，不必再從 `DataContext` 轉型。

*ViewModel 從哪來*：默認 {{nameof(MkCtx)}} 走容器（`Di.DiOrMk`），
子類可覆寫成「用父視圖傳進來的實例」或「測試替身」——這是 View 可測的唯一入口。

#Eg[
	```cs
	public partial class ViewSample : AppViewBase<VmSample>{
		protected override object Build(VmSample vm){
			return new StackGrid(IsRow: true).Rows("Auto,*").Children(
				new TextBlock().Text(vm, X => X.StatusText)
			);
		}
	}
	```
]
""")]
public abstract partial class AppViewBase<TVm> : ViewBase<TVm>
	where TVm : class, IMk<TVm>
{

	[Doc("本視圖的 ViewModel；建構子完成後即可用。")]
	protected TVm vm = default!;

	[Doc("取得 ViewModel 並設為 DataContext；子類不得再做耗時工作。")]
	protected AppViewBase(){
		vm = MkCtx();
		ViewModel = vm;
		DataContext = vm;
	}

	[Doc($$"""
	取得 ViewModel：默認走容器，沒有容器時走型別自己的 `Mk()`（見 {nameof(Di.DiOrMk)}）。
	#Rtn[本視圖使用的 ViewModel。]
	""")]
	protected virtual TVm MkCtx(){
		return Di.DiOrMk<TVm>();
	}
}
