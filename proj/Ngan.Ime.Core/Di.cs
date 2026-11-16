namespace Ngan.Ime;

using Microsoft.Extensions.DependencyInjection;

[Doc("全局依賴注入管理：進程唯一的服務容器，以及「取不到就退回最小構造」的取用入口。")]
public class Di{

	[Doc("容器的後備欄位；null 表示還沒有人設定。")]
	static IServiceProvider? ProviderField;

	[Doc($$"""
	進程唯一的容器。未設定時取用會拋，而不是回 null。

	#Throw[{{nameof(TypedErr)}}][容器尚未建立（鍵為 KeysErr.Sys.DiNotSetup）時。]
	""")]
	public static IServiceProvider SvcProvider{
		get{
			return ProviderField ?? throw KeysErr.Sys.DiNotSetup.ToErr();
		}
		set{
			ProviderField = value ?? throw new ArgumentNullException(nameof(value));
		}
	}

	[Doc($$"""
	容器不存在時回 null 而不是拋。

	*為何需要它*：視圖在設計器預覽、單元測試或「宿主還沒建容器」的場合也會呼叫
	{{nameof(DiOrMk)}}；那種場合應該走型別自己的最小構造，而不是讓整個界面炸在解析上。
	""")]
	public static IServiceProvider? SvcProviderOrNull => ProviderField;

	[Doc($$"""
	設定進程唯一的容器；**重複設定直接拋**。

	*為何要守衛*：先前 Android 允許兩個容器輪流覆蓋這個靜態欄位，
	於是舊視圖綁舊容器的單例、新視圖綁新容器的單例，同一份界面狀態分裂成兩份。
	容器是進程級的，第二次設定一定是生命週期寫錯。

	#Prm[{{nameof(Provider)}}][已建好的容器。]
	#Throw[{{nameof(TypedErr)}}][容器已存在（鍵為 KeysErr.Sys.DiAlreadySetup）時。]
	""")]
	public static void SetSvcProvider(IServiceProvider Provider){
		if(SvcProviderOrNull is not null){
			throw KeysErr.Sys.DiAlreadySetup.ToErr();
		}
		SvcProvider = Provider;
	}

	[Doc($$"""
	取一個已註冊的服務；沒註冊或容器未建立就拋。
	#Prm[{{nameof(T)}}][要解析的服務型別。]
	#Rtn[容器裡的實例。]
	#Throw[{{nameof(TypedErr)}}][容器尚未建立（鍵為 KeysErr.Sys.DiNotSetup）時。]
	#Throw[{{nameof(InvalidOperationException)}}][容器已建立但該型別沒有註冊時。]
	""")]
	public static T GetRSvc<T>()
		where T : class
	{
		return SvcProvider.GetRequiredService<T>();
	}

	[Doc($$"""
	優先取容器中的註冊，沒有就用型別自己宣告的最小構造方式。

	*為何不用 `ActivatorUtilities`*：那個走反射，AOT 裁剪後可能失效；
	{{nameof(IMk<T>)}} 是編譯期就查得到的約定。

	*注意*：若型別「有註冊但建構子依賴缺失」，DI 會拋而不是回 null，
	故本方法的兜底只覆蓋「整個型別沒註冊」這一種情況——這正是設計器預覽會遇到的。
	""")]
	public static T DiOrMk<T>()
		where T : class, IMk<T>
	{
		var Svc = SvcProviderOrNull?.GetService<T>();
		return Svc ?? T.Mk();
	}
}
