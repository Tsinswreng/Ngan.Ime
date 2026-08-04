namespace Ngan.Ime.UI.Views.Key;
using Tsinswreng.CsCore;

[Doc(@$"輸入法觸摸鍵盤上的按鍵
不能給按鍵之間加無法點擊的縫隙 分隔線應僅是視覺上的作用 邏輯上 每個按鍵都緊密相連
")]
public interface IViewKey{
	[Doc("按鍵上顯示的文字")]
	public str Label{get;}

	[Doc(@$"上方小字提示")]
	public str UpperHint{get;}

	[Doc(@$"單擊")]
	public Task<nil> Click(CT Ct);

	[Doc(@$"長按")]
	public Task<nil> LongClick(CT Ct);


	[Doc(@$"上滑")]
	public Task<nil> SwipeUp(CT Ct);

	[Doc(@$"下滑")]
	public Task<nil> SwipeDown(CT Ct);

	[Doc(@$"左滑")]
	public Task<nil> SwipeLeft(CT Ct);

	[Doc(@$"右滑")]
	public Task<nil> SwipeRight(CT Ct);
}

