namespace Ngan.Ime.Core.Infra.Log;
using Tsinswreng.CsLog;


public class AppLog:DelegatingLogger {
	public static AppLog Inst => field??=new AppLog();
}
