namespace Avalime.Core.Infra.Log;

using Microsoft.Extensions.Logging;

public static class ExtnAppLog {
	extension(AppLog) {
		public static void Trace(string message) => Write(LogLevel.Trace, message);
		public static void Debug(string message) => Write(LogLevel.Debug, message);
		public static void Info(string message) => Write(LogLevel.Information, message);
		public static void Warn(string message) => Write(LogLevel.Warning, message);
		public static void Error(string message) => Write(LogLevel.Error, message);
		public static void Error(Exception ex, string? message = null) {
			Write(LogLevel.Error, string.IsNullOrWhiteSpace(message) ? ex.ToString() : message + "\n" + ex);
		}

		static void Write(LogLevel level, string message) {
			AppLog.Inst.Log(level, 0, message, null, static (state, _) => state?.ToString() ?? "");
		}
	}
}
