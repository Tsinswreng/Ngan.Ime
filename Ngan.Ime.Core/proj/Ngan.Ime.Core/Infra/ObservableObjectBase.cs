namespace Ngan.Ime.Infra;

using System.ComponentModel;
using System.Runtime.CompilerServices;
using Tsinswreng.CsCore;

[Doc($$"""
最小可觀察對象基類：只提供 {{nameof(INotifyPropertyChanged)}} 與 `SetProperty`。

*為何 Core 不直接用 `CommunityToolkit.Mvvm.ObservableObject`*：
Core 是「平臺無關的抽象與基礎設施」，不該為了屬性通知而依賴 MVVM 工具包；
界面層（`Ngan.Ime.UI`）照樣可以用 CommunityToolkit，但那裡才是它該出現的地方。
本類只依賴 BCL 的兩個介面，30 行，足夠 Core 用。

#Eg[
	```cs
	public str Preedit{
		get;
		protected set{ SetProperty(ref field, value); }
	} = "";
	```
]
""")]
public abstract class ObservableObjectBase : INotifyPropertyChanged, INotifyPropertyChanging {

	[Doc("屬性值變更後觸發。")]
	public event PropertyChangedEventHandler? PropertyChanged;

	[Doc("屬性值變更前觸發；目前沒有訂閱者，保留以符合 WPF 系約定。")]
	public event PropertyChangingEventHandler? PropertyChanging;

	[Doc($$"""
	值有變才寫入並發通知；相等比較用 `EqualityComparer<T>.Default`。
	#Prm[{{nameof(Field)}}][字段的 ref，必須是 `ref field` 形式。]
	#Prm[{{nameof(Value)}}][新值。]
	#Prm[{{nameof(PropertyName)}}][屬性名，由編譯器自動填入。]
	#Rtn[確實變更並已發通知時 true。]
	""")]
	protected bool SetProperty<T>(
		ref T Field
		,T Value
		,[CallerMemberName] str PropertyName = ""
	){
		if(EqualityComparer<T>.Default.Equals(Field, Value)){
			return false;
		}
		OnPropertyChanging(PropertyName);
		Field = Value;
		OnPropertyChanged(PropertyName);
		return true;
	}

	[Doc($$"""
	發送屬性變更通知。
	#Prm[{{nameof(PropertyName)}}][屬性名，由編譯器自動填入。]
	""")]
	protected void OnPropertyChanged([CallerMemberName] str PropertyName = ""){
		PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(PropertyName));
	}

	[Doc($$"""
	發送屬性變更前通知。
	#Prm[{{nameof(PropertyName)}}][屬性名，由編譯器自動填入。]
	""")]
	protected void OnPropertyChanging([CallerMemberName] str PropertyName = ""){
		PropertyChanging?.Invoke(this, new PropertyChangingEventArgs(PropertyName));
	}
}
