namespace Ngan.Ime.Core.Ime;

using Tsinswreng.CsCore;

[Doc($$"""
Rime 的 option 名稱常量。

*為何要集中*：`ascii_mode`／`simplification` 這兩個字串同時出現在通知回調、狀態回寫與
模式提示三處；散著寫就等著有一天某一處打錯字而整個模式切換靜默失效。
名稱本身是 Rime 的對外契約（librime 以字串傳遞），故不能用 `nameof` 取代。
""")]
public static class OptionNames{

	[Doc("英文（ASCII）模式；`true` 表示直接上屏原字元。")]
	public const str AsciiMode = "ascii_mode";

	[Doc("簡體輸出；`true` 表示輸出簡體。")]
	public const str Simplification = "simplification";
}
