namespace Ngan.Ime.UI;

using Avalonia;
using Avalonia.Media;
using Avalonia.Media.Fonts;

[Doc($$"""
界面主題常量的唯一出處：字級、尺寸、顏色、鍵盤字體。

*為何是靜態單例*：這些值被幾十個視圖在建構期讀取，注入只會讓每個 View 多一個參數；
它們是「主題常量」而非「服務」，故靜態是合理形態。真正有 IO 的字體解析被拆到
{{nameof(UiCfg.FontFamily)}} 的快取與 {{nameof(Ngan.Ime.UI.Infra.FontNameReader)}}，本類只做快取與配置讀取。

*配置何時讀*：第一次存取時才讀（延遲），故「誰先誰後」不再影響結果；
先前在建構子讀 `KeysAppCfg.Keyboard.Font.BaseFontSize`，若 `UiCfg` 早於 `AppCfg.Setup` 被碰到，
字級會永久凍結在默認值。現在提供 {{nameof(Reload)}} 供設定頁改完後重讀。

*字級比例*：全部由 {{nameof(BaseFontSize)}} 按固定比例推出，避免各處各寫一個魔法數。

#Eg[
	```cs
	f64 KeySize = UiCfg.Inst.KeyFontSize;
	IBrush Main = UiCfg.Inst.MainColor;
	```
]
""")]
public partial class UiCfg{

	[Doc("全局單例；第一次取用時建立（此時不讀配置）。")]
	public static UiCfg Inst => field ??= new UiCfg();

	[Doc("配置讀取鎖；Reload 與延遲初始化都可能從不同線程第一次觸發。")]
	readonly Lock CfgLock = new();

	[Doc("配置是否已讀過。")]
	bool CfgLoaded;

	[Doc("基準字級（px）；由配置 `Keyboard/Font/BaseFontSize` 提供，第一次取用時才讀。")]
	public f64 BaseFontSize{
		get{
			EnsureCfgLoaded();
			return BaseFontSizeField;
		}
		private set{ BaseFontSizeField = value; }
	}

	[Doc("基準字級的實際值；只由 EnsureCfgLoaded 與 Reload 改寫。")]
	f64 BaseFontSizeField = 32.0;

	[Doc("鍵帽字級。")]
	public f64 KeyFontSize => BaseFontSize * 0.9;

	[Doc("候選詞字級。")]
	public f64 CandidateFontSize => BaseFontSize * 0.9;

	[Doc("候選註釋字級。")]
	public f64 CandidateCommentFontSize => BaseFontSize * 0.38;

	[Doc("候選註釋列高度。")]
	public f64 CandidateCommentHeight => BaseFontSize * 0.66;

	[Doc("候選文字列高度（頂欄高度減去註釋列）。")]
	public f64 CandidateTextHeight => TopBarHeight - CandidateCommentHeight;

	[Doc("不顯示註釋時的頂欄高度。")]
	public f64 TopBarHeightNoComment => CandidateTextHeight;

	[Doc("預編輯列高度。")]
	public f64 PreeditHeight => BaseFontSize * 1.12;

	[Doc("預編輯字級。")]
	public f64 PreeditFontSize => BaseFontSize * 0.6;

	[Doc("功能鍵（123／qwe 等）字級。")]
	public f64 ActionKeyFontSize => BaseFontSize * 0.72;

	[Doc("鍵帽角落提示字級。")]
	public f64 HintFontSize => BaseFontSize * 0.46;

	[Doc("顯示註釋時的頂欄高度（候選／工具欄共用）。")]
	public f64 TopBarHeight => BaseFontSize * 1.66;

	[Doc("工具欄字級。")]
	public f64 TopBarFontSize => BaseFontSize * 0.72;

	[Doc("主色：工具欄啟用態、高亮邊框、錄音態。")]
	public IBrush MainColor{get;set;} = SolidColorBrush.Parse("#4DB6AC");

	[Doc("鍵盤底色（鍵與鍵之間的縫也是這個色）。")]
	public IBrush KeyBgColor{get;set;} = SolidColorBrush.Parse("#000000");

	[Doc("候選欄底色。")]
	public IBrush CandidateBgColor{get;set;} = SolidColorBrush.Parse("#1E2A32");

	[Doc("分隔線顏色（鍵盤縫、工具欄邊框）。")]
	public IBrush GapLineBrush{get;set;} = SolidColorBrush.Parse("#253238");

	[Doc("候選之間的分隔線顏色。")]
	public IBrush CandidateGapBrush{get;set;} = SolidColorBrush.Parse("#FFFFFF");

	[Doc("浮窗候選詞字級（= BaseFontSize × 0.5，基準 32 時為 16）。")]
	public f64 PopupCandidateFontSize => BaseFontSize * 0.5;

	[Doc("浮窗候選註釋字級（= BaseFontSize × 0.375，基準 32 時為 12）。")]
	public f64 PopupCommentFontSize => BaseFontSize * 0.375;

	[Doc("浮窗候選序號字級（= BaseFontSize × 0.4375，基準 32 時為 14）。")]
	public f64 PopupIndexFontSize => BaseFontSize * 0.4375;

	[Doc("浮窗組字列字級（= BaseFontSize × 0.5）。")]
	public f64 PopupPreeditFontSize => BaseFontSize * 0.5;

