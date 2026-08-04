using Microsoft.Extensions.Logging;

namespace Ngan.Ime.Core.Infra.Log;

public delegate void FnLog(LogLevel logLevel, string message);
public delegate bool FnFilter(LogLevel logLevel);
public delegate bool FnIsEnabled(LogLevel logLevel);
public delegate IDisposable? FnBeginScope(Type TState, obj? State);

/// 通過委託函數配置輸出目標的 <see cref="ILogger"/> 實現。
public interface IFuncLogger : ILogger {
	public FnLog? FnLog { get; set; }
	public FnFilter Filter { get; set; }
	public FnIsEnabled FnIsEnabled { get; set; }
	public FnBeginScope? FnBeginScope { get; set; }
}


public class FuncLogger : IFuncLogger {
	public FnLog? FnLog { get; set; }
	public FnFilter Filter { get; set; } = _ => true;
	public FnIsEnabled FnIsEnabled { get; set; } = _ => true;
	public FnBeginScope? FnBeginScope { get; set; }
	public FuncLogger() { }

	public FuncLogger(FnLog Write, FnFilter? Filter = null) {
		this.FnLog = Write;
		if (Filter != null) this.Filter = Filter;
	}

	public void Log(LogLevel logLevel, string message) {
		FnLog?.Invoke(logLevel, message);
	}

	public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter){
		Log(logLevel, formatter(state, exception));
	}

	public bool IsEnabled(LogLevel logLevel){
		return FnIsEnabled(logLevel);
	}

	public IDisposable? BeginScope<TState>(TState state)
		where TState : notnull
	{
		return FnBeginScope?.Invoke(typeof(TState), state);
	}
}
