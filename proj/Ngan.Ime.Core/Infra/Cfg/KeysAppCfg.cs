namespace Ngan.Ime.Infra.Cfg;

using Tsinswreng.CsCore;
using Tsinswreng.CsKeyNode;

[Doc($$"""
客戶端配置鍵族（CsKeyNode）。

*鍵與值分離*：鍵在這裡聲明（有靜態型別、有默認值、可跳轉），值從
{{nameof(AppCfg)}} 的兩個源裡按路徑取。業務側一律用 `AppCfg.Inst.Get(KeysAppCfg.Xxx)`，
不得出現字符串鍵名。

*路徑是分段而非字符串*：取值收的是路徑段清單，所以鍵裡的 `MkrKeyNode` 分隔符只用於顯示與日誌，
不參與取值。

*默認值的去處*：第三個參數就是默認值。配置裡沒有這個鍵時，`ICfgReaderExtn.Get<T>` 直接回它——
業務側不必再寫一次 `?? 16.0` 這種重複。*默認值的唯一出處就是這裡*，
其他型別（如 `AudioInputOptions`）若也有默認值，必須與這裡一致，並用註釋指明「本處只是鏡像」。

*新增鍵的流程*：①在此加 `KeyNode<T>`；②找到唯一讀取點；③若需要界面入口，同時補設置頁。
只加鍵不接讀取點等於死鍵——本檔已刪過一次（`Keyboard.Size` 的四個比例鍵從未有讀者）。
""")]
public class KeysAppCfg : KeysDeclBase<KeysAppCfg>, IStaticKeysDecl {

