using Avalime.Core.Infra;
using Avalime.Core.Keys;
using Avalime.UI;
using Avalime.ViewModels;

namespace Avalime.UI.Views.KeyBoard;
using Ctx = VmKeyBoard;

public partial class VmKeyBoard : ViewModelBase
	, IMk<VmKeyBoard>
{
	public ISvcIme ImeState{get;set;}

	public VmKeyBoard(ISvcIme ImeState){
		this.ImeState = ImeState;
	}

	/// <summary>
	/// 鍵盤 VM 的兜底創建路徑。
	/// </summary>
	public static Ctx Mk()
	{
		return new Ctx(Di.GetRSvc<ISvcIme>());
	}

	public bool IsNumLayout{
		get => field;
		set => SetProperty(ref field, value);
	}

	public bool IsShiftLocked{
		get => field;
		set => SetProperty(ref field, value);
	}
}

