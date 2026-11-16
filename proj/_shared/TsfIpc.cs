namespace Ngan.Ime.Tsf.Ipc;

using System;
using System.Collections.Generic;
using System.Text;

[Doc($$"""
TSF 外殼與服務端之間的管道契約：管道名、請求與回應的型別，以及它們的序列化。

*為何是這一支檔案*：協議先前只靠兩邊註釋同步，管道名在三處各寫一次，
欄位順序誰也不知道。本檔以 `Compile Include` 連結進外殼（`Ngan.Ime.Tsf.Windows`）、
服務端（`Ngan.Ime.Windows`）與測試專案，三處共用同一份型別與同一個解析器。

*為何不用 JSON*：外殼不帶 CLR 基礎庫以外的東西，也不該為了一次按鍵付出反射與配置成本。
單行、以 `|` 分隔、字串走 Base64 的自訂格式已經足夠，且解析結果可預期。

*版本*：{{nameof(Version)}} 寫在每一條請求與回應裡。收端版本不符就回
{{nameof(ETsfErr.UnsupportedVersion)}}，不必成對升級也知道是誰對不上。

*向後相容*：外殼省略請求末端的欄位時，服務端用預設值補；
回應則永遠帶齊所有欄位。這讓舊版外殼還能送鍵，只是拿不到新欄位。

#Eg[
	```cs
	var Req = new TsfKeyRequest{ VirtualKey = 67, IsUp = false };
	str Line = Req.Format();          // K|1|67|0|0|0|0
	TsfKeyRequest? Back = TsfKeyRequest.Parse(Line);
	```
]
""")]
public static class TsfIpc{

	[Doc($$"""
	協議版本；請求與回應都帶這一欄。

	2：按鍵請求多了掃描碼與擴展鍵位兩個欄位。理由是同一個 `VK_SHIFT`（0x10）分不出左右，
	而左右 Shift 各自綁不同行為（例如左 Shift 切英文、右 Shift 切中文）是很常見的用法。
	舊版請求沒有這兩個欄位，解析時補 0。

	*版本必須相等*：服務端對 `Ver != Version` 一律回 `ETsfErr.UnsupportedVersion`（`eaten = false`），
	並記一次 Warn（含雙方版本）。這不是保守，而是版本化安裝的前提：
	每個安裝目錄有自己的 `identity.txt`、管道名與服務端，外殼與服務端永遠是同一次發布的產物，
	版本天生一致；不一致只代表有人把不同版本的檔案混進同一個目錄。
	解析端仍然容忍欄位短缺（見 `Parse`），那是為了讓同一版內的舊格式字串還能讀，不是跨版本相容。

	""")]
	public const i32 Version = 3;

	[Doc($$"""
	管道名稱的前綴。

	*為何不是整條名稱*：每次安裝都是一個獨立輸入法，各自有服務端；
	若共用同一個管道名，兩個版本的服務端會同時在同一條管道上監聽
	（`NamedPipeServerStream` 允許多個同名實例），客戶端連到哪一個由系統決定，
	舊版外殼就可能吃到新版的引擎狀態。實際名稱一律由 {{nameof(PipeNameFor)}} 依安裝目錄算出。
	""")]
	public const str PipeNamePrefix = "NganIme.Tsf.v1";

