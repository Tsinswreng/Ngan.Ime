namespace Ngan.Ime.Tsf.Registration;

using System;

/// <summary>Explicit command-line entrypoint for per-user TSF registration.</summary>
internal static class Program {
    /// <summary>在 STA 執行緒上依明確命令註冊或移除 POC。</summary>
    [STAThread]
    private static int Main(string[] Args) {
        return TsfRegistration.Run(Args);
    }
}
