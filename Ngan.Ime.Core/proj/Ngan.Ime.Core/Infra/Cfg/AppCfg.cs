namespace Ngan.Ime.Core.Infra;

using Tsinswreng.CsCfg;

public partial class AppCfg: DualSrcCfg, ICfgAccessor{
	protected static AppCfg? _Inst = null;
	public static AppCfg Inst => _Inst ??= new AppCfg();
}