	[Doc("浮窗候選行與行之間的間距；行與行緊貼，故為 0。")]
	public f64 PopupRowSpacing => 0;

	[Doc("浮窗行內序號與候選詞之間的間距（px）。")]
	public f64 PopupInnerSpacing => BaseFontSize * 0.0625;

	[Doc("浮窗內容內距；盡量貼邊，基準 32 時左右各 1px、上下各 0。")]
	public Thickness PopupPadding => new(BaseFontSize * 0.03125, 0);

	[Doc("浮窗底色；純黑。")]
	public IBrush PopupBgColor{get;set;} = SolidColorBrush.Parse("#000000");

	[Doc("浮窗文字顏色；純白。")]
	public IBrush PopupForeColor{get;set;} = SolidColorBrush.Parse("#FFFFFF");

	[Doc("浮窗高亮行底色。")]
	public IBrush PopupHighlightColor{get;set;} = SolidColorBrush.Parse("#2F6FEB");

	[Doc("鍵盤字體集合的註冊鍵；同一行程只註冊一次。")]
	static readonly Uri KeyboardFontCollectionKey = new("fonts:keyboard");

	[Doc("上次解析的字體檔路徑；用於快取判斷。")]
	str? CachedFontPath;

	[Doc("上次解析的家族名；用於快取判斷。")]
	str? CachedFontFamilyName;

	[Doc("上次解析出的字體；null 表示不可用。")]
	FontFamily? CachedFontFamily;

	[Doc($$"""
	鍵盤字體；配置沒填或解析不到時為 null（視圖據此用系統默認字體）。

	*快取*：路徑與家族名都沒變就回上次結果，**不再碰檔案系統**。
	舊版每次 get 都 `AppCfg.Get` ＋ `File.Exists`，建一次鍵盤（約 120 顆鍵）就是 120 次檔案查詢。
	""")]
	public FontFamily? FontFamily{
		get{
			EnsureCfgLoaded();
			var FontPath = AppCfg.Inst.Get(KeysAppCfg.Keyboard.Font.Path);
			if(string.IsNullOrWhiteSpace(FontPath) || !File.Exists(FontPath)){
				CachedFontPath = FontPath;
				CachedFontFamilyName = null;
				CachedFontFamily = null;
				return null;
			}
			try{
				var ConfiguredFamily = AppCfg.Inst.Get(KeysAppCfg.Keyboard.Font.Family);
				var FamilyName = string.IsNullOrWhiteSpace(ConfiguredFamily)
					? FontNameReader.TryReadFamilyName(FontPath)
					: ConfiguredFamily;
				if(
					string.Equals(CachedFontPath, FontPath, StringComparison.Ordinal)
					&& string.Equals(CachedFontFamilyName, FamilyName, StringComparison.Ordinal)
				){
					return CachedFontFamily;
				}
				CachedFontPath = FontPath;
				CachedFontFamilyName = FamilyName;
				CachedFontFamily = null;
				if(string.IsNullOrWhiteSpace(FamilyName)){
					return null;
				}
				RegisterFontCollection(FontPath);
				var Family = FontFamily.Parse($"{KeyboardFontCollectionKey}#{FamilyName}");
				if(!FontManager.Current.TryGetGlyphTypeface(new Typeface(Family), out _)){
					AppLog.Warn($"[Font] 字體集合註冊後仍取不到字形：{FamilyName}（{FontPath}）");
					return null;
				}
				CachedFontFamily = Family;
				return Family;
			}catch(Exception Ex){
				AppLog.Error(Ex, "[Font] 解析鍵盤字體失敗");
				CachedFontFamilyName = null;
				CachedFontFamily = null;
				return null;
			}
		}
	}

	[Doc("延遲讀配置：只在第一次需要字級時讀一次。")]
	void EnsureCfgLoaded(){
		lock(CfgLock){
			if(CfgLoaded){
				return;
			}
			BaseFontSizeField = AppCfg.Inst.Get(KeysAppCfg.Keyboard.Font.BaseFontSize);
			CfgLoaded = true;
		}
	}

	[Doc("設定頁改完字級／字體後呼叫：丟棄字級快取與字體快取，下次取用時重讀。")]
	public void Reload(){
		lock(CfgLock){
			CfgLoaded = false;
			CachedFontPath = null;
			CachedFontFamilyName = null;
			CachedFontFamily = null;
		}
	}

	[Doc("把字體檔註冊成 Avalonia 字體集合；同一行程以同一鍵重複註冊時直接略過。")]
	static void RegisterFontCollection(str FontPath){
		// 只註冊一次：Avalonia 對同一鍵重複 AddFontCollection 的行為沒有保證，
		// 而每次重算字體都註冊一遍是最容易踩到的冗餘。
		if(RegisteredFontPaths.Contains(FontPath)){
			return;
		}
		RegisteredFontPaths.Add(FontPath);
		FontManager.Current.AddFontCollection(new EmbeddedFontCollection(KeyboardFontCollectionKey, new Uri(FontPath, UriKind.Absolute)));
	}

	[Doc("已註冊過的字體檔集合；避免重複 AddFontCollection。")]
	static readonly HashSet<str> RegisteredFontPaths = new(StringComparer.OrdinalIgnoreCase);
}
