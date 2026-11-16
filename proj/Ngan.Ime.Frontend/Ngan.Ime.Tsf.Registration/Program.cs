namespace Ngan.Ime.Tsf.Registration;

using Tsinswreng.CsCore;
using System;

[Doc($$"""
Explicit command-line entrypoint for per-user TSF registration.
""")]
internal static class Program {
	[Doc($$"""
	在 STA 執行緒上依明確命令註冊或移除 POC。
	""")]
	[STAThread]
	private static i32 Main(str[] Args) {
		return TsfRegistration.Run(Args);
	}
}
