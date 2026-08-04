using System.Collections.ObjectModel;
using Ngan.Ime.Core.Infra;
using Ngan.Ime.ViewModels;

namespace Ngan.Ime.UI.Views.RimeLog;

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

