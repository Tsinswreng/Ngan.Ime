using Microsoft.Extensions.DependencyInjection;
using Tsinswreng.CsCore;
namespace Avalime.Core.Infra;

[Doc("全局依賴注入管理")]
public class Di{
	public static IServiceProvider SvcProvider{
		get{
			return field ?? throw new InvalidOperationException("Di.SvcP has not been initialized.");
		}
		set{
			field = value ?? throw new ArgumentNullException(nameof(value));
		}
	}

	public static T GetRSvc<T>()
		where T:class
	{
		return SvcProvider.GetRequiredService<T>();
	}

	/// <summary>
	/// 先嘗試從 DI 取服務；若當前類型未註冊，則退回到類型自身定義的 `Mk()`。
	/// 這樣可避免 `ActivatorUtilities` 對反射構造的依賴，更適合 AOT 場景。
	/// </summary>
	public static T DiOrMk<T>()
		where T : class, IMk<T>
	{
		var Svc = SvcProvider?.GetService<T>();
		return Svc ?? T.Mk();
	}

}
