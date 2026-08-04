using Android.Content;

namespace Ngan.Ime.Android;

public class AndroidClipboardService : Ngan.Ime.UI.IClipboardService
{
	public Task<IReadOnlyList<str>> GetItemsAsy(CT ct = default){
		var clipboard = global::Android.App.Application.Context.GetSystemService(Context.ClipboardService) as ClipboardManager;
		if(clipboard?.PrimaryClip is null || clipboard.PrimaryClip.ItemCount <= 0){
			return Task.FromResult<IReadOnlyList<str>>([]);
		}

		var ans = new List<str>();
		for(var i = 0; i < clipboard.PrimaryClip.ItemCount; i++){
			var item = clipboard.PrimaryClip.GetItemAt(i);
			var text = item?.CoerceToText(global::Android.App.Application.Context)?.ToString();
			if(!string.IsNullOrWhiteSpace(text)){
				ans.Add(text);
			}
		}
		return Task.FromResult<IReadOnlyList<str>>(ans);
	}
}
