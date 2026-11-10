namespace Ngan.Ime.Infra.Storage;

using Tsinswreng.CsCore;

[Doc($$"""
應用儲存區根目錄的抽象。

*為何要抽像*：桌面是「可執行檔目錄」、Android 是 `GetExternalFilesDir(null)`、Browser 沒有檔案系統。
三者語義相同（這份應用自己的可寫位置）、取得方式完全不同。呼叫方（日誌、配置）只依賴本介面，
宿主在建立容器時決定實現；換宿主不必改呼叫方。

*為何不要用當前工作目錄*：從 cmd 或捷徑啟動時當前目錄可能是任何地方，日誌與配置會跟著跑。

*Android 的注意點*：安卓應用的 pwd 皆為根目錄，故不可由相對路徑載入配置；凡路徑宜皆由此拼接。

#Eg[
	```cs
	var Dir = Di.GetRSvc<I_GetBaseDir>().GetBaseDir();
	var LogPath = Di.GetRSvc<I_GetBaseDir>().Combine("logs", "app.log");
	```
]
""")]
public interface I_GetBaseDir{
	[Doc($$"""
	取應用儲存區的根目錄（絕對路徑，不含結尾分隔符）。
	#Rtn[根目錄路徑。]
	#Throw[{{nameof(NotSupportedException)}}][宿主沒有檔案系統時（見 {nameof(NoBaseDirMgr)}）。]
	""")]
	public str GetBaseDir();
}

[Doc("拼接應用儲存區下的子路徑。不要自己接字串，否則分隔符與空段各處不一致。")]
public static class I_GetBaseDirExtn{
	[Doc($$"""
	把根目錄與若干路徑段拼成完整路徑。
	#Prm[{{nameof(z)}}][根目錄來源。]
	#Prm[{{nameof(Segs)}}][子路徑段，可多個。]
	#Rtn[{nameof(I_GetBaseDir.GetBaseDir)} 與 {nameof(Segs)} 的組合。]
	""")]
	public static str Combine(
		this I_GetBaseDir z
		,params IList<str> Segs
	){
		return Path.Combine([z.GetBaseDir(), ..Segs]);
	}
}
