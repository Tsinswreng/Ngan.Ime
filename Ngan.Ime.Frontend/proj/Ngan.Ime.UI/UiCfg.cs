using Ngan.Ime.Core.Infra;
using Ngan.Ime.Core.Infra.Log;
using Avalonia;
using Avalonia.Media;
using Avalonia.Media.Fonts;
using System.Text;
using Tsinswreng.CsCfg;
using Tsinswreng.CsCore;
namespace Ngan.Ime.UI;

public partial class UiCfg{
	public UiCfg(){
		BaseFontSize = KeysCfg.Keyboard.Font.BaseFontSize.GetFrom(AppCfg.Inst);

	}
	public static UiCfg Inst => field??= new UiCfg();

	[Doc(@$"#See[{nameof(KeysCfg.Keyboard.Font.BaseFontSize)}]")]
	public f64 BaseFontSize { get; set; } = 30.0;
	public f64 KeyFontSize => BaseFontSize * 0.9;
	public f64 CandidateFontSize => BaseFontSize * 0.9;
	public f64 CandidateCommentFontSize => BaseFontSize * 0.38;
	public f64 CandidateCommentHeight => BaseFontSize * 0.66;
	public f64 CandidateTextHeight => TopBarHeight - CandidateCommentHeight;
	public f64 TopBarHeightNoComment => CandidateTextHeight;
	public f64 PreeditHeight => BaseFontSize * 1.12;
	public f64 PreeditFontSize => BaseFontSize * 0.6;
	public f64 ActionKeyFontSize => BaseFontSize * 0.72;
	public f64 HintFontSize => BaseFontSize * 0.46;
	public f64 TopBarHeight => BaseFontSize * 1.66;
	public f64 TopBarFontSize => BaseFontSize * 0.72;
	public FontFamily? KeyboardFontFamily => GetKeyboardFontFamily();
	public IBrush MainColor { get; set; } = SolidColorBrush.Parse("#4DB6AC");
	public IBrush KeyBgColor { get; set; } = SolidColorBrush.Parse("#000000");
	public IBrush CandidateBgColor { get; set; } = SolidColorBrush.Parse("#1E2A32");
	public IBrush GapLineBrush { get; set; } = SolidColorBrush.Parse("#253238");
	public IBrush CandidateGapBrush { get; set; } = SolidColorBrush.Parse("#ffffff");
	static readonly Uri KeyboardFontCollectionKey = new("fonts:keyboard");

	string? _cachedFontPath;
	string? _cachedFontFamilyName;
	FontFamily? _cachedKeyboardFontFamily;

	FontFamily? GetKeyboardFontFamily(){
		var fontPath = KeysCfg.Keyboard.Font.Path.GetFrom(AppCfg.Inst);
		if(string.IsNullOrWhiteSpace(fontPath) || !File.Exists(fontPath)){
			_cachedFontPath = fontPath;
			_cachedFontFamilyName = null;
			_cachedKeyboardFontFamily = null;
			return null;
		}
		try{
			var configuredFamily = KeysCfg.Keyboard.Font.Family.GetFrom(AppCfg.Inst);
			var familyName = string.IsNullOrWhiteSpace(configuredFamily)
				? TryReadFontFamilyName(fontPath)
				: configuredFamily;
			if(
				string.Equals(_cachedFontPath, fontPath, StringComparison.Ordinal)
				&& string.Equals(_cachedFontFamilyName, familyName, StringComparison.Ordinal)
			){
				return _cachedKeyboardFontFamily;
			}
			if(string.IsNullOrWhiteSpace(familyName)){
				_cachedFontPath = fontPath;
				_cachedFontFamilyName = null;
				_cachedKeyboardFontFamily = null;
				return null;
			}
			RegisterKeyboardFontCollection(fontPath);
			var fontFamily = FontFamily.Parse($"{KeyboardFontCollectionKey}#{familyName}");
			var typeface = new Typeface(fontFamily);
			if(!FontManager.Current.TryGetGlyphTypeface(typeface, out _)){
				_cachedFontPath = fontPath;
				_cachedFontFamilyName = familyName;
				_cachedKeyboardFontFamily = null;
				return null;
			}
			_cachedFontPath = fontPath;
			_cachedFontFamilyName = familyName;
			_cachedKeyboardFontFamily = fontFamily;
			return fontFamily;
		}catch(Exception ex){
			AppLog.Error(ex, "[KeyboardFont] GetKeyboardFontFamily failed");
		}
		_cachedFontPath = fontPath;
		_cachedFontFamilyName = null;
		_cachedKeyboardFontFamily = null;
		return null;
	}

