using Microsoft.Extensions.DependencyInjection;
using Tsinswreng.CsCore;
namespace Ngan.Ime.Core.Infra;

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

	public static T DiOrMk<T>()
		where T : class, IMk<T>
	{
		var Svc = SvcProvider?.GetService<T>();
		return Svc ?? T.Mk();
	}

}
