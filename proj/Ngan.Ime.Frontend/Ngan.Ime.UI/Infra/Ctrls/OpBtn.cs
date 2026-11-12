namespace Ngan.Ime.UI.Infra.Ctrls;

using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Tsinswreng.CsCore;

[Doc(@$"Operation Button.
when processing, underline will have rolling bar.
設置背景要用 _Button.Background、不能直接.Backround
")]
public partial class OpBtn : ContentControl{

	public enum EState{
		Ready,
		Working,
		Disabled
	}

	public Button _Button{get;set;} = new();
	public CancellationTokenSource Cts{get;set;} = new();
	public Func<CT, Task<nil>?>? FnExeAsy{get;set;} = async _ => NIL;
	public Func<obj?>? FnOk{get;set;}
	public Func<Exception?, obj?>? FnFail{get;set;}
	public Func<obj?>? FnCancel{get;set;}
	public EState State{get;set;}

	public Grid Grid = new();
	public Control Overlay;

	void Start(){
		Cts = new();
		State = EState.Working;
		SetupOverlay();
	}

	void End(){
		Dispatcher.UIThread.Post(() => {
			State = EState.Ready;
			SetdownOverlay();
		});
	}

	void Cancel(){
		Cts.Cancel();
		SetdownOverlay();
	}

	void SetdownOverlay(){
		if(Grid.Children.Count > 1){
			var overlay = Grid.Children[1];
			overlay.IsVisible = false;
		}
	}

	void SetupOverlay(){
		if(Grid.Children.Count > 1){
			var overlay = Grid.Children[1];
			overlay.IsVisible = true;
		}else{
			Overlay.IsVisible = true;
			Grid.Children.Add(Overlay);
		}
	}

	public obj? BtnContent{
		get => _Button?.Content;
		set => _Button.Content = value;
	}

	public void PerformClick(){
		_Button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
	}

	public OpBtn(){
		base.Content = Grid;
		Grid.RowDefinitions.Add(new RowDefinition(1, GUT.Star));
		_Button.VerticalAlignment = VAlign.Stretch;
		_Button.HorizontalAlignment = HAlign.Stretch;
		Grid.Children.Add(_Button);

		Overlay = MkOverlay();

		_Button.Click += (_, _) => {
			if(State == EState.Working){
				Cancel();
				return;
			}

			Start();
			var task = FnExeAsy?.Invoke(Cts.Token);
			if(task is null){
				End();
				return;
			}
			task.ContinueWith(t => {
				if(t.IsCanceled){
					FnCancel?.Invoke();
				}else if(t.IsFaulted){
					FnFail?.Invoke(t.Exception?.InnerException);
				}else{
					FnOk?.Invoke();
				}
				End();
			});
		};
	}

	public Control MkOverlay(){
		var bar = new ProgressBar{
			IsIndeterminate = true,
			VerticalAlignment = VAlign.Bottom,
			HorizontalAlignment = HAlign.Stretch,
			Height = 4,
			Margin = new(0),
			IsHitTestVisible = false
		};

		return new ZeroDecorator{Child = bar};
	}
}

class ZeroDecorator : Decorator{
	protected override Size MeasureOverride(Size availableSize)
		=> new(0, 0);
}