	[Doc($$"""
	依安裝目錄算出這一版專屬的管道名稱。

	*為何要按版本分*：每次安裝都是一個獨立輸入法，各自有服務端。若共用一條固定名稱的管道，
	兩個版本的服務端會同時在同一條名稱上監聽（具名管道允許多個同名 server 實例），
	客戶端連到哪一個由系統決定——舊版外殼就可能吃到新版的引擎狀態。

	*名稱從哪來*：看目錄裏的 `identity.txt`（安裝時由 `install.ps1` 生成，內含本版的 CLSID）。
	用 CLSID 當鍵的好處是「同一版的兩個組件即使分在不同目錄（開發時的 VM 佈局）也會算出同一個名稱」。

	*沒有 identity.txt 時退回舊的固定名稱*：那是開發／手動註冊的情境（外殼與服務端分別在
	`bin/Debug/net10.0` 與 `bin/Debug/net10.0/win-x86`，沒有共同的識別）。
	退回固定名稱讓那種流程照舊可用；代價是同機的兩份「未安裝」建置會共用一條管道，
	而這正是改動之前的行為，不是新的風險。

	*為何要雜湊*：管道名最長 256 字元，而目錄可能很深；雜湊之後長度固定，
	也避免把使用者目錄結構寫進一個全域可見的名稱。

	#Prm[{{nameof(BaseDir)}}][外殼或服務端所在目錄；大小寫與結尾斜線不敏感。]
	#Rtn[形如 `NganIme.Tsf.v1.1a2b3c4d` 的管道名稱。]
	""")]
	public static str PipeNameFor(str BaseDir){
		return PipeNameFrom(ReadIdentityKey(BaseDir));
	}

	[Doc($$"""
	由版本識別（可空）算出管道名稱；純函數，方便測試。
	#Prm[{{nameof(IdentityKey)}}][本版識別（通常是 `clsid:<GUID>`）；null 表示沒有 identity.txt。]
	#Rtn[管道名稱。]
	""")]
	public static str PipeNameFrom(str? IdentityKey){
		var Key = IdentityKey;
		if(string.IsNullOrEmpty(Key)){
			// 開發／手動註冊：沒有版本識別，沿用舊的固定名稱（兩邊都走這條，故仍能連上）。
			// 注意：這裡刻意**不**用目錄名當鍵——外殼與服務端在開發佈局裡分處兩個目錄
			// （`bin/Debug/net10.0` 與 `bin/Debug/net10.0/win-x86`），用目錄雜湊會各算一個名字而永遠連不上。
			return PipeNamePrefix;
		}
		// FNV-1a 32 位元：不需要加密強度，只需要「同一鍵穩定、不同鍵幾乎不撞」。
		// 自己寫是因為外殼不帶 CLR 以外的庫，而 System.Security.Cryptography 會拉進額外的原生依賴。
		var Hash = 2166136261u;
		foreach(var One in Key){
			Hash ^= (uint)One;
			Hash *= 16777619u;
		}
		return PipeNamePrefix + "." + Hash.ToString("x8");
	}

	[Doc("版本識別檔名；與 exe 同層，由安裝腳本寫入。")]
	public const str IdentityFileName = "identity.txt";

	[Doc($$"""
	讀 `identity.txt` 的版本識別（取 `clsid=` 那一行）。
	#Prm[{{nameof(BaseDir)}}][組件所在目錄。]
	#Rtn[形如 `clsid:<GUID>` 的鍵；檔案不存在或格式不對時回 null。]
	""")]
	public static str? ReadIdentityKey(str BaseDir){
		try{
			var Path = System.IO.Path.Combine(BaseDir ?? "", IdentityFileName);
			if(!System.IO.File.Exists(Path)){
				return null;
			}
			foreach(var Line in System.IO.File.ReadAllLines(Path)){
				var Index = Line.IndexOf('=');
				if(Index <= 0){
					continue;
				}
				if(!Line[..Index].Trim().Equals("clsid", StringComparison.OrdinalIgnoreCase)){
					continue;
				}
				var Value = Line[(Index + 1)..].Trim();
				if(Value.Length > 0){
					return "clsid:" + Value.ToUpperInvariant();
				}
			}
		}catch{
			// 讀不到（權限、檔案半寫入）就當作「沒有版本識別」，退回**固定前綴**的名稱
			// （不是目錄雜湊），與檔案不存在時的行為一致。
		}
		return null;
	}

	[Doc("單行長度上限（字元）；超過即視為畸形請求，避免一條超長行吃掉記憶體。")]
	public const i32 MaxLineLength = 4096;

