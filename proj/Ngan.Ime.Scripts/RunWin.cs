namespace Ngan.Ime.Scripts;

using Tsinswreng.CsSh;

[Doc($$"""
以 win-x86 建置並啟動 Windows 服務端（取代原本的 `RunWin.sh`）。

為何是 win-x86：本機的 `rime.dll` 來自 weasel 0.15.0，是 32 位版本；
服務端必須以同一字寬的進程載入它，否則 `NativeLibrary` 會直接失敗。
""")]
internal static partial class RunWin{
	[Doc($$"""
	執行：把配置檔放到輸出目錄、再 `dotnet run` 啟動服務端。
	""")]
	internal static partial Task Main(ISCtx Ctx, CT Ct);
}
