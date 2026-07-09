using System.Collections.ObjectModel;
using Avalime.Core.Infra;
using Avalime.ViewModels;

namespace Avalime.UI.Views.RimeLog;

public class VmRimeLog : ViewModelBase
	, IMk<VmRimeLog>
{
	public ObservableCollection<string> Lines { get; }

	public VmRimeLog(RimeLogBuffer RimeLogBuffer){
		Lines = RimeLogBuffer.Lines;
	}

	/// <summary>
	/// 日誌 VM 的兜底創建路徑。
	/// </summary>
	public static VmRimeLog Mk()
	{
		return new VmRimeLog(Di.GetRSvc<RimeLogBuffer>());
	}
}

