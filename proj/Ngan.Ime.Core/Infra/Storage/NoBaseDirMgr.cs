namespace Ngan.Ime.Infra.Storage;

using Tsinswreng.CsCore;

[Doc($$"""
沒有檔案系統的宿主（Browser／WASM）用的 {{nameof(I_GetBaseDir)}} 實現。

*為何不直接給一個路徑*：WASM 下的「當前目錄」是虛擬的，任何寫入都必定失敗且沒有提示，
於是一個看起來成功的日誌檔實際上一行也沒寫。在這裡明確拋出，
讓「把檔案 API 用在不該用的宿主」當場暴露。

#Throw[{{nameof(NotSupportedException)}}][呼叫 {{nameof(GetBaseDir)}} 時必定拋出。]
""")]
public class NoBaseDirMgr : I_GetBaseDir {
	[Doc("全局單例；Browser 入口註冊進容器時用的就是它。")]
	public static NoBaseDirMgr Inst => field ??= new NoBaseDirMgr();

	[Doc("本宿主沒有應用儲存區目錄，故一律拒絕，不做任何降級。")]
	public str GetBaseDir() {
		throw KeysErr.Storage.NoBaseDir.ToErr();
	}
}
