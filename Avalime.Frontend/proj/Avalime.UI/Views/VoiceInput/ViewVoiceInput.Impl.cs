using Avalime.Core.Infra;
using Avalime.Core.Language;
using Avalonia.Controls;
using Avalonia.Media;
using Tsinswreng.Avln.Grid;

namespace Avalime.UI.Views.VoiceInput;

/// 語音輸入頁的第一版純前端實現。
public partial class ViewVoiceInput{
	/// 建立語音頁並初始化語言選擇覆蓋層。
	public partial ViewVoiceInput(){
		Ctx = Di.DiOrMk<VmVoiceInput>();
		Render();
	}

	/// 組裝語言入口、錄音區及語言選擇覆蓋層。
	partial void Render(){
		this.SetContent(new Grid(), O=>{
			O.Background = Brushes.Black;
			O.A(Root.Grid, _=>{
				Root.SetRowDefs([
					new(1, GUT.Auto),
					new(1, GUT.Star),
				]);
				Root.A(MkLangBar())
				.A(MkRecordArea());
			})
			.A(new ViewVoiceInputLangSelector(), V=>{
				LangSelector = V;
				V.IsVisible = false;
				V.IsHitTestVisible = false;
				V.OnSelected += OnLangSelected;
			});
		});
	}

	/// 建立靠上置中的當前語言按鈕。
	private partial Control MkLangBar(){
		var R = new Border{
			Background = Brushes.Black,
			BorderBrush = UiCfg.Inst.GapLineBrush,
			BorderThickness = new(0, 0, 0, 0.5),
			Padding = new(12, 8),
		};
		R.SetChild(new Button(), B=>{
			LangButton = B;
			B.Background = UiCfg.Inst.CandidateBgColor;
			B.Foreground = Brushes.White;
			B.BorderBrush = UiCfg.Inst.GapLineBrush;
			B.BorderThickness = new(1);
			B.CornerRadius = new(UiCfg.Inst.BaseFontSize);
			B.Padding = new(18, 7);
			B.HorizontalAlignment = HAlign.Center;
			B.Click += (_, _)=>OpenLangSelector();
			B.SetContent(new TextBlock(), T=>{
				LangText = T;
				T.FontSize = UiCfg.Inst.BaseFontSize * 0.55;
				T.Foreground = Brushes.White;
				Ctx.Bind(T, X=>X.Text, X=>X.CurrentLangDisplay);
			});
		});
		return R;
	}

	/// 建立中央圓形錄音按鈕與下方提示文字。
	private partial Control MkRecordArea(){
		var R = new GridStack(IsRow: true);
		R.SetRowDefs([
			new(1, GUT.Star),
			new(1, GUT.Auto),
			new(1, GUT.Auto),
			new(1, GUT.Star),
		]);
		R.A(new Border())
		.A(new Button(), B=>{
			RecordButton = B;
			var Diameter = UiCfg.Inst.BaseFontSize * 3.4;
			B.Width = Diameter;
			B.Height = Diameter;
			B.Background = Brushes.White;
			B.BorderThickness = new(0);
			B.CornerRadius = new(Diameter / 2);
			B.HorizontalAlignment = HAlign.Center;
			B.VerticalAlignment = VAlign.Center;
			B.SetContent(Avalime.UI.Icons.Icons.Microphone(), I=>{
				RecordIcon = I;
				I.Width = UiCfg.Inst.BaseFontSize * 1.25;
				I.Height = UiCfg.Inst.BaseFontSize * 1.25;
				I.Fill = Brushes.Black;
			});
		})
		.A(new TextBlock(), T=>{
			StatusText = T;
			T.Margin = new(0, UiCfg.Inst.BaseFontSize * 0.55, 0, 0);
			T.HorizontalAlignment = HAlign.Center;
			T.Foreground = Brushes.LightGray;
			T.FontSize = UiCfg.Inst.BaseFontSize * 0.52;
			Ctx.Bind(T, X=>X.Text, X=>X.StatusText);
		})
		.A(new Border());
		return R.Grid;
	}

	/// 直接切換 View 可見性，暫不建立 ViewModel 顯隱狀態。
	partial void OpenLangSelector(){
		if(LangSelector is null){
			return;
		}
		LangSelector.IsVisible = true;
		LangSelector.IsHitTestVisible = true;
	}

	/// 收到示例語言點擊後只關閉覆蓋層；語言狀態留待後續實現。
	partial void OnLangSelected(NormLangDetail Lang){
		_ = Lang;
		if(LangSelector is null){
			return;
		}
		LangSelector.IsVisible = false;
		LangSelector.IsHitTestVisible = false;
	}

	/// 錄音狀態尚未接入，因此維持第一版白色按鈕外觀。
	partial void SyncRecordingVisual(){
		if(RecordButton is not null){
			RecordButton.Background = Brushes.White;
		}
	}

	/// 解除子視圖事件，避免未來重建語音頁時保留舊引用。
	public partial void Dispose(){
		if(LangSelector is not null){
			LangSelector.OnSelected -= OnLangSelected;
		}
	}
}