	[Doc("請求首欄：按鍵。")]
	public const str KeyTag = "K";

	[Doc("請求首欄：收起候選窗。")]
	public const str HideTag = "H";

	[Doc($$"""
	請求首欄：連線探測。
	照 Weasel 的 `WEASEL_IPC_ECHO`（`WeaselIPC/WeaselClientImpl.cpp` 的 `ClientImpl::Echo`）：
	外殼每一顆鍵都先用它問一次「服務端還在嗎」，失敗就重連，連六次失敗就把服務端拉起來。
	比「等按鍵請求失敗了才推測服務端不在」可靠：探測本身不碰引擎，成本是一次空往返。
	""")]
	public const str EchoTag = "E";

	[Doc($$"""
	請求首欄：插入點位置更新（只移動候選窗，不送按鍵）。

	*為何需要*：Weasel 的候選窗與 TSF 在同一個行程裡，`_SetCompositionPosition`
	量到插入點就立刻 `_cand->UpdateInputPosition(rc)`（`WeaselTSF/Composition.cpp:228-243`），
	所以視窗緊貼光標。我們的浮窗在服務端行程，位置只能靠管道送過去；
	若只在按鍵請求裡附座標，視窗就會慢一顆鍵、而且用的是上一輪量到的值——那就是「漂」。
	於是以量測會話為單位推一條位置更新，讓浮窗與 Weasel 一樣即時跟隨。
	""")]
	public const str PosTag = "P";

	[Doc("回應首欄。")]
	public const str ReplyTag = "R";

	[Doc("修飾鍵位：Shift。")]
	public const i32 ModShift = 1;

	[Doc("修飾鍵位：Ctrl。")]
	public const i32 ModCtrl = 2;

	[Doc("修飾鍵位：Alt。")]
	public const i32 ModAlt = 4;

	[Doc("把字串轉成 Base64；空字串轉成空字串，避免 `||` 被誤判成缺欄。")]
	internal static str B64(str Text){
		if(string.IsNullOrEmpty(Text)){
			return "";
		}
		return Convert.ToBase64String(Encoding.UTF8.GetBytes(Text));
	}

	[Doc($$"""
	把 Base64 還原成字串；空字串回空串。
	#Prm[{{nameof(Text)}}][Base64 文本。]
	#Rtn[還原後的字串；格式錯誤時回空串，不拋。]
	""")]
	internal static str UnB64(str Text){
		if(string.IsNullOrEmpty(Text)){
			return "";
		}
		try{
			return Encoding.UTF8.GetString(Convert.FromBase64String(Text));
		}catch(FormatException){
			return "";
		}
	}

	[Doc("把一欄文字解析成整數；失敗回 null。")]
	internal static i32? ParseInt(str Text){
		if(i32.TryParse(Text, out var Value)){
			return Value;
		}
		return null;
	}

	[Doc("把一行切成欄位；尾端的空欄（`Format` 固定以 `|` 結尾）由各解析器自行忽略，這裡不做修剪。")]
	internal static str[] Split(str Line){
		return Line.Split('|');
	}
}

[Doc($$"""
請求種類。

*為何要分類*：先前 `HIDE` 是一條裸字串，與 `K|...` 混在同一條判斷裡。
新增請求（切換方案、查狀態）時這種寫法必然愈來愈亂，故先立型別。
""")]
public enum ETsfReqKind{
	[Doc("按鍵請求。")]
	Key = 0,

	[Doc("收起候選窗。")]
	Hide = 1,

	[Doc("連線探測（Weasel 的 ECHO）。")]
	Echo = 2,

	[Doc("插入點位置更新：只移動候選窗，不送按鍵。")]
	Position = 3,
}

[Doc($$"""
回應的錯誤碼。

*為何要錯誤碼*：先前所有失敗都回同一條空回應，外殼分不出「服務端不在」
「鍵碼不認識」「引擎壞了」，排查時只能猜。錯誤碼只進日誌，不影響是否吞鍵。
""")]
public enum ETsfErr{
	[Doc("沒有錯誤。")]
	None = 0,

