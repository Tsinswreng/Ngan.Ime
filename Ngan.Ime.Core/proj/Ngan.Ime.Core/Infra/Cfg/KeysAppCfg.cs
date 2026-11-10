namespace Ngan.Ime.Infra.Cfg;

using Tsinswreng.CsCore;
using Tsinswreng.CsKeyNode;

[Doc($$"""
客戶端配置鍵族（CsKeyNode）。

*鍵與值分離*：鍵在這裡聲明（有靜態型別、有默認值、可跳轉），值從
{{nameof(AppCfg)}} 的兩個源裡按路徑取。業務側一律用 `AppCfg.Inst.Get(KeysAppCfg.Xxx)`，
不得出現字符串鍵名。

*路徑是分段而非字符串*：取值收的是路徑段清單，所以鍵裡的
{{nameof(MkrKeyNode)}} 分隔符只用於顯示與日誌，不參與取值。

*默認值的去處*：第三個參數就是默認值。配置裡沒有這個鍵時，
`ICfgReaderExtn.Get<T>` 直接回它——業務側不必再寫一次 `?? 16.0` 這種重複。
""")]
public class KeysAppCfg : KeysDeclBase<KeysAppCfg>, IStaticKeysDecl {

	[Doc("本族共用的節點製造器；分隔符只影響顯示，不參與取值。要寫在鍵聲明之前。")]
	public static MkrKeyNode _MkrKeyNode { get; } = new() { PathSep = "/" };

	[Doc("""
	引導鍵：運行時保存的那份配置檔的路徑，相對應用儲存區。
	它可以被開發者那份出廠配置改寫，所以實際位置要在讀過出廠配置之後才決定。

	這裡的默認值是默認檔名的*唯一*出處。
	""")]
	public static KeyNode<str> RwCfgPath = Mk(null, [nameof(RwCfgPath)], "Ngan.Ime.Rw.jsonc");

	[Doc("librime 動態庫與 RimeTraits 的配置根。")]
	public static class Librime{
		public static KeyNode _R = Mk(null, [nameof(Librime)]);

		[Doc("是否自動鏈接 dll（實驗項；目前引擎初始化只認顯式 DllPath）。")]
		public static KeyNode<bool> AutoLinkDll = Mk(_R, [nameof(AutoLinkDll)], false);

		[Doc("librime 動態庫的絕對路徑。空串表示未配置，引擎初始化會直接失敗。")]
		public static KeyNode<str> DllPath = Mk(_R, [nameof(DllPath)], "");

		[Doc("RimeTraits 的各個字段，由 RimeSetup 填進 librime。")]
		public static class RimeTraits{
			// 父節點必須是 Librime 這一個：出廠配置檔裡的層級就是 Librime/RimeTraits/…，
			// 舊寫法把它掛在根上，導致配置文件裡的這段整段讀不到（只有宿主補的默認值生效）。
			public static KeyNode _R = Mk(Librime._R, [nameof(RimeTraits)]);

			public static KeyNode<str> shared_data_dir = Mk(_R, [nameof(shared_data_dir)], "");
			public static KeyNode<str> user_data_dir = Mk(_R, [nameof(user_data_dir)], "");
			public static KeyNode<str> distribution_name = Mk(_R, [nameof(distribution_name)], "");
			public static KeyNode<str> distribution_code_name = Mk(_R, [nameof(distribution_code_name)], "");
			public static KeyNode<str> distribution_version = Mk(_R, [nameof(distribution_version)], "");
			public static KeyNode<str> app_name = Mk(_R, [nameof(app_name)], "rime.ngan.ime");
			public static KeyNode<i32> min_log_level = Mk(_R, [nameof(min_log_level)], 0);
			public static KeyNode<str> log_dir = Mk(_R, [nameof(log_dir)], "");
			public static KeyNode<str> prebuilt_data_dir = Mk(_R, [nameof(prebuilt_data_dir)], "");
			public static KeyNode<str> staging_dir = Mk(_R, [nameof(staging_dir)], "");
		}
	}

	[Doc("鍵盤外觀與行為。")]
	public static class Keyboard{
		public static KeyNode _R = Mk(null, [nameof(Keyboard)]);

		[Doc("是否啓用分體鍵盤。用戶在工具欄手動切換後，將狀態持久化到可寫配置。")]
		public static KeyNode<bool> IsSplitEnabled = Mk(_R, [nameof(IsSplitEnabled)], false);

		[Doc("是否啓用「Rime 未處理按鍵 -> OS 發送前」的系統按鍵映射。")]
		public static KeyNode<bool> IsSystemKeyRemappingEnabled = Mk(_R, [nameof(IsSystemKeyRemappingEnabled)], false);

