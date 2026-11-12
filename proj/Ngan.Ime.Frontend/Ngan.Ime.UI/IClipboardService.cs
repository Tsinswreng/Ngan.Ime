namespace Ngan.Ime.UI;

public interface IClipboardService
{
	public Task<IReadOnlyList<str>> GetItemsAsy(CT ct = default);
}
