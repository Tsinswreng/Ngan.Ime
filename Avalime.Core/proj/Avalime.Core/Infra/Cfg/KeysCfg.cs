namespace Avalime.Core.Infra;

using Tsinswreng.CsCfg;
using static Tsinswreng.CsCfg.CfgNode<obj?>;

public class KeysCfg{
	public static ICfgNode<str> RwCfgPath = Mk(null, [nameof(RwCfgPath)], "Avalime.Rw.jsonc");

	public static class Librime{
		public static ICfgNode _R = Mk(null, [nameof(Librime)]);
		public static ICfgNode<bool> AutoLinkDll = Mk(_R, [nameof(AutoLinkDll)], false);
		public static ICfgNode<str> DllPath = Mk(_R, [nameof(DllPath)], "");

		public static class RimeTraits{
			public static ICfgNode _R = Mk(null, [nameof(RimeTraits)]);
			public static ICfgNode<str> shared_data_dir = Mk(_R, [nameof(shared_data_dir)], "");
			public static ICfgNode<str> user_data_dir = Mk(_R, [nameof(user_data_dir)], "");
			public static ICfgNode<str> distribution_name = Mk(_R, [nameof(distribution_name)], "");
			public static ICfgNode<str> distribution_code_name = Mk(_R, [nameof(distribution_code_name)], "");
			public static ICfgNode<str> distribution_version = Mk(_R, [nameof(distribution_version)], "");
			public static ICfgNode<str> app_name = Mk(_R, [nameof(app_name)], "rime.avalime");
			public static ICfgNode<i32> min_log_level = Mk(_R, [nameof(min_log_level)], 0);
			public static ICfgNode<str> log_dir = Mk(_R, [nameof(log_dir)], "");
			public static ICfgNode<str> prebuilt_data_dir = Mk(_R, [nameof(prebuilt_data_dir)], "");
			public static ICfgNode<str> staging_dir = Mk(_R, [nameof(staging_dir)], "");
		}
	}
	public class Keyboard{
		public static ICfgNode _R = Mk(null, [nameof(Keyboard)]);
		/// 是否啓用分體鍵盤。用戶在工具欄手動切換後，將狀態持久化到可寫配置。
		public static ICfgNode<bool> IsSplitEnabled = Mk(_R, [nameof(IsSplitEnabled)], false);
		/// 是否啓用“Rime 未處理按鍵 -> OS 發送前”的系統按鍵映射。
		public static ICfgNode<bool> IsSystemKeyRemappingEnabled = Mk(_R, [nameof(IsSystemKeyRemappingEnabled)], false);
		public class Font{
			public static ICfgNode _R = Mk(Keyboard._R, [nameof(Font)]);
			public static ICfgNode<str?> Path = Mk(_R, [nameof(Path)], (str?)null);
			public static ICfgNode<str?> Family = Mk(_R, [nameof(Family)], (str?)null);
			public static ICfgNode<f64> BaseFontSize = Mk(_R, [nameof(BaseFontSize)], 32.0);
		}

		public class Size{
			public static ICfgNode _R = Mk(Keyboard._R, [nameof(Size)]);

			public class _SizeBase{
				[Doc("寬度比例。鍵盤寬度佔整個屏幕的寬度")]
				public static ICfgNode<f64> WidthRatio = Mk(_R, [nameof(WidthRatio)], 0.0);
				public static ICfgNode<f64> HeightRatio = Mk(_R, [nameof(HeightRatio)], 0.0);
			}

			[Doc("豎屏")]
			public class Portrait: _SizeBase{
				public static ICfgNode _R = Mk(Size._R, [nameof(Portrait)]);

			}

			[Doc("橫屏")]
			public class Landscape: _SizeBase{
				public static ICfgNode _R = Mk(Size._R, [nameof(Landscape)]);
			}


		}

	}

	/// 語音輸入的跨平臺配置根節點。
	public static class VoiceInput{
		public static ICfgNode _R = Mk(null, [nameof(VoiceInput)]);

		/// 麥克風採集格式；Windows 和 Android 應輸出相同 PCM 契約。
		public static class Audio{
			public static ICfgNode _R = Mk(VoiceInput._R, [nameof(Audio)]);
			public static ICfgNode<i32> SampleRate = Mk(_R, [nameof(SampleRate)], 16000);
			public static ICfgNode<i32> Channels = Mk(_R, [nameof(Channels)], 1);
			public static ICfgNode<i32> BitsPerSample = Mk(_R, [nameof(BitsPerSample)], 16);
			public static ICfgNode<i32> ChunkDurationMs = Mk(_R, [nameof(ChunkDurationMs)], 200);
			public static ICfgNode<i32> MaxBufferedChunks = Mk(_R, [nameof(MaxBufferedChunks)], 32);
		}

		/// 火山引擎 v3 流式識別配置。
		public static class Volcengine{
			public static ICfgNode _R = Mk(VoiceInput._R, [nameof(Volcengine)]);
			public static ICfgNode<str> ApiKey = Mk(_R, [nameof(ApiKey)], "");
			public static ICfgNode<str> AppId = Mk(_R, [nameof(AppId)], "");
			public static ICfgNode<str> AccessToken = Mk(_R, [nameof(AccessToken)], "");
			public static ICfgNode<str> Endpoint = Mk(_R, [nameof(Endpoint)], "wss://openspeech.bytedance.com/api/v3/sauc/bigmodel_nostream");
			public static ICfgNode<str> ResourceId = Mk(_R, [nameof(ResourceId)], "volc.seedasr.sauc.duration");
			public static ICfgNode<str> ModelName = Mk(_R, [nameof(ModelName)], "bigmodel");
			public static ICfgNode<i64> ConnectTimeoutMs = Mk(_R, [nameof(ConnectTimeoutMs)], 10000L);
			public static ICfgNode<i64> FinalResultTimeoutMs = Mk(_R, [nameof(FinalResultTimeoutMs)], 15000L);
			public static ICfgNode<i64> MaxServerMessageBytes = Mk(_R, [nameof(MaxServerMessageBytes)], 1024 * 1024L);
			public static ICfgNode<bool> EnablePunctuation = Mk(_R, [nameof(EnablePunctuation)], true);
			public static ICfgNode<bool> EnableInverseTextNormalization = Mk(_R, [nameof(EnableInverseTextNormalization)], true);
			public static ICfgNode<bool> ShowUtterances = Mk(_R, [nameof(ShowUtterances)], true);
		}
	}
}