	[Doc("請求格式錯誤（欄位缺失、數字解析失敗、方向欄非法）。")]
	BadRequest = 1,

	[Doc("協議版本不符。")]
	UnsupportedVersion = 2,

	[Doc("虛擬鍵碼沒有對應的按鍵。")]
	UnknownKey = 3,

	[Doc("引擎未就緒或處理時拋例外。")]
	EngineFailed = 4,

	[Doc("請求行超過長度上限。")]
	LineTooLong = 5,
}

[Doc($$"""
一條按鍵請求。

*欄位*：版本、虛擬鍵碼、修飾鍵位、插入點座標、方向。
座標允許 0，代表外殼拿不到插入點，候選窗會落在左上角。

#Eg[
	```cs
	var Req = new TsfKeyRequest{ VirtualKey = 65, Mods = 0, X = 100, Y = 200 };
	```
]
""")]
public sealed class TsfKeyRequest{

	[Doc("協議版本。")]
	public i32 Ver{get;set;} = TsfIpc.Version;

	[Doc("按鍵種類。")]
	public ETsfReqKind Kind{get;set;} = ETsfReqKind.Key;

	[Doc("Win32 虛擬鍵碼。")]
	public i32 VirtualKey{get;set;}

	[Doc("修飾鍵位；見 `TsfIpc.ModShift` 等常量。")]
	public i32 Mods{get;set;}

	[Doc("插入點螢幕 X（像素）。")]
	public i32 X{get;set;}

	[Doc("插入點螢幕 Y（像素）；位置更新時是插入點矩形的下緣。")]
	public i32 Y{get;set;}

	[Doc("插入點矩形的上緣（像素）；只有位置更新（`P`）會用到，0 表示未知。")]
	public i32 Top{get;set;}

	[Doc("true 表示擡起，false 表示按下。")]
	public bool IsUp{get;set;}

	[Doc("掃描碼（lParam 第 16 到 23 位）。用來分辨左右 Shift／Ctrl／Alt；0 表示未知。")]
	public i32 Scan{get;set;}

	[Doc("擴展鍵位（lParam 第 24 位）。右 Ctrl／右 Alt 靠它與左邊區分。")]
	public i32 Ext{get;set;}

	[Doc($$"""
	序列化成一行。
	#Rtn[請求行；按鍵為 `K|<ver>|<vk>|<mods>|<x>|<y>|<0或1>|<scan>|<ext>`，收起為 `H|<ver>`。]
	""")]
	public str Format(){
		if(Kind == ETsfReqKind.Hide){
			return TsfIpc.HideTag + "|" + Ver;
		}
		if(Kind == ETsfReqKind.Echo){
			return TsfIpc.EchoTag + "|" + Ver;
		}
		if(Kind == ETsfReqKind.Position){
			// 完整插入點矩形：左、下緣、上緣。Weasel 的浮窗靠上緣翻到文字行上方
			// （`WeaselPanel.cpp:1196` 的 `y = inputPos.top - height - 6`），
			// 只有下緣的話翻上去會蓋住整個文字行。
			return TsfIpc.PosTag + "|" + Ver + "|" + X + "|" + Y + "|" + Top;
		}
		return TsfIpc.KeyTag + "|" + Ver + "|" + VirtualKey + "|" + Mods + "|" + X + "|" + Y + "|" + (IsUp ? "1" : "0")
			+ "|" + Scan + "|" + Ext;
	}

