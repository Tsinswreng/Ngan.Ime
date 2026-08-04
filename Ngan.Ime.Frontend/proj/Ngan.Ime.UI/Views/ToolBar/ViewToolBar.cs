using System.ComponentModel;
using Ngan.Ime.UI.Icons;
using Ngan.Ime.UI.Infra;
using Avalonia.Controls;
using Tsinswreng.Avln.Grid;

namespace Ngan.Ime.UI.Views.ToolBar;
using Ctx = VmToolBar;

/// 輸入法頂部工具欄視圖聲明。
/// 各模式入口的圖標控件均獨立暴露，便於綁定選中高亮及進行視圖測試。
public partial class ViewToolBar : AppViewBase<Ctx>, IDisposable{
	/// 工具欄水平根版面；每個功能入口佔一個等寬欄位。
	public GridStack Root = new(IsRow: false);

	/// 工具欄使用的共享 UI 狀態；目前用於同步頂欄高度。
	public readonly ImeUiState _uiState;

	/// 共享 UI 狀態變更處理器；釋放視圖時必須解除訂閱。
	public PropertyChangedEventHandler? _uiStatePropertyChangedHandler;

	/// 剪貼板入口圖標。
	/// 剪貼板頁打開時，其填充色應綁定爲 `VmToolBar.ClipboardForeground`。
	public Icon? ClipboardIcon;

	/// Rime 日誌入口圖標。
	/// 日誌頁由手動或系統強制顯示時，其填充色應綁定爲 `VmToolBar.RimeLogForeground`。
	public Icon? RimeLogIcon;

	/// 語音輸入入口圖標。
	/// 語音頁打開時，其填充色綁定爲 `VmToolBar.VoiceInputForeground`。
	public Icon? VoiceInputIcon;

	/// 建立工具欄、解析 ViewModel 並組裝控件樹。
	public partial ViewToolBar();

	/// 組裝工具欄中的簡繁、註釋、剪貼板、日誌、語音及其他功能入口。
	private partial void Render();

	/// 根據候選註釋顯示狀態取得工具欄高度。
	private partial f64 GetTopBarHeight();

	/// 候選註釋顯示狀態改變後，同步工具欄自身高度。
	private partial void SyncHeight();

	/// 建立具有統一黑色背景與分隔線的工具欄按鈕容器。
	private static partial Border MkBtn();

	/// 解除共享狀態訂閱並釋放工具欄 ViewModel。
	public partial void Dispose();
}
