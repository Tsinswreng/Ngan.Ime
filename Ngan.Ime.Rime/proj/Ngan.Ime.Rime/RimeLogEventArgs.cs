using Microsoft.Extensions.Logging;

namespace Ngan.Ime.Rime;

public class RimeLogEventArgs : EventArgs
{
	public required LogLevel Level { get; init; }
	public required string Message { get; init; }
}