	[Doc(@"本族共用的節點製造器；分隔符只影響顯示，不參與取值。必須寫在鍵聲明之前：
	靜態字段按文字順序初始化，製造器晚於鍵就會是 null。")]
	public static MkrKeyNode _MkrKeyNode { get; } = new() { PathSep = "/" };

	[Doc("""
	引導鍵：運行時保存的那份配置檔的路徑，相對應用儲存區。
	它可以被開發者那份出廠配置改寫，所以實際位置要在讀過出廠配置之後才決定。

	這裡的默認值是默認檔名的*唯一*出處。
	""")]
	public static KeyNode<str> RwCfgPath = Mk(null, [nameof(RwCfgPath)], "Ngan.Ime.Rw.jsonc");

	[Doc("""
	日誌級別。宿主在讀完配置後把這個值套到 `AppLog.MinLevel` 與檔案 sink 上。
	0=Trace、1=Debug、2=Information、3=Warning、4=Error、5=Critical、6=None。
	默認 Information：熱路徑的 Debug 埋點只在排查時才值得留。
	""")]
	public static KeyNode<i32> LogMinLevel = Mk(null, [nameof(LogMinLevel)], 2);

	[Doc("librime 動態庫與 RimeTraits 的配置根。")]
	public static class Librime{

		[Doc("本組的根節點。")]
		public static KeyNode _R = Mk(null, [nameof(Librime)]);

		[Doc("是否自動鏈接 dll（實驗項；目前引擎初始化只認顯式 DllPath）。")]
		public static KeyNode<bool> AutoLinkDll = Mk(_R, [nameof(AutoLinkDll)], false);

		[Doc("librime 動態庫的絕對路徑。空串表示未配置，引擎初始化會直接失敗。")]
		public static KeyNode<str> DllPath = Mk(_R, [nameof(DllPath)], "");

		[Doc("RimeTraits 的各個字段，由 RimeSetup 填進 librime。鍵名刻意與 librime 的 struct 欄位同名，便於對照。")]
		public static class RimeTraits{

			[Doc("本組的根節點。父節點必須是 Librime：出廠配置檔裡的層級就是 `Librime/RimeTraits/…`。")]
			public static KeyNode _R = Mk(Librime._R, [nameof(RimeTraits)]);

			[Doc("共享資料目錄（schema、詞典）。空串表示不提供，由 librime 用其默認。")]
			public static KeyNode<str> shared_data_dir = Mk(_R, [nameof(shared_data_dir)], "");

			[Doc("用戶資料目錄（用戶詞典、配置）。空串表示未配置，引擎初始化會直接失敗。")]
			public static KeyNode<str> user_data_dir = Mk(_R, [nameof(user_data_dir)], "");

			[Doc("發行版名稱，供 Rime 顯示與日誌。")]
			public static KeyNode<str> distribution_name = Mk(_R, [nameof(distribution_name)], "");

			[Doc("發行版代號，通常等於宿主名。")]
			public static KeyNode<str> distribution_code_name = Mk(_R, [nameof(distribution_code_name)], "");

			[Doc("發行版版本號。")]
			public static KeyNode<str> distribution_version = Mk(_R, [nameof(distribution_version)], "");

			[Doc("應用名，供 Rime 建立用戶資料子目錄。")]
			public static KeyNode<str> app_name = Mk(_R, [nameof(app_name)], "rime.ngan.ime");

			[Doc("Rime 日誌級別（0=INFO，1=WARNING，2=ERROR，3=FATAL）。")]
			public static KeyNode<i32> min_log_level = Mk(_R, [nameof(min_log_level)], 0);

			[Doc("Rime 日誌目錄；null 表示讓 librime 用臨時目錄，空串表示只寫 stderr。")]
			public static KeyNode<str> log_dir = Mk(_R, [nameof(log_dir)], "");

			[Doc("預編譯資料目錄（可選）。")]
			public static KeyNode<str> prebuilt_data_dir = Mk(_R, [nameof(prebuilt_data_dir)], "");

			[Doc("部署暫存目錄（可選）。")]
			public static KeyNode<str> staging_dir = Mk(_R, [nameof(staging_dir)], "");
		}
	}

	[Doc("鍵盤外觀與行為。")]
	public static class Keyboard{

		[Doc("本組的根節點。")]
		public static KeyNode _R = Mk(null, [nameof(Keyboard)]);

		[Doc("是否啓用分體鍵盤。用戶在工具欄手動切換後，將狀態持久化到可寫配置。")]
		public static KeyNode<bool> IsSplitEnabled = Mk(_R, [nameof(IsSplitEnabled)], false);

		[Doc("是否啓用「Rime 未處理按鍵 → OS 發送前」的系統按鍵映射。")]
		public static KeyNode<bool> IsSystemKeyRemappingEnabled = Mk(_R, [nameof(IsSystemKeyRemappingEnabled)], false);

		[Doc("鍵盤字體。")]
		public static class Font{

			[Doc("本組的根節點。")]
			public static KeyNode _R = Mk(Keyboard._R, [nameof(Font)]);

			[Doc("字體檔路徑；空／null 表示用系統默認字體。")]
			public static KeyNode<str?> Path = Mk(_R, [nameof(Path)], (str?)null);

			[Doc("字體家族名；留空則由字體檔的 name 表推斷。")]
			public static KeyNode<str?> Family = Mk(_R, [nameof(Family)], (str?)null);

			[Doc("鍵帽基準字級（px）；其餘字級由它按比例推出，見 UiCfg。")]
			public static KeyNode<f64> BaseFontSize = Mk(_R, [nameof(BaseFontSize)], 32.0);
		}
	}

	[Doc("語音輸入的跨平臺配置根節點。")]
	public static class VoiceInput{

		[Doc("本組的根節點。")]
		public static KeyNode _R = Mk(null, [nameof(VoiceInput)]);

		[Doc("麥克風採集格式；Windows 和 Android 必須輸出相同 PCM 契約。")]
		public static class Audio{

			[Doc("本組的根節點。")]
			public static KeyNode _R = Mk(VoiceInput._R, [nameof(Audio)]);

			[Doc("採樣率（Hz）。16000 是火山流式模型的常用值。")]
			public static KeyNode<i32> SampleRate = Mk(_R, [nameof(SampleRate)], 16000);

			[Doc("聲道數。1 表示單聲道。")]
			public static KeyNode<i32> Channels = Mk(_R, [nameof(Channels)], 1);

			[Doc("每樣本位數。16 表示 16-bit little-endian PCM。")]
			public static KeyNode<i32> BitsPerSample = Mk(_R, [nameof(BitsPerSample)], 16);

			[Doc("每個音頻分片的時長（ms）。火山建議 100～200ms，取上界以降低包數與協議開銷。")]
			public static KeyNode<i32> ChunkDurationMs = Mk(_R, [nameof(ChunkDurationMs)], 200);

			[Doc("採集與發送之間最多緩衝的包數；滿了必須報錯，不得靜默丟音頻。")]
			public static KeyNode<i32> MaxBufferedChunks = Mk(_R, [nameof(MaxBufferedChunks)], 32);
		}

		[Doc("火山引擎 v3 流式識別配置。")]
		public static class Volcengine{

			[Doc("本組的根節點。")]
			public static KeyNode _R = Mk(VoiceInput._R, [nameof(Volcengine)]);

			[Doc("新版控制台的 API Key；有值時優先走 `X-Api-Key`。")]
			public static KeyNode<str> ApiKey = Mk(_R, [nameof(ApiKey)], "");

			[Doc("舊版控制台的 App ID；僅在 ApiKey 為空時配合 AccessToken 使用。")]
			public static KeyNode<str> AppId = Mk(_R, [nameof(AppId)], "");

			[Doc("舊版控制台的 Access Token。")]
			public static KeyNode<str> AccessToken = Mk(_R, [nameof(AccessToken)], "");

			[Doc("WebSocket v3 端點。")]
			public static KeyNode<str> Endpoint = Mk(_R, [nameof(Endpoint)], "wss://openspeech.bytedance.com/api/v3/sauc/bigmodel_nostream");

			[Doc("計費資源標識。`volc.bigasr.sauc.duration` 對應 1.0（bigmodel）小時版；`volc.seedasr.sauc.duration` 對應 2.0，與本專案使用的 `bigmodel` 模型名不一致，故默認取前者。")]
			public static KeyNode<str> ResourceId = Mk(_R, [nameof(ResourceId)], "volc.bigasr.sauc.duration");

			[Doc("首包裡的模型名稱。")]
			public static KeyNode<str> ModelName = Mk(_R, [nameof(ModelName)], "bigmodel");

			[Doc("建立 WebSocket 連接的最長等待（ms）。")]
			public static KeyNode<i64> ConnectTimeoutMs = Mk(_R, [nameof(ConnectTimeoutMs)], 10000L);

			[Doc("停止錄音後等待最終結果的最長時間（ms）。")]
			public static KeyNode<i64> FinalResultTimeoutMs = Mk(_R, [nameof(FinalResultTimeoutMs)], 15000L);

			[Doc("單條服務端消息允許的最大位元組數（解壓前）。")]
			public static KeyNode<i64> MaxServerMessageBytes = Mk(_R, [nameof(MaxServerMessageBytes)], 1024 * 1024L);

			[Doc("是否要求服務端做標點恢復。")]
			public static KeyNode<bool> EnablePunctuation = Mk(_R, [nameof(EnablePunctuation)], true);

			[Doc("是否要求服務端做數字、日期等文本規整（ITN）。")]
			public static KeyNode<bool> EnableInverseTextNormalization = Mk(_R, [nameof(EnableInverseTextNormalization)], true);

			[Doc("是否要求服務端返回分句，用於區分穩定段與臨時段。")]
			public static KeyNode<bool> ShowUtterances = Mk(_R, [nameof(ShowUtterances)], true);
		}
	}
}
