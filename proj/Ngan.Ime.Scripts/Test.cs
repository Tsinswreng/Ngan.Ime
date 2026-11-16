namespace Ngan.Ime.Scripts;

using Tsinswreng.CsSh;

[Doc($$"""
以 `dotnet run` 直接運行測試專案（非 AOT 的快速驗證）。
""")]
internal static partial class Test{
	[Doc($$"""
	執行測試。輸出直接轉送終端，否則看不到測試結果。
	""")]
	internal static partial Task Main(ISCtx Ctx, CT Ct);
}

[Doc($$"""
以 win-x64 NativeAOT 發布測試專案，再運行發布物。
""")]
internal static partial class TestAotWin{
	[Doc($$"""
	發布並執行。
	""")]
	internal static partial Task Main(ISCtx Ctx, CT Ct);
}
