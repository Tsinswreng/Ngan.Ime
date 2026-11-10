namespace Ngan.Ime.Scripts;

using Tsinswreng.CsSh;

/// 掃描 `IAppSerializable` 的實現者，重寫 JSON 源生成上下文登記檔。
internal static partial class GenAppJsonCtx{
	/// 執行生成並印出統計。
	internal static partial Task Main(ISCtx Ctx, CT Ct);
}
