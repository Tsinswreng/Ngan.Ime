using CommunityToolkit.Mvvm.ComponentModel;

namespace Ngan.Ime.ViewModels;

public partial class MainViewModel : ViewModelBase
{
	[ObservableProperty]
	private string _greeting = "Welcome to Avalonia!";
}
