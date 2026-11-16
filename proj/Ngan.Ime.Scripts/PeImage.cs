using Tsinswreng.CsSh;
using static Tsinswreng.CsSh.ShGlobal;

namespace Ngan.Ime.Scripts;

[Doc($$"""
發布產物的 PE 標頭檢查：確認位數與「是不是原生」。
*為何要檢查*：這個專案是「x64 外殼 + x86 服務端」的組合，兩邊位數錯了就裝不起來，
而且錯的方式很難看出來（外殼載入失敗只會在事件檢視器留下一行）。
記憶檔記過一次事故：只發了 x86 服務端，結果在乾淨機器上既裝不了也切不過去。

*為何不用 dumpbin／corflags*：那要裝 SDK 工具或 Visual Studio；
直接讀 PE 標頭十幾行程式碼，任何機器都跑得動，也不必起外部行程。
""")]
internal static class PeImage{

	[Doc($$"""
	PE 的 machine 欄位：x64（AMD64）。
	""")]
	public const u16 MachineAmd64 = 0x8664;

	[Doc($$"""
	PE 的 machine 欄位：x86（I386）。
	""")]
	public const u16 MachineI386 = 0x014C;

	[Doc($$"""
	MZ 標頭。
	""")]
	const u16 DosMagic = 0x5A4D;

	[Doc($$"""
	PE 標頭。
	""")]
	const u32 PeMagic = 0x00004550;

	[Doc($$"""
	PE32 的 optional header magic。
	""")]
	const u16 Pe32Magic = 0x10B;

	[Doc($$"""
	PE32+（64 位）的 optional header magic。
	""")]
	const u16 Pe32PlusMagic = 0x20B;

	[Doc($$"""
	CLR runtime header 在 data directory 裏的索引。
	""")]
	const i32 ClrDirectoryIndex = 14;

	[Doc($$"""
	PE 標頭解析結果。
	#Prm[{{nameof(Machine)}}][目標機器碼；{{nameof(MachineAmd64)}} 或 {{nameof(MachineI386)}}。]
	#Prm[{{nameof(HasClrHeader)}}][true 表示這是托管組件（有 CLR header）。]
	""")]
	public readonly record struct PeInfo(u16 Machine, bool HasClrHeader);

	[Doc($$"""
	讀出 PE 標頭；檔案不是 PE 時拋 {{nameof(InvalidDataException)}}。
	#Prm[{{nameof(Path)}}][檔案路徑。]
	""")]
	public static PeInfo Read(str Path){
		using var stream = File.OpenRead(Path);
		using var reader = new BinaryReader(stream);
		if(reader.ReadUInt16() != DosMagic){
			throw new InvalidDataException($"不是 PE 檔（缺 MZ）：{Path}");
		}
		// DOS 標頭 0x3C 處是 PE 標頭的位移。
		stream.Position = 0x3C;
		var peOffset = reader.ReadInt32();
		stream.Position = peOffset;
		if(reader.ReadUInt32() != PeMagic){
			throw new InvalidDataException($"不是 PE 檔（缺 PE 簽章）：{Path}");
		}
		var Machine = reader.ReadUInt16();
		// COFF header 餘下：NumberOfSections(2)、TimeDateStamp(4)、PointerToSymbolTable(4)、
		// NumberOfSymbols(4)、SizeOfOptionalHeader(2)、Characteristics(2)。
		stream.Position += 2 + 4 + 4 + 4;
		var optionalSize = reader.ReadUInt16();
		if(optionalSize == 0){
			// 目的檔（.obj）沒有 optional header；發布產物不該是這種東西。
			throw new InvalidDataException($"不是可執行映像（沒有 optional header）：{Path}");
		}
		stream.Position += 2;
		var optionalStart = stream.Position;
		var Magic = reader.ReadUInt16();
		// magic 必須先驗：不是 PE32／PE32+ 時，下面用 PE32 的位移去讀 data directory，
		// 讀到的是垃圾，於是「有沒有 CLR header」這個結論就沒有意義了。
		if(Magic != Pe32Magic && Magic != Pe32PlusMagic){
			throw new InvalidDataException($"不是可執行映像（optional header magic=0x{Magic:X}）：{Path}");
		}
		// data directory 的起點隨 PE32／PE32+ 不同。
		var dataDirectoryStart = optionalStart + (Magic == Pe32PlusMagic ? 112 : 96);
		stream.Position = dataDirectoryStart + ClrDirectoryIndex * 8;
		var clrRva = reader.ReadUInt32();
		var clrSize = reader.ReadUInt32();
		return new PeInfo(Machine, clrSize != 0 || clrRva != 0);
	}

	[Doc($$"""
	把 machine 碼翻成可讀字串，供日誌使用。
	""")]
	public static str Describe(u16 Machine){
		return Machine switch {
			MachineAmd64 => "x64",
			MachineI386 => "x86",
			_ => "0x" + Machine.ToString("X4"),
		};
	}
}
