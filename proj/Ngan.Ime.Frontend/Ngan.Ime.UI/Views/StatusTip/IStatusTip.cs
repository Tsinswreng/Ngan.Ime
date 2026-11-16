namespace Ngan.Ime.UI.Views.StatusTip;

using Avalonia;
using Tsinswreng.CsCore;

[Doc($$"""
光標旁的狀態提示（中／英、簡／繁切換時閃一下）。

*為何獨立於候選浮窗*：Weasel 把狀態圖示畫在候選面板裡（`WeaselUI/StandardLayout.cpp:391` 的
`ShouldDisplayStatusIcon`），因為它的面板與 TSF 同行程；我們的候選浮窗只在組字時出現，
切換模式時沒有東西可依附，所以另開一個小視窗。

*文字*：英文模式 `A`；中文模式繁體 `漢`、簡體 `汉`。簡繁切換後顯示切換後的狀態。
""")]
public interface IStatusTip{

	[Doc($$"""
	顯示提示；Anchor 為 null 時只更新內容、不動位置。
	#Prm[{{nameof(Text)}}][要顯示的字（單字）。]
	#Prm[{{nameof(Anchor)}}][插入點下緣的螢幕座標。]
	""")]
	public void Show(str Text, PixelPoint? Anchor);

	[Doc("收起提示；已收起時無副作用。")]
	public void Hide();
}
