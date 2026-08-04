namespace Ngan.Ime.Android;

using Microsoft.Extensions.Logging;
using AndroidLog = global::Android.Util.Log;

public class AndroidLogger : ILogger
{
	readonly string _tag;
	readonly LogLevel _minLevel;

	public AndroidLogger(string tag, LogLevel minLevel = LogLevel.Debug){
		_tag = tag;
		_minLevel = minLevel;
	}

	public IDisposable BeginScope<TState>(TState state)
		where TState : notnull
	{
		return NoOpDisposable.Inst;
	}

	public bool IsEnabled(LogLevel logLevel){
		return logLevel >= _minLevel && logLevel != LogLevel.None;
	}

	public void Log<TState>(
		LogLevel logLevel
		, EventId eventId
		, TState state
		, Exception? exception
		, Func<TState, Exception?, string> formatter
	){
		if(!IsEnabled(logLevel)){
			return;
		}
		var message = formatter(state, exception);
		if(string.IsNullOrWhiteSpace(message) && exception is null){
			return;
		}
		if(exception is not null){
			message = string.IsNullOrWhiteSpace(message)
				? exception.ToString()
				: message + "\n" + exception;
		}
		switch(logLevel){
			case LogLevel.Trace:
			case LogLevel.Debug:
				AndroidLog.Debug(_tag, message);
				break;
			case LogLevel.Information:
				AndroidLog.Info(_tag, message);
				break;
			case LogLevel.Warning:
				AndroidLog.Warn(_tag, message);
				break;
			case LogLevel.Error:
			case LogLevel.Critical:
				AndroidLog.Error(_tag, message);
				break;
		}
	}

	sealed class NoOpDisposable : IDisposable
	{
		public static NoOpDisposable Inst { get; } = new();
		public void Dispose(){}
	}
}