	static void RegisterKeyboardFontCollection(string fontPath){
		var source = new Uri(fontPath, UriKind.Absolute);
		FontManager.Current.AddFontCollection(new EmbeddedFontCollection(KeyboardFontCollectionKey, source));
	}

	static string? TryReadFontFamilyName(string fontPath){
		try{
			using var fs = File.OpenRead(fontPath);
			using var br = new BinaryReader(fs, Encoding.BigEndianUnicode, leaveOpen: false);
			var scalerType = ReadU32BE(br);
			if(scalerType == 0x74746366){
				return null;
			}
			var numTables = ReadU16BE(br);
			br.BaseStream.Seek(6, SeekOrigin.Current);
			u32 nameOffset = 0;
			u32 nameLength = 0;
			for(var i = 0; i < numTables; i++){
				var tag = ReadU32BE(br);
				_ = ReadU32BE(br);
				var offset = ReadU32BE(br);
				var length = ReadU32BE(br);
				if(tag == 0x6E616D65){
					nameOffset = offset;
					nameLength = length;
				}
			}
			if(nameOffset == 0 || nameLength == 0){
				return null;
			}
			br.BaseStream.Seek(nameOffset, SeekOrigin.Begin);
			var format = ReadU16BE(br);
			var count = ReadU16BE(br);
			var stringOffset = ReadU16BE(br);
			if(format > 1){
				return null;
			}
			var storageStart = nameOffset + stringOffset;
			string? family = null;
			string? anyUnicode = null;
			string? anyEnglish = null;
			for(var i = 0; i < count; i++){
				var platformId = ReadU16BE(br);
				var encodingId = ReadU16BE(br);
				var languageId = ReadU16BE(br);
				var nameId = ReadU16BE(br);
				var length = ReadU16BE(br);
				var offset = ReadU16BE(br);
				if(nameId is not 1 and not 16){
					continue;
				}
				var pos = br.BaseStream.Position;
				br.BaseStream.Seek(storageStart + offset, SeekOrigin.Begin);
				var bytes = br.ReadBytes(length);
				br.BaseStream.Seek(pos, SeekOrigin.Begin);
				var text = DecodeNameRecord(platformId, encodingId, bytes);
				if(string.IsNullOrWhiteSpace(text)){
					continue;
				}
				text = text.Trim();
				if(nameId == 1){
					family ??= text;
					if(platformId == 3){
						anyUnicode ??= text;
						if(languageId is 0x0409 or 0){
							anyEnglish ??= text;
						}
					}
				}
			}
			return anyEnglish ?? anyUnicode ?? family;
		}catch(Exception ex){
			AppLog.Error(ex, "[KeyboardFont] TryReadFontFamilyName failed");
			return null;
		}
	}

	static string? DecodeNameRecord(u16 platformId, u16 encodingId, byte[] bytes){
		if(bytes.Length == 0){
			return null;
		}
		try{
			if(platformId == 0 || platformId == 3){
				return Encoding.BigEndianUnicode.GetString(bytes);
			}
			if(platformId == 1){
				return Encoding.UTF8.GetString(bytes);
			}
			if(platformId == 2 && encodingId == 1){
				return Encoding.BigEndianUnicode.GetString(bytes);
			}
			return Encoding.UTF8.GetString(bytes);
		}catch{
			return null;
		}
	}

	static u16 ReadU16BE(BinaryReader br){
		var bytes = br.ReadBytes(2);
		if(bytes.Length < 2){
			throw new EndOfStreamException();
		}
		if(BitConverter.IsLittleEndian){
			Array.Reverse(bytes);
		}
		return BitConverter.ToUInt16(bytes, 0);
	}

	static u32 ReadU32BE(BinaryReader br){
		var bytes = br.ReadBytes(4);
		if(bytes.Length < 4){
			throw new EndOfStreamException();
		}
		if(BitConverter.IsLittleEndian){
			Array.Reverse(bytes);
		}
		return BitConverter.ToUInt32(bytes, 0);
	}
}
