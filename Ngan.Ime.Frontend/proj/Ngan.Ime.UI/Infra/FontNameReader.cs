namespace Ngan.Ime.UI.Infra;

using System.Text;
using Tsinswreng.CsCore;

[Doc($$"""
從 TTF／OTF 檔讀出字體家族名（`name` 表）。

*為何自己解析*：Avalonia 只提供「已註冊的字體集合 → 家族名」的反查，沒有「給一個檔案問它叫什麼」；
而配置允許使用者只填字體檔路徑、家族名留空。故這裡直接讀 `name` 表。

*職責邊界*：本類只做「位元組 → 家族名」，不碰配置、不碰 `FontManager`、不碰單例；
`UiCfg` 負責快取，`IFontFamilySource` 負責註冊字體集合。這樣解析邏輯可以單獨測（給一段位元組就能驗）。

*支援*：TrueType／OpenType（`0x00010000`、`true`、`OTTO`），不支援字體集合（`ttcf`）；
`name` 表取 nameId 1 或 16，平台 3（Windows）優先、語言 0x0409（英文）優先。

#Eg[
	```cs
	str? Family = FontNameReader.TryReadFamilyName("C:/fonts/keyboard.ttf");
	```
]
""")]
public static class FontNameReader{

	[Doc($$"""
	嘗試讀出字體家族名。
	#Prm[{{nameof(FontPath)}}][字體檔路徑。]
	#Rtn[家族名；不是可解析的字體檔時為 null（不拋，因為「使用者填錯路徑」是常態）。]
	""")]
	public static str? TryReadFamilyName(str FontPath){
		try{
			using var Fs = File.OpenRead(FontPath);
			using var Br = new BinaryReader(Fs, Encoding.BigEndianUnicode, leaveOpen: false);
			return ReadFromStream(Br);
		}catch(Exception Ex){
			AppLog.Warn($"[Font] 讀取字體名失敗：{FontPath}：{Ex.Message}");
			return null;
		}
	}

	[Doc("從已開啟的資料流讀家族名；格式不認識或沒有 name 表時回 null。")]
	static str? ReadFromStream(BinaryReader Br){
		var ScalerType = ReadU32BE(Br);
		// ttcf：字體集合，本函數不處理（呼叫方應改填家族名）。
		if(ScalerType == 0x74746366){
			return null;
		}
		var NumTables = ReadU16BE(Br);
		Br.BaseStream.Seek(6, SeekOrigin.Current);
		u32 NameOffset = 0;
		u32 NameLength = 0;
		for(var I = 0; I < NumTables; I++){
			var Tag = ReadU32BE(Br);
			_ = ReadU32BE(Br);
			var Offset = ReadU32BE(Br);
			var Length = ReadU32BE(Br);
			if(Tag == 0x6E616D65){
				NameOffset = Offset;
				NameLength = Length;
			}
		}
		if(NameOffset == 0 || NameLength == 0){
			return null;
		}
		Br.BaseStream.Seek(NameOffset, SeekOrigin.Begin);
		var Format = ReadU16BE(Br);
		var Count = ReadU16BE(Br);
		var StringOffset = ReadU16BE(Br);
		if(Format > 1){
			return null;
		}
		var StorageStart = NameOffset + StringOffset;
		str? Family = null;
		str? AnyUnicode = null;
		str? AnyEnglish = null;
		for(var I = 0; I < Count; I++){
			var PlatformId = ReadU16BE(Br);
			var EncodingId = ReadU16BE(Br);
			var LanguageId = ReadU16BE(Br);
			var NameId = ReadU16BE(Br);
			var Length = ReadU16BE(Br);
			var Offset = ReadU16BE(Br);
			// 1 = 家族名，16 = 排版家族名（可變字體）。其餘跳過。
			if(NameId is not 1 and not 16){
				continue;
			}
			var Pos = Br.BaseStream.Position;
			Br.BaseStream.Seek(StorageStart + Offset, SeekOrigin.Begin);
			var Bytes = Br.ReadBytes(Length);
			Br.BaseStream.Seek(Pos, SeekOrigin.Begin);
			var Text = DecodeNameRecord(PlatformId, EncodingId, Bytes)?.Trim();
			if(string.IsNullOrWhiteSpace(Text)){
				continue;
			}
			if(NameId == 1){
				Family ??= Text;
				if(PlatformId == 3){
					AnyUnicode ??= Text;
					if(LanguageId is 0x0409 or 0){
						AnyEnglish ??= Text;
					}
				}
			}
		}
		return AnyEnglish ?? AnyUnicode ?? Family;
	}

	[Doc("依平台／編碼解碼一條 name 記錄；無法辨識時按 UTF-8 嘗試。")]
	static str? DecodeNameRecord(u16 PlatformId, u16 EncodingId, byte[] Bytes){
		if(Bytes.Length == 0){
			return null;
		}
		try{
			// 平台 0（Unicode）與 3（Windows）都用 UTF-16BE。
			if(PlatformId is 0 or 3){
				return Encoding.BigEndianUnicode.GetString(Bytes);
			}
			if(PlatformId == 1){
				return Encoding.UTF8.GetString(Bytes);
			}
			if(PlatformId == 2 && EncodingId == 1){
				return Encoding.BigEndianUnicode.GetString(Bytes);
			}
			return Encoding.UTF8.GetString(Bytes);
		}catch{
			return null;
		}
	}

	[Doc("讀 2 字節大端無符號整數；不足時拋 EndOfStreamException（由呼叫方統一轉成 null）。")]
	static u16 ReadU16BE(BinaryReader Br){
		var Bytes = Br.ReadBytes(2);
		if(Bytes.Length < 2){
			throw new EndOfStreamException();
		}
		if(BitConverter.IsLittleEndian){
			Array.Reverse(Bytes);
		}
		return BitConverter.ToUInt16(Bytes, 0);
	}

	[Doc("讀 4 字節大端無符號整數；不足時拋 EndOfStreamException。")]
	static u32 ReadU32BE(BinaryReader Br){
		var Bytes = Br.ReadBytes(4);
		if(Bytes.Length < 4){
			throw new EndOfStreamException();
		}
		if(BitConverter.IsLittleEndian){
			Array.Reverse(Bytes);
		}
		return BitConverter.ToUInt32(Bytes, 0);
	}
}
