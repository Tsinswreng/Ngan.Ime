using Ngan.Ime.Core.Keys;
using Ngan.Ime.Core.Infra.Log;
using Microsoft.Extensions.DependencyInjection;
using Rime.Api;
using Tsinswreng.CsInterop;

namespace Ngan.Ime.Rime;


unsafe public class RimeKeyProcessor
	: IImeKeyProcessor
{
	public event ErrHandler? OnErr;
	readonly IServiceProvider _SvcProvider;
	RimeSetup? _RimeSetup;
	public RimeKeyProcessor(IServiceProvider svcProvider) {
		_SvcProvider = svcProvider;
	}

	RimeSetup GetRimeSetup() {
		_RimeSetup ??= _SvcProvider.GetRequiredService<RimeSetup>();
		return _RimeSetup;
	}

	public async Task<IRespOnKeyEvent> OnKeyEvents(IEnumerable<IKeyEvent> KeyEvents, CT Ct) {
		var sw = System.Diagnostics.Stopwatch.StartNew();
		AppLog.Debug($"[Perf] RimeKeyProcessor start: {sw.ElapsedMilliseconds}ms");
		var resp = new RespOnKeyEvent();
		var rimeSetup = GetRimeSetup();
		var rime = rimeSetup.apiFn;
		var suppressUnhandledEnterForBatch = false;
		foreach (var keyEvent in KeyEvents) {
			var tuple = RimeKeyCharConverter.Inst.Convert(keyEvent);

			var swPk = System.Diagnostics.Stopwatch.StartNew();
			var handled = rime.process_key(
				rimeSetup.rimeSessionId
				,tuple.Item1
				,tuple.Item2
			);
			AppLog.Debug($"[Perf] RimeKeyProcessor process_key({keyEvent.KeyChar.Name}, {(keyEvent.KeyState.IsKeyDown?"Down":"Up")}): {swPk.ElapsedMilliseconds}ms, handled={handled}");

			// 檢查 commit 這裏的commit是沒有進translator 出候選那種 直接在processor階段就上屏的
			var swCommit = System.Diagnostics.Stopwatch.StartNew();
			var commit = new RimeCommit();
			commit.data_size = RimeUtil.DataSize<RimeCommit>();
			var hasCommitText = false;
			if(rime.get_commit(rimeSetup.rimeSessionId, &commit) != RimeUtil.False){
				var text = ToolCStr.ToCsStr(commit.text);
				if(!string.IsNullOrEmpty(text)){
					hasCommitText = true;
					resp.Commits.Add(text);
				}
				rime.free_commit(&commit);
			}
			AppLog.Debug($"[Perf] RimeKeyProcessor get_commit: {swCommit.ElapsedMilliseconds}ms");

			var isEnterKey = keyEvent.KeyChar.Name == nameof(KeyChars.Enter);
			if(isEnterKey && hasCommitText){
				suppressUnhandledEnterForBatch = true;
			}

			// 當 Enter 已經在本批次內產生 commit 時，
			// 這一輪的 Enter 不應再作為原生按鍵 fallback 給宿主；
			// 否則宿主會同時收到 CommitText 與 KeyEvent(Enter) 兩套語義。
			var shouldFallbackToOs = handled == RimeUtil.False
				&& !(isEnterKey && suppressUnhandledEnterForBatch);
			if(shouldFallbackToOs){
				resp.UnhandledKeys.Add(keyEvent);
			}
		}
		AppLog.Debug($"[Perf] RimeKeyProcessor total: {sw.ElapsedMilliseconds}ms, unhandled: {resp.UnhandledKeys.Count}");
		return resp;
	}
}
