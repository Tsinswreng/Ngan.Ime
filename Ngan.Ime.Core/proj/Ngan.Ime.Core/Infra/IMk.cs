namespace Ngan.Ime.Core.Infra;

/// 爲 `DiOrMk<T>()` 提供顯式的兜底構造約定。
/// 當 DI 容器中沒有註冊某個類型時，改由類型自己決定最小可用的創建方式，
/// 避免 `ActivatorUtilities` 在 AOT / 反射裁剪場景下隱式構造。
/// <typeparam name="T">具體可創建的類型。</typeparam>
public interface IMk<T>
	where T : class
{
	static abstract T Mk();
}
