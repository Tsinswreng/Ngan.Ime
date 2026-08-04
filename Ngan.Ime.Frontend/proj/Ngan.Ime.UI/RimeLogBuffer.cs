using System.Collections.ObjectModel;
using Avalonia.Threading;
using Ngan.Ime.Core.Infra.Log;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Ngan.Ime.UI;

public class RimeLogBuffer : ObservableObject
	, IDisposable
{
	public ObservableCollection<string> Lines { get; } = [];

	const int MaxLines = 200;
	readonly IRimeLogSource _RimeLogSource;
	readonly EventHandler<RimeLogEntryEventArgs> _OnLogAppendedHandler;

	public RimeLogBuffer(IRimeLogSource RimeLogSource)
	{
		_RimeLogSource = RimeLogSource;
		foreach(var line in _RimeLogSource.Snapshot()){
			Lines.Add(line);
		}
		_OnLogAppendedHandler = (_, Args) => Append(Args.FormattedLine);
		_RimeLogSource.OnLogAppended += _OnLogAppendedHandler;
	}

	public void Append(string line)
	{
		Dispatcher.UIThread.Post(() => {
			Lines.Add(line);
			while(Lines.Count > MaxLines){
				Lines.RemoveAt(0);
			}
		});
	}

	public void AppendAndLog(Microsoft.Extensions.Logging.LogLevel level, string message)
	{
		AppLog.Inst.Log(level, 0, message, null, static (state, _) => state?.ToString() ?? "");
		Append($"[{level}] {message}");
	}

	public void Dispose()
	{
		_RimeLogSource.OnLogAppended -= _OnLogAppendedHandler;
	}
}
