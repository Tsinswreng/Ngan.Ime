namespace Ngan.Ime.Infra.Cfg;

using Tsinswreng.CsCore;
using Tsinswreng.CsCfg;
using Tsinswreng.CsCfg.Json;
using Ngan.Ime.Infra.Storage;

[Doc($$"""
本應用的客戶端配置門面。

*雙源*: 兩份 JSON 配置檔疊在一起, 只讀那份在前——

1. `Ngan.Ime.Ro.jsonc`(出廠): 開發者手寫, 只讀。可以不存在, 不存在就是空源。
2. `Ngan.Ime.Rw.jsonc`(日常): 運行時保存的那份, 唯一可寫源。

*為何要分兩份*: 手寫那份要能放註釋、要能原樣留在版本庫裡; 而程式存檔是整份重寫,
註釋與排版一定保不住。兩件事放同一份檔案裡必然互相破壞, 所以分開。

*同一個鍵兩邊都有時*: 出廠那份贏。代價是界面上改了卻看不出為什麼沒生效,
故 `TryGet` 會吐出命中的源, 設定頁可以據此顯示「此項由配置文件接管」。

*哪些事屬於本類而不屬於庫*: 檔名、兩份檔誰是只讀誰是可寫、日常檔的位置由哪個配置鍵決定。
庫只知道「源」「優先級」「唯一可寫源」; 這三件事是本應用的決定, 所以裝配寫在這裡。

#Eg[
	```cs
	// 宿主在建立容器之前接好; 之後任何地方都能用
	AppCfg.Inst.Setup(AppDirBaseDirMgr.Inst);

	var FontSize = AppCfg.Inst.Get(KeysAppCfg.Keyboard.Font.BaseFontSize);
	```
]
""")]
public partial class AppCfg : LayeredCfgAccessor{

	[Doc("全局唯一實例。宿主的 `DiXxx` 在建容器之前呼叫 Setup，之後人人取這一個。")]
	public static AppCfg Inst { get; } = new();

	[Doc("開發者手寫的出廠配置檔名，位於應用儲存區下。可以不存在。")]
	public const str RoCfgFileName = "Ngan.Ime.Ro.jsonc";

	[Doc("出廠那份源的技術標識; 設定頁據此顯示「這一項由誰提供」。")]
	public const str RoSrcName = "file";

	[Doc("日常那份源的技術標識。")]
	public const str RwSrcName = "gui";

	[Doc(@$"
	出廠那份的讀者。{nameof(Setup)} 之後才有值; 檔案不存在時它是一個空源(取值全走鍵上宣告的默認值)。

	對外只給 {nameof(ICfgReader)}: 這一層不該有人以為它寫得進去。
	")]
	public ICfgReader? RoCfg { get; private set; }

	[Doc(@$"
	日常那份的訪問器。{nameof(Setup)} 之後才有值。

	對外只給 {nameof(ICfgAccessor)}: 呼叫方要寫配置就走這裡, 不要自己去開檔。
	")]
	public ICfgAccessor? RwCfg { get; private set; }

	[Doc(@$"出廠配置檔的實際路徑(絕對)。{nameof(Setup)} 之後才有值, 供日誌與診斷顯示。")]
	public str RoCfgPath { get; private set; } = "";

	[Doc(@$"日常配置檔的實際路徑(絕對)。{nameof(Setup)} 之後才有值, 供日誌與診斷顯示。")]
	public str RwCfgPath { get; private set; } = "";

	[Doc($$"""
	按應用儲存區接上兩個源並疊起來。宿主各呼叫一次，必須早於任何取配置的地方。

	日常配置檔的位置由引導鍵 {{nameof(KeysAppCfg.RwCfgPath)}} 決定, 它的默認值就是默認檔名;
	出廠檔裡寫了這個鍵就以出廠檔為準。這也是「出廠優先」在這裡唯一的必要理由——
	這個鍵若被界面覆蓋, 程式下次就找不到日常配置檔。

	*重跑 `Setup`* 會重建兩個源, 已訂閱舊源事件的訂閱者要自己重新訂閱。

	#Prm[{{nameof(BaseDir)}}][應用儲存區根目錄；桌面是執行檔目錄，Android 是外部私有目錄。]
	#Throw[{{nameof(TypedErr)}}][出廠配置把引導鍵寫成空串時。]
	#Throw[{{nameof(IOException)}}][檔案開不起來時；那屬於部署錯誤，不靜默降級。]
	""")]
	public partial void Setup(I_GetBaseDir BaseDir);
}
