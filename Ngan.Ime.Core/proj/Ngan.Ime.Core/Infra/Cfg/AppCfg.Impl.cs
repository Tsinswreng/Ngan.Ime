//Impl: 聲明見 AppCfg.cs。
namespace Ngan.Ime.Infra.Cfg;

using Tsinswreng.CsCfg;
using Tsinswreng.CsCfg.Json;
using Ngan.Ime.Infra.Storage;

public partial class AppCfg{

	public partial void Setup(I_GetBaseDir BaseDir){
		// 門面的名字: 事件負載與日誌裡用它標識「這一層」
		Name = "app";
		var Dir = BaseDir.GetBaseDir();

		// 1: 出廠那份。檔案不存在就當空源; 開關檔與初始讀取都在源的建構子裡
		var Ro = new JsonFileCfgReader(Path.Combine(Dir, RoCfgFileName), RoSrcName);
		RoCfg = Ro;
		RoCfgPath = Ro.Path;

		// 2: 日常那份。位置由出廠配置的引導鍵決定, 所以必須先讀過出廠那份。
		//    這條耦合是本應用的策略, 庫不知道「配置裡有一個鍵指向配置檔」這種事
		var RwCfgRelPath = Ro.Get(KeysAppCfg.RwCfgPath);
		if(str.IsNullOrWhiteSpace(RwCfgRelPath)){
			// 走到這裡不是「沒有配置檔」而是「配置檔把引導鍵寫成空串」, 屬於部署或手改錯誤。
			// 直接讓它帶著鍵名崩掉, 比靜默退到某個默認檔名好查。
			throw KeysErr.Cfg.RwCfgPathEmpty.ToErr(nameof(KeysAppCfg.RwCfgPath));
		}
		var Rw = new JsonFileCfgAccessor(Path.Combine(Dir, RwCfgRelPath), RwSrcName);
		RwCfg = Rw;
		RwCfgPath = Rw.Path;

		// 3: 疊起來: 只讀在前、日常在後, 寫入只落日常那份(見 ILayeredCfgAccessorExtn.SetRoRw)。
		//    本應用只有一個只讀源, 所以清單裡放它一個
		this.SetRoRw([Ro], Rw);
	}
}
