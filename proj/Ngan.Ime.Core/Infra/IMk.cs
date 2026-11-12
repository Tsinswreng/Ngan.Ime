namespace Ngan.Ime.Infra;

using Tsinswreng.CsCore;

[Doc($$"""
爲 `Di.DiOrMk<T>()` 提供顯式的兜底構造約定。

當 DI 容器中沒有註冊某個類型時，改由類型自己決定最小可用的創建方式，
避免 `ActivatorUtilities` 在 AOT / 反射裁剪場景下隱式構造。

#Eg[
	```cs
	public class VmKey : IMk<VmKey> {
		public static VmKey Mk() => new VmKey();
	}
	```
]
""")]
public interface IMk<T>
	where T : class
{
	[Doc("容器沒有註冊時使用的最小可用實例。")]
	static abstract T Mk();
}
