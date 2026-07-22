using System.Collections.ObjectModel;
using Avalime.Core.Language;

namespace Avalime.UI.Views.VoiceInput;

/// 語音语言选择页的静态目录与搜索实现。
/// 正式 Provider 接入后只需用其支持语言调用 `LoadRows`，搜索逻辑无需修改。
public partial class VmVoiceInputLangSelector{
	/// 建立覆盖首轮目标语言的预览目录。
	public static partial VmVoiceInputLangSelector Mk(){
		var R = new VmVoiceInputLangSelector();
		R.LoadRows([
			new NormLangDetail{Code = "zh-CN", NativeName = "中文（中國）", EnglishName = "Chinese (China)"},
			new NormLangDetail{Code = "zh-TW", NativeName = "中文（臺灣）", EnglishName = "Chinese (Taiwan)"},
			new NormLangDetail{Code = "en-US", NativeName = "English (United States)", EnglishName = "English (United States)"},
			new NormLangDetail{Code = "ja-JP", NativeName = "日本語（日本）", EnglishName = "Japanese (Japan)"},
			new NormLangDetail{Code = "es-MX", NativeName = "Español (México)", EnglishName = "Spanish (Mexico)"},
			new NormLangDetail{Code = "it-IT", NativeName = "Italiano (Italia)", EnglishName = "Italian (Italy)"},
			new NormLangDetail{Code = "fr-FR", NativeName = "Français (France)", EnglishName = "French (France)"},
			new NormLangDetail{Code = "de-DE", NativeName = "Deutsch (Deutschland)", EnglishName = "German (Germany)"},
		]);
		return R;
	}

	/// 单次消费输入集合并保存稳定快照，同时恢复完整可见列表。
	public partial void LoadRows(IEnumerable<NormLangDetail> Rows){
		var Snapshot = Rows.ToArray();
		SourceRows = Snapshot;
		this.Rows = new ObservableCollection<NormLangDetail>(Snapshot);
	}

	/// 清空查询并恢复 Provider 原始排序。
	public partial void ClearSearch(){
		Input = "";
		Rows = new ObservableCollection<NormLangDetail>(SourceRows);
	}

	/// 对代码、本地名及英文名进行大小写不敏感的包含匹配。
	public partial void Search(){
		var Query = Input.Trim();
		if(str.IsNullOrWhiteSpace(Query)){
			ClearSearch();
			return;
		}
		Rows = new ObservableCollection<NormLangDetail>(SourceRows.Where(Lang=>
			Lang.Code.Contains(Query, StringComparison.OrdinalIgnoreCase)
			|| Lang.NativeName.Contains(Query, StringComparison.OrdinalIgnoreCase)
			|| Lang.EnglishName.Contains(Query, StringComparison.OrdinalIgnoreCase)
		));
	}
}