		[Doc("鍵盤字體。")]
		public static class Font{
			public static KeyNode _R = Mk(Keyboard._R, [nameof(Font)]);

			[Doc("字體檔路徑；空／null 表示用系統默認字體。")]
			public static KeyNode<str?> Path = Mk(_R, [nameof(Path)], (str?)null);

			[Doc("字體家族名；留空則由字體檔的 name 表推斷。")]
			public static KeyNode<str?> Family = Mk(_R, [nameof(Family)], (str?)null);

			public static KeyNode<f64> BaseFontSize = Mk(_R, [nameof(BaseFontSize)], 32.0);
		}

		[Doc("鍵盤尺寸比例。")]
		public static class Size{
			public static KeyNode _R = Mk(Keyboard._R, [nameof(Size)]);

			[Doc("寬高比例的公共聲明。")]
			public class _SizeBase{
				// 注意：這裡的 `_R` 解析到外層 Size._R（本類沒有自己的 _R），
				// 故四個子鍵實際路徑是 Keyboard/Size/WidthRatio 等，而非 Keyboard/Size/Portrait/…。
				// 現階段沒有讀取點，保留原語義以免動到既有配置檔；要改須先確認檔名與檔內層級。
				[Doc("寬度比例。鍵盤寬度佔整個屏幕的寬度")]
				public static KeyNode<f64> WidthRatio = Mk(_R, [nameof(WidthRatio)], 0.0);

				[Doc("高度比例。鍵盤高度佔整個屏幕的高度")]
				public static KeyNode<f64> HeightRatio = Mk(_R, [nameof(HeightRatio)], 0.0);
			}

			[Doc("豎屏")]
			public class Portrait: _SizeBase{
				public static KeyNode _R = Mk(Size._R, [nameof(Portrait)]);
			}

			[Doc("橫屏")]
			public class Landscape: _SizeBase{
				public static KeyNode _R = Mk(Size._R, [nameof(Landscape)]);
			}
		}
	}

	[Doc("語音輸入的跨平臺配置根節點。")]
	public static class VoiceInput{
		public static KeyNode _R = Mk(null, [nameof(VoiceInput)]);

		[Doc("麥克風採集格式；Windows 和 Android 應輸出相同 PCM 契約。")]
		public static class Audio{
			public static KeyNode _R = Mk(VoiceInput._R, [nameof(Audio)]);

			public static KeyNode<i32> SampleRate = Mk(_R, [nameof(SampleRate)], 16000);
			public static KeyNode<i32> Channels = Mk(_R, [nameof(Channels)], 1);
			public static KeyNode<i32> BitsPerSample = Mk(_R, [nameof(BitsPerSample)], 16);
			public static KeyNode<i32> ChunkDurationMs = Mk(_R, [nameof(ChunkDurationMs)], 200);
			public static KeyNode<i32> MaxBufferedChunks = Mk(_R, [nameof(MaxBufferedChunks)], 32);
		}

		[Doc("火山引擎 v3 流式識別配置。")]
		public static class Volcengine{
			public static KeyNode _R = Mk(VoiceInput._R, [nameof(Volcengine)]);

			public static KeyNode<str> ApiKey = Mk(_R, [nameof(ApiKey)], "");
			public static KeyNode<str> AppId = Mk(_R, [nameof(AppId)], "");
			public static KeyNode<str> AccessToken = Mk(_R, [nameof(AccessToken)], "");
			public static KeyNode<str> Endpoint = Mk(_R, [nameof(Endpoint)], "wss://openspeech.bytedance.com/api/v3/sauc/bigmodel_nostream");
			public static KeyNode<str> ResourceId = Mk(_R, [nameof(ResourceId)], "volc.seedasr.sauc.duration");
			public static KeyNode<str> ModelName = Mk(_R, [nameof(ModelName)], "bigmodel");
			public static KeyNode<i64> ConnectTimeoutMs = Mk(_R, [nameof(ConnectTimeoutMs)], 10000L);
			public static KeyNode<i64> FinalResultTimeoutMs = Mk(_R, [nameof(FinalResultTimeoutMs)], 15000L);
			public static KeyNode<i64> MaxServerMessageBytes = Mk(_R, [nameof(MaxServerMessageBytes)], 1024 * 1024L);
			public static KeyNode<bool> EnablePunctuation = Mk(_R, [nameof(EnablePunctuation)], true);
			public static KeyNode<bool> EnableInverseTextNormalization = Mk(_R, [nameof(EnableInverseTextNormalization)], true);
			public static KeyNode<bool> ShowUtterances = Mk(_R, [nameof(ShowUtterances)], true);
		}
	}
}
