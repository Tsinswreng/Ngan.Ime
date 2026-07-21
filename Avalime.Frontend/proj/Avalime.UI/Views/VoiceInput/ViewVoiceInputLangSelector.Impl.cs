using Avalime.Core.Infra;
using Avalime.Core.Language;
using Avalonia.Controls;
using Avalonia.Media;
using Tsinswreng.Avln.Grid;

namespace Avalime.UI.Views.VoiceInput;

/// 語音語言選擇區的第一版純前端實現。
public partial class ViewVoiceInputLangSelector{
	/// 建立帶靜態語言示例資料的選擇區。
	public partial ViewVoiceInputLangSelector(){
		Ctx = Di.DiOrMk<VmVoiceInputLangSelector>();
		Render();
	}

	/// 組裝頂部搜索列和可滾動語言列表。
	partial void Render(){
		this.SetContent(new Border(), B=>{
			B.Background = Brushes.Black;
			B.BorderBrush = UiCfg.Inst.GapLineBrush;
			B.BorderThickness = new(1);
			B.SetChild(Root.Grid, _=>{
				Root.SetRowDefs([
					new(1, GUT.Auto),
					new(1, GUT.Star),
				]);
				Root.A(MkSearchBar())
				.A(MkLangList());
			});
		});
	}

	/// 建立清空、搜索輸入與搜索按鈕。
	private partial Control MkSearchBar(){
		var R = new GridStack(IsRow: false);
		R.SetColDefs([
			new(1, GUT.Auto),
			new(1, GUT.Star),
			new(1, GUT.Auto),
		]);
		R.A(new Button(), B=>{
			ClearSearchButton = B;
			B.SetContent(new TextBlock(), T=>{
				T.Text = "×";
				T.FontSize = UiCfg.Inst.BaseFontSize * 0.75;
			});
		})
		.A(new TextBox(), T=>{
			SearchInput = T;
			T.PlaceholderText = "語言名稱或 BCP 47 代碼";
			T.FontSize = UiCfg.Inst.BaseFontSize * 0.52;
			T.Background = UiCfg.Inst.CandidateBgColor;
			T.Foreground = Brushes.White;
			T.BorderBrush = UiCfg.Inst.GapLineBrush;
		})
		.A(new Button(), B=>{
			SearchButton = B;
			B.Background = UiCfg.Inst.MainColor;
			B.SetContent(new TextBlock(), T=>{
				T.Text = "搜尋";
				T.FontSize = UiCfg.Inst.BaseFontSize * 0.5;
				T.Foreground = Brushes.Black;
			});
		});
		return R.Grid;
	}

	/// 建立語言項目列表；靜態集合只消費一次。
	private partial Control MkLangList(){
		var List = new StackPanel{
			Orientation = Avalonia.Layout.Orientation.Vertical,
			Spacing = 0,
		};
		LangList = List;
		if(Ctx is not null){
			foreach(var Lang in Ctx.Rows){
				List.A(MkLangItem(Lang));
			}
		}
		var R = new ScrollViewer();
		LangScroll = R;
		R.SetContent(List);
		return R;
	}

	/// 建立顯示代碼、本地名稱與英文名稱的語言按鈕。
	private partial Control MkLangItem(NormLangDetail Lang){
		var R = new Button{
			Background = Brushes.Black,
			BorderBrush = UiCfg.Inst.GapLineBrush,
			BorderThickness = new(0, 0, 0, 0.5),
			CornerRadius = new(0),
			Padding = new(14, 9),
			HorizontalContentAlignment = HAlign.Stretch,
		};
		var Row = new GridStack(IsRow: false);
		Row.SetColDefs([
			new(1, GUT.Auto),
			new(1, GUT.Star),
		]);
		Row.A(new TextBlock(), T=>{
			T.Text = Lang.Code;
			T.MinWidth = UiCfg.Inst.BaseFontSize * 3.8;
			T.Foreground = UiCfg.Inst.MainColor;
			T.FontSize = UiCfg.Inst.BaseFontSize * 0.52;
			T.VerticalAlignment = VAlign.Center;
		})
		.A(new StackPanel(), P=>{
			P.Spacing = 2;
			P.A(new TextBlock(), T=>{
				T.Text = Lang.NativeName;
				T.Foreground = Brushes.White;
				T.FontSize = UiCfg.Inst.BaseFontSize * 0.52;
			})
			.A(new TextBlock(), T=>{
				T.Text = Lang.EnglishName;
				T.Foreground = Brushes.Gray;
				T.FontSize = UiCfg.Inst.BaseFontSize * 0.4;
			});
		});
		R.SetContent(Row.Grid);
		R.Click += (_, _)=>SelectLang(Lang);
		return R;
	}

	/// 把點擊結果交給語音頁；目前不在此處保存選中狀態。
	partial void SelectLang(NormLangDetail Lang){
		OnSelected?.Invoke(Lang);
	}
}
