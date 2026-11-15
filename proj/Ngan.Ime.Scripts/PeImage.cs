using Tsinswreng.CsSh;
using static Tsinswreng.CsSh.ShGlobal;

namespace Ngan.Ime.Scripts;

/// <summary>
/// 發布產物的 PE 標頭檢查：確認位數與「是不是原生」。
/// </summary>
/// <remarks>
/// *為何要檢查*：這個專案是「x64 外殼 + x86 服務端」的組合，兩邊位數錯了就裝不起來，
/// 而且錯的方式很難看出來（外殼載入失敗只會在事件檢視器留下一行）。
/// 記憶檔記過一次事故：只發了 x86 服務端，結果在乾淨機器上既裝不了也切不過去。
///
/// *為何不用 dumpbin／corflags*：那要裝 SDK 工具或 Visual Studio；
/// 直接讀 PE 標頭十幾行程式碼，任何機器都跑得動，也不必起外部行程。
/// </remarks>
internal static class PeImage{

	/// <summary>PE 的 machine 欄位：x64（AMD64）。</summary>
	public const ushort MachineAmd64 = 0x8664;

	/// <summary>PE 的 machine 欄位：x86（I386）。</summary>
	public const ushort MachineI386 = 0x014C;

	/// <summary>MZ 標頭。</summary>
	const ushort DosMagic = 0x5A4D;

	/// <summary>PE 標頭。</summary>
	const uint PeMagic = 0x00004550;

	/// <summary>PE32 的 optional header magic。</summary>
	const ushort Pe32Magic = 0x10B;

	/// <summary>PE32+（64 位）的 optional header magic。</summary>
	const ushort Pe32PlusMagic = 0x20B;

	/// <summary>CLR runtime header 在 data directory 裏的索引。</summary>
	const int ClrDirectoryIndex = 14;

	/// <summary>PE 標頭解析結果。</summary>
	/// <param name="Machine">目標機器碼；<see cref="MachineAmd64"/> 或 <see cref="MachineI386"/>。</param>
	/// <param name="HasClrHeader">true 表示這是托管組件（有 CLR header）。</param>
	public readonly record struct PeInfo(ushort Machine, bool HasClrHeader);

	/// <summary>讀出 PE 標頭；檔案不是 PE 時拋 <see cref="InvalidDataException"/>。</summary>
	/// <param name="Path">檔案路徑。</param>
	public static PeInfo Read(str Path){
		using var Stream = File.OpenRead(Path);
		using var Reader = new BinaryReader(Stream);
		if(Reader.ReadUInt16() != DosMagic){
			throw new InvalidDataException($"不是 PE 檔（缺 MZ）：{Path}");
		}
		// DOS 標頭 0x3C 處是 PE 標頭的位移。
		Stream.Position = 0x3C;
		var PeOffset = Reader.ReadInt32();
		Stream.Position = PeOffset;
		if(Reader.ReadUInt32() != PeMagic){
			throw new InvalidDataException($"不是 PE 檔（缺 PE 簽章）：{Path}");
		}
		var Machine = Reader.ReadUInt16();
		// COFF header 餘下：NumberOfSections(2)、TimeDateStamp(4)、PointerToSymbolTable(4)、
		// NumberOfSymbols(4)、SizeOfOptionalHeader(2)、Characteristics(2)。
		Stream.Position += 2 + 4 + 4 + 4;
		var OptionalSize = Reader.ReadUInt16();
		if(OptionalSize == 0){
			// 目的檔（.obj）沒有 optional header；發布產物不該是這種東西。
			throw new InvalidDataException($"不是可執行映像（沒有 optional header）：{Path}");
		}
		Stream.Position += 2;
		var OptionalStart = Stream.Position;
		var Magic = Reader.ReadUInt16();
		// data directory 的起點隨 PE32／PE32+ 不同。
		var DataDirectoryStart = OptionalStart + (Magic == Pe32PlusMagic ? 112 : 96);
		Stream.Position = DataDirectoryStart + ClrDirectoryIndex * 8;
		var ClrRva = Reader.ReadUInt32();
		var ClrSize = Reader.ReadUInt32();
		_ = Magic == Pe32Magic || Magic == Pe32PlusMagic;
		return new PeInfo(Machine, ClrSize != 0 || ClrRva != 0);
	}

	/// <summary>把 machine 碼翻成可讀字串，供日誌使用。</summary>
	public static str Describe(ushort Machine){
		return Machine switch {
			MachineAmd64 => "x64",
			MachineI386 => "x86",
			_ => "0x" + Machine.ToString("X4"),
		};
	}
}
