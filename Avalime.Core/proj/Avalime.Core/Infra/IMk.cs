namespace Avalime.Core.Infra;

/// <summary>
/// 爲 `DiOrMk<T>()` 提供顯式的兜底構造約定。
/// 當 DI 容器中沒有註冊某個類型時，改由類型自己決定最小可用的創建方式，
/// 避免 `ActivatorUtilities` 在 AOT / 反射裁剪場景下隱式構造。
/// </summary>
/// <typeparam name="T">具體可創建的類型。</typeparam>
public interface IMk<T>
	where T : class
{
	/// <summary>
	/// 在未命中 DI 註冊時，返回一個最小可用實例。
	/// </summary>
	static abstract T Mk();
}
