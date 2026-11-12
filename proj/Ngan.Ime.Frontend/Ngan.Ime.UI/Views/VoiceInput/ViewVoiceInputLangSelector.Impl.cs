//Impl: 聲明見 ViewVoiceInputLangSelector.cs。
namespace Ngan.Ime.UI.Views.VoiceInput;

using System.Collections.Specialized;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Ngan.Ime.Core.Language;
using Ngan.Ime.UI.Infra;

public partial class ViewVoiceInputLangSelector{

	[Doc("搜索框字級比例。")]
	const f64 SearchFontScale = 0.48;

	[Doc("語言項字級比例。")]
	const f64 ItemFontScale = 0.44;

	[Doc("語言項次要文字比例。")]
	const f64 ItemSubFontScale = 0.34;

	[Doc("組出「搜索列 ＋ 列表 ＋ 空提示」。")]
	protected override partial object Build(VmVoiceInputLangSelector vm){
		_LangList = new StackPanel{
			Orientation = Avalonia.Layout.Orientation.Vertical,
			Spacing = 0,
		};
		_EmptyText = new TextBlock()
			.Text(Todo.I18n("沒有匹配的語言"))
			.Foreground(Brushes.Gray)
			.HorizontalAlignment(HAlign.Center)
			.VerticalAlignment(VAlign.Center);
		var ListArea = new Grid().Children(
			new ScrollViewer()
				.With(S => {
					S.VerticalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Auto;
					S.Content = _LangList;
				})
			,_EmptyText
		);
		return new Border()
			.Background(Brushes.Black)
			.BorderBrush(UiCfg.Inst.GapLineBrush)
			.BorderThickness(new Avalonia.Thickness(1))
			.Child(new StackGrid(IsRow: true)
				.Rows("Auto,*")
				.Children(MkSearchBar(vm), ListArea));
	}

	[Doc("頂部搜索列：返回、輸入框、清空、搜索。")]
	Control MkSearchBar(VmVoiceInputLangSelector vm){
		_CloseButton = new Button()
			.Content(Todo.I18n("返回"))
			.With(B => B.Click += (_, _) => OnCloseRequested?.Invoke());
		_SearchInput = new TextBox()
			.With(T => {
				T.PlaceholderText = Todo.I18n("語言名稱或 BCP 47 代碼");
				T.FontSize = UiCfg.Inst.BaseFontSize * SearchFontScale;
				T.Background = UiCfg.Inst.CandidateBgColor;
				T.Foreground = Brushes.White;
				T.BorderBrush = UiCfg.Inst.GapLineBrush;
				T.KeyDown += (_, Args) => {
					if(Args.Key == Key.Enter){
						vm.Search();
						Args.Handled = true;
					}
				};
			})
			.Text(vm, X => X.Input, Avalonia.Data.BindingMode.TwoWay);
		_ClearSearchButton = new Button()
			.Content("×")
			.With(B => B.Click += (_, _) => vm.ClearSearch());
		_SearchButton = new Button()
			.With(B => {
				B.Background = UiCfg.Inst.MainColor;
				B.Foreground = Brushes.Black;
				B.Click += (_, _) => vm.Search();
			})
			.Content(Todo.I18n("搜尋"));
		return new Grid()
			.Cols("Auto,*,Auto,Auto")
			.Children(_CloseButton, _SearchInput, _ClearSearchButton, _SearchButton);
	}

	[Doc("訂閱結果集合：每次重建列表（項數少，簡單可靠）。")]
	protected override partial void OnAfterInitialized(){
		vm.Rows.CollectionChanged += OnRowsChanged;
		RebuildLangItems();
	}

	void OnRowsChanged(obj? Sender, NotifyCollectionChangedEventArgs Args){
		RebuildLangItems();
	}

	[Doc("清空並重建語言項；同步空結果提示。")]
	partial void RebuildLangItems(){
		if(_LangList is null){
			return;
		}
		_LangList.Children.Clear();
		foreach(var Lang in vm.Rows){
			_LangList.Children.Add(MkLangItem(Lang));
		}
		if(_EmptyText is not null){
			_EmptyText.IsVisible = vm.IsEmpty;
		}
	}

	[Doc("單個語言項：代碼用主色、本地名為主文字、英文名為次要文字。")]
	private partial Control MkLangItem(INormLang Lang){
		var Detail = Lang as INormLangDetail;
		return new Button()
			.With(B => {
				B.Background = Brushes.Black;
				B.BorderBrush = UiCfg.Inst.GapLineBrush;
				B.BorderThickness = new Avalonia.Thickness(0, 0, 0, 0.5);
				B.CornerRadius = new Avalonia.CornerRadius(0);
				B.HorizontalAlignment = HAlign.Stretch;
				B.HorizontalContentAlignment = HAlign.Left;
				B.Padding = new Avalonia.Thickness(10, 6);
				B.Click += (_, _) => OnSelected?.Invoke(Lang);
			})
			.Content(new StackGrid(IsRow: false)
				.Cols("Auto,*")
				.Children(
					new TextBlock()
						.Text(Lang.Code)
						.Foreground(UiCfg.Inst.MainColor)
						.FontSize(UiCfg.Inst.BaseFontSize * ItemSubFontScale)
						.Margin(new Avalonia.Thickness(0, 0, 8, 0))
					,new StackGrid(IsRow: true)
						.Rows("Auto,Auto")
						.Children(
							new TextBlock()
								.Text(Detail?.NativeName ?? Lang.Code)
								.Foreground(Brushes.White)
								.FontSize(UiCfg.Inst.BaseFontSize * ItemFontScale)
							,new TextBlock()
								.Text(Detail?.EnglishName ?? "")
								.Foreground(Brushes.Gray)
								.FontSize(UiCfg.Inst.BaseFontSize * ItemSubFontScale)
								.IsVisible(Detail is not null && !string.IsNullOrWhiteSpace(Detail.EnglishName))
						)
				));
	}

	[Doc("解除集合訂閱並釋放 ViewModel。")]
	public partial void Dispose(){
		if(vm is not null){
			vm.Rows.CollectionChanged -= OnRowsChanged;
		}
		GC.SuppressFinalize(this);
	}
}
