namespace Ngan.Ime;

using Microsoft.Extensions.DependencyInjection;
using Tsinswreng.CsCore;

[Doc("全局依賴注入管理")]
public class Di{
	public static IServiceProvider SvcProvider{
		get{
			return field ?? throw KeysErr.Sys.DiNotSetup.ToErr();
		}
		set{
			field = value ?? throw new ArgumentNullException(nameof(value));
		}
	}

	[Doc($$"""
	容器不存在時回 null 而不是拋。

	*為何需要它*：視圖在設計器預覽、單元測試或「宿主還沒建容器」的場合也會呼叫
	{{nameof(DiOrMk)}}；那種場合應該走型別自己的最小構造，而不是讓整個界面炸在解析上。
	""")]
	public static IServiceProvider? SvcProviderOrNull => field;

	[Doc($$"""
	取一個已註冊的服務；沒註冊或容器未建立就拋。
	#Prm[{{nameof(T)}}][要解析的服務型別。]
	#Rtn[容器裡的實例。]
	#Throw[{{nameof(TypedErr)}}][容器尚未建立（鍵為 KeysErr.Sys.DiNotSetup）時。]
	#Throw[{{nameof(InvalidOperationException)}}][容器已建立但該型別沒有註冊時。]
	""")]
	public static T GetRSvc<T>()
		where T:class
	{
		return SvcProvider.GetRequiredService<T>();
	}

	[Doc($$"""
	優先取容器中的註冊，沒有就用型別自己宣告的最小構造方式。

	*為何不用 `ActivatorUtilities`*：那個走反射，AOT 裁剪後可能失效；
	{{nameof(IMk<T>)}} 是編譯期就查得到的約定。
	""")]
	public static T DiOrMk<T>()
		where T : class, IMk<T>
	{
		var Svc = SvcProviderOrNull?.GetService<T>();
		return Svc ?? T.Mk();
	}

}
