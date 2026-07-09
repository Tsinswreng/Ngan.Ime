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
}
