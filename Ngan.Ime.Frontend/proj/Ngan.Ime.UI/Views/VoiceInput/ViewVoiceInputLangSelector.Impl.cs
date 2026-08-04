using Ngan.Ime.Core.Infra;
using Ngan.Ime.Core.Language;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;

namespace Ngan.Ime.UI.Views.VoiceInput;

/// 語音识别语言覆盖层的搜索、列表重建与关闭实现。
public partial class ViewVoiceInputLangSelector{
	/// 建立覆盖层并订阅搜索结果变化。
	public partial ViewVoiceInputLangSelector(){
		Ctx = Di.DiOrMk<VmVoiceInputLangSelector>();
		Render();
		_ctxPropertyChangedHandler = (_, E)=>{
			if(E.PropertyName is nameof(VmVoiceInputLangSelector.Rows) or nameof(VmVoiceInputLangSelector.IsEmpty)){
				RebuildLangItems();
			}
		};
		if(Ctx is not null){
			Ctx.PropertyChanged += _ctxPropertyChangedHandler;
		}
		RebuildLangItems();
	}

	/// 组装顶部工具列和语言列表区。
	private partial void Render(){
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

	/// 建立关闭、输入、清空与搜索操作。
	private partial Control MkSearchBar(){
		var R = new GridStack(IsRow: false);
		R.SetColDefs([
			new(1, GUT.Auto),
			new(1, GUT.Star),
			new(1, GUT.Auto),
			new(1, GUT.Auto),
		]);
		R.A(new Button(), B=>{
			CloseButton = B;
			B.SetContent("返回");
			B.Click += (_, _)=>Close();
		})
		.A(new TextBox(), T=>{
			SearchInput = T;
			T.PlaceholderText = "語言名稱或 BCP 47 代碼";
			T.FontSize = UiCfg.Inst.BaseFontSize * 0.48;
			T.Background = UiCfg.Inst.CandidateBgColor;
			T.Foreground = Brushes.White;
			T.BorderBrush = UiCfg.Inst.GapLineBrush;
			Ctx.Bind(T, X=>X.Text, X=>X.Input, Mode: Avalonia.Data.BindingMode.TwoWay);
			T.KeyDown += (_, E)=>{
				if(E.Key == Avalonia.Input.Key.Enter){
					Ctx?.Search();
					E.Handled = true;
				}
			};
		})
		.A(new Button(), B=>{
			ClearSearchButton = B;
			B.SetContent("×");
			B.Click += (_, _)=>Ctx?.ClearSearch();
		})
		.A(new Button(), B=>{
			SearchButton = B;
			B.Background = UiCfg.Inst.MainColor;
			B.Foreground = Brushes.Black;
			B.SetContent("搜尋");
			B.Click += (_, _)=>Ctx?.Search();
		});
		return R.Grid;
	}

	/// 建立列表与空结果提示叠层。
	private partial Control MkLangList(){
		var R = new Grid();
		R.A(new ScrollViewer(), S=>{
			LangScroll = S;
			S.SetContent(new StackPanel(), L=>{
				LangList = L;
				L.Orientation = Avalonia.Layout.Orientation.Vertical;
				L.Spacing = 0;
			});
		})
		.A(new TextBlock(), T=>{
			EmptyText = T;
			T.Text = "沒有匹配的語言";
			T.Foreground = Brushes.Gray;
			T.HorizontalAlignment = HAlign.Center;
			T.VerticalAlignment = VAlign.Center;
		});
		return R;
	}

	/// 重建语言项并同步空结果提示。
	private partial void RebuildLangItems(){
		if(LangList is null){
			return;
		}
		LangList.Children.Clear();
		if(Ctx is not null){
			foreach(var Lang in Ctx.Rows){
				LangList.A(MkLangItem(Lang));
			}
		}
		if(EmptyText is not null){
			EmptyText.IsVisible = Ctx?.IsEmpty != false;
		}
	}

	/// 建立代码、本地名和英文名三层信息的点击项。
	private partial Control MkLangItem(INormLang Lang){
		var R = new Button{
			Background = Brushes.Black,
			BorderBrush = UiCfg.Inst.GapLineBrush,
			BorderThickness = new(0, 0, 0, 0.5),
			CornerRadius = new(0),
			Padding = new(14, 8),
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
			T.FontSize = UiCfg.Inst.BaseFontSize * 0.48;
			T.VerticalAlignment = VAlign.Center;
		})
		.A(new StackPanel(), P=>{
			P.Spacing = 2;
			var Detail = Lang as INormLangDetail;
			P.A(new TextBlock(), T=>{
				T.Text = Detail?.NativeName ?? Lang.Code;
				T.Foreground = Brushes.White;
				T.FontSize = UiCfg.Inst.BaseFontSize * 0.48;
			})
			.A(new TextBlock(), T=>{
				T.Text = Detail?.EnglishName ?? "";
				T.Foreground = Brushes.Gray;
				T.FontSize = UiCfg.Inst.BaseFontSize * 0.36;
			});
		});
		R.SetContent(Row.Grid);
		R.Click += (_, _)=>SelectLang(Lang);
		return R;
	}

	/// 回传语言选择，不在覆盖层内部改变主页面状态。
	private partial void SelectLang(INormLang Lang){
		OnSelected?.Invoke(Lang);
	}

	/// 显式请求关闭覆盖层。
	private partial void Close(){
		OnCloseRequested?.Invoke();
	}

	/// 解除搜索结果订阅，避免已离开控件树的覆盖层继续重建列表。
	public partial void Dispose(){
		if(_ctxPropertyChangedHandler is not null && Ctx is not null){
			Ctx.PropertyChanged -= _ctxPropertyChangedHandler;
			_ctxPropertyChangedHandler = null;
		}
	}
}