	[Doc($$"""
	解析一行請求。
	#Prm[{{nameof(Line)}}][請求行。]
	#Rtn[請求物件；格式錯誤或版本不符時為 null（呼叫端應回錯誤碼，而不是拋）。]
	""")]
	public static TsfKeyRequest? Parse(str Line){
		if(string.IsNullOrEmpty(Line) || Line.Length > TsfIpc.MaxLineLength){
			return null;
		}
		var Parts = TsfIpc.Split(Line);
		if(Parts.Length < 2){
			return null;
		}
		var Ver = TsfIpc.ParseInt(Parts[1]);
		if(Ver is null){
			return null;
		}
		if(Parts[0] == TsfIpc.HideTag){
			return new TsfKeyRequest{ Ver = Ver.Value, Kind = ETsfReqKind.Hide };
		}
		if(Parts[0] == TsfIpc.EchoTag){
			return new TsfKeyRequest{ Ver = Ver.Value, Kind = ETsfReqKind.Echo };
		}
		if(Parts[0] == TsfIpc.PosTag){
			return new TsfKeyRequest{
				Ver = Ver.Value,
				Kind = ETsfReqKind.Position,
				X = Parts.Length > 2 ? (TsfIpc.ParseInt(Parts[2]) ?? 0) : 0,
				Y = Parts.Length > 3 ? (TsfIpc.ParseInt(Parts[3]) ?? 0) : 0,
				// 上緣是版本 3 才有的欄位；舊外殼沒有它，補 0（由接收端用下緣推算）。
				Top = Parts.Length > 4 ? (TsfIpc.ParseInt(Parts[4]) ?? 0) : 0,
			};
		}
		if(Parts[0] != TsfIpc.KeyTag || Parts.Length < 6){
			return null;
		}
		var Vk = TsfIpc.ParseInt(Parts[2]);
		var Mods = TsfIpc.ParseInt(Parts[3]);
		if(Vk is null || Mods is null){
			return null;
		}
		var X = TsfIpc.ParseInt(Parts[4]) ?? 0;
		var Y = TsfIpc.ParseInt(Parts[5]) ?? 0;
		// 方向欄可省略（舊版外殼只有五欄），但一旦出現就必須是 0 或 1。
		var IsUp = false;
		if(Parts.Length > 6){
			var State = TsfIpc.ParseInt(Parts[6]);
			if(State is not 0 and not 1){
				return null;
			}
			IsUp = State == 1;
		}
		// 掃描碼與擴展鍵位是版本 2 才有的欄位；舊版外殼沒有它們，補 0 即可。
		var Scan = Parts.Length > 7 ? (TsfIpc.ParseInt(Parts[7]) ?? 0) : 0;
		var Ext = Parts.Length > 8 ? (TsfIpc.ParseInt(Parts[8]) ?? 0) : 0;
		return new TsfKeyRequest{
			Ver = Ver.Value,
			Kind = ETsfReqKind.Key,
			VirtualKey = Vk.Value,
			Mods = Mods.Value,
			X = X,
			Y = Y,
			IsUp = IsUp,
			Scan = Scan,
			Ext = Ext,
		};
	}
}

[Doc("一個候選詞的文字與註釋；協議層的型別，與 Core 的 `ICandidate` 解耦。")]
public sealed class TsfCandidate{

	[Doc("候選詞本體。")]
	public str Text{get;set;} = "";

	[Doc("候選詞註釋；可為空串。")]
	public str Comment{get;set;} = "";
}

[Doc($$"""
一條回應。

*欄位*：版本、是否吞掉、錯誤碼、高亮序號、組字串、候選清單、上屏清單。
字串全走 Base64，避免 `|` 與 `~` 出現在負載裡。

#Eg[
	```cs
	var Reply = new TsfReply{ Eaten = true, Preedit = "ni" };
	str Line = Reply.Format();
	```
]
""")]
public sealed class TsfReply{

	[Doc("協議版本。")]
	public i32 Ver{get;set;} = TsfIpc.Version;

	[Doc("引擎是否吃掉了這顆鍵。")]
	public bool Eaten{get;set;}

	[Doc("錯誤碼；不影響 {{nameof(Eaten)}}，只供日誌與排查。")]
	public ETsfErr Err{get;set;}

