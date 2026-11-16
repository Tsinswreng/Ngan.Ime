using Ngan.Ime.Core.Keys;
using Rime.Api;
using Rime.Api.Types;
using Tsinswreng.CsInterop;

namespace Ngan.Ime.Rime;


[Doc($$"""
把 Core 的按鍵事件送進 librime 並收集結果。

*一次一批*：{{nameof(IImeKeyProcessor)}} 的契約是一批鍵一起處理，故這裡逐鍵 `process_key`、
逐鍵 `get_commit`；上屏文本與未處理鍵分別累積到回傳值。

*Enter 的雙重語義*：某些方案會用 Enter 提交候選（產生 commit），此時不能再把同一顆 Enter
當成「未處理鍵」交還給服務端，否則服務端會同時收到上屏文本與 Enter 鍵——等於提交兩次。
這裡用 `suppressUnhandledEnter` 記住「本批的 Enter 已產生 commit」，只影響 Enter 本身。

*日誌*：熱路徑只留 Trace；舊版每鍵兩條 Debug + 兩個 Stopwatch，一次按鍵就十幾條日誌。
""")]
unsafe public class RimeKeyProcessor : IImeKeyProcessor {

	[Doc("取引擎單例的工廠；由 DI 注入，避免本類直接依賴靜態單例（可測性）。")]
	readonly Func<RimeSetup> GetRimeSetup;

	[Doc("錯誤事件；本實現目前不主動觸發（原生崩潰不可捕獲），保留供服務端統一掛接。")]
	public event ErrHandler? OnErr;

	[Doc($$"""
	建立處理器。
	#Prm[{{nameof(GetRimeSetup)}}][延遲取引擎的工廠：真正初始化留到第一次按鍵或明顯的預熱時機。]
	""")]
	public RimeKeyProcessor(Func<RimeSetup> GetRimeSetup){
		this.GetRimeSetup = GetRimeSetup;
	}

	[Doc("逐鍵送進 librime，收集 commit 與未處理鍵。")]
	public Task<IRespOnKeyEvent> OnKeyEvents(IEnumerable<IKeyEvent> KeyEvents, CT Ct){
		var Resp = new RespOnKeyEvent();
		var Setup = GetRimeSetup();
		var Rime = Setup.apiFn;
		var Session = Setup.rimeSessionId;
		if(Session == 0){
			throw KeysErr.Rime.SessionInvalid.ToErr();
		}

		foreach(var KeyEvent in KeyEvents){
			Ct.ThrowIfCancellationRequested();
			var Key = RimeKeyCharConverter.Inst.Convert(KeyEvent);
			var Handled = Rime.process_key(Session, Key.Code, Key.Mask);
			var HasCommit = TryTakeCommit(Rime, Session, Resp);
			AppLog.Trace($"[Rime] key {KeyEvent.KeyChar.Name} down={KeyEvent.KeyState.IsKeyDown} handled={Handled}");

			// Enter 已在本批產生 commit 時，把它從「未處理」裡拿掉（見類註釋）。
			var IsEnter = KeyEvent.KeyChar.Equals(KeyChars.Enter);
			var Suppress = IsEnter && HasCommit;
			if(Handled == RimeUtil.False && !Suppress){
				Resp.UnhandledKeys.Add(KeyEvent);
			}
		}
		return Task.FromResult<IRespOnKeyEvent>(Resp);
	}

	[Doc($$"""
	取走本鍵產生的 commit 文本（若有）；無論成功與否都要 `free_commit`。
	#Prm[{{nameof(Rime)}}][函數表。]
	#Prm[{{nameof(Session)}}][會話 id。]
	#Prm[{{nameof(Resp)}}][累積結果。]
	#Rtn[本鍵是否產生了非空 commit。]
	""")]
	static bool TryTakeCommit(RimeApi Rime, RimeSessionId Session, RespOnKeyEvent Resp){
		var Commit = new RimeCommit{
			data_size = RimeUtil.DataSize<RimeCommit>(),
		};
		if(Rime.get_commit(Session, &Commit) == RimeUtil.False){
			return false;
		}
		var HasText = false;
		try{
			var Text = ToolCStr.ToCsStr(Commit.text);
			if(!string.IsNullOrEmpty(Text)){
				Resp.Commits.Add(Text);
				HasText = true;
			}
		}finally{
			Rime.free_commit(&Commit);
		}
		return HasText;
	}
}