	[Doc("高亮候選的序號；沒有候選時為 0。")]
	public i32 Highlight{get;set;}

	[Doc("組字串；沒有組字時為空串。")]
	public str Preedit{get;set;} = "";

	[Doc("本頁候選；永不為 null。")]
	public List<TsfCandidate> Candidates{get;set;} = [];

	[Doc("本次上屏的文字；沒有上屏時為空清單。")]
	public List<str> Commits{get;set;} = [];

	[Doc("建立一條「沒有吃掉、沒有錯誤」的回應，供失敗路徑使用。")]
	public static TsfReply Empty(){
		return new TsfReply();
	}

	[Doc("建立一條帶錯誤碼的空回應。")]
	public static TsfReply Fail(ETsfErr Err){
		return new TsfReply{ Err = Err };
	}

	[Doc($$"""
	序列化成一行。末尾固定留一個 `|`，解析端因此不必特別處理最後一欄。

	#Rtn[回應行。]
	""")]
	public str Format(){
		var Sb = new StringBuilder();
		Sb.Append(TsfIpc.ReplyTag).Append('|')
			.Append(Ver).Append('|')
			.Append(Eaten ? 1 : 0).Append('|')
			.Append((i32)Err).Append('|')
			.Append(Highlight).Append('|')
			.Append(TsfIpc.B64(Preedit)).Append('|')
			.Append(Candidates.Count).Append('|');
		foreach(var One in Candidates){
			Sb.Append(TsfIpc.B64(One.Text)).Append('~').Append(TsfIpc.B64(One.Comment)).Append('|');
		}
		Sb.Append(Commits.Count).Append('|');
		foreach(var One in Commits){
			Sb.Append(TsfIpc.B64(One)).Append('|');
		}
		return Sb.ToString();
	}

	[Doc($$"""
	解析一行回應。
	#Prm[{{nameof(Line)}}][回應行。]
	#Rtn[回應物件；格式錯誤時為 null（呼叫端據此放行按鍵）。]
	""")]
	public static TsfReply? Parse(str Line){
		if(string.IsNullOrEmpty(Line) || Line.Length > TsfIpc.MaxLineLength){
			return null;
		}
		var Parts = TsfIpc.Split(Line);
		if(Parts.Length < 7 || Parts[0] != TsfIpc.ReplyTag){
			return null;
		}
		var Ver = TsfIpc.ParseInt(Parts[1]);
		var Eaten = TsfIpc.ParseInt(Parts[2]);
		var Err = TsfIpc.ParseInt(Parts[3]);
		var Highlight = TsfIpc.ParseInt(Parts[4]);
		var CandCount = TsfIpc.ParseInt(Parts[6]);
		if(Ver is null || Eaten is null || Err is null || Highlight is null || CandCount is null || CandCount < 0){
			return null;
		}
		var Reply = new TsfReply{
			Ver = Ver.Value,
			Eaten = Eaten != 0,
			Err = (ETsfErr)Err.Value,
			Highlight = Highlight.Value,
			Preedit = TsfIpc.UnB64(Parts[5]),
		};
		var Index = 7;
		for(var I = 0; I < CandCount.Value; I++){
			if(Index >= Parts.Length){
				return null;
			}
			var Pair = Parts[Index++].Split('~');
			Reply.Candidates.Add(new TsfCandidate{
				Text = TsfIpc.UnB64(Pair[0]),
				Comment = Pair.Length > 1 ? TsfIpc.UnB64(Pair[1]) : "",
			});
		}
		if(Index >= Parts.Length){
			return null;
		}
		var CommitCount = TsfIpc.ParseInt(Parts[Index++]);
		if(CommitCount is null || CommitCount < 0){
			return null;
		}
		for(var I = 0; I < CommitCount.Value; I++){
			if(Index >= Parts.Length){
				return null;
			}
			Reply.Commits.Add(TsfIpc.UnB64(Parts[Index++]));
		}
		return Reply;
	}
}
