using Ngan.Ime.Core.Ime;
using Ngan.Ime.Core.Infra.Log;
using Ngan.Ime.Core.Keys;
using Microsoft.Extensions.DependencyInjection;
using Rime.Api;
using Rime.Api.Types;
using Tsinswreng.CsInterop;

namespace Ngan.Ime.Rime;

/// ISvcIme 的 Rime 引擎實現。
/// 負責把 Rime 引擎狀態回填到 Core 狀態模型。
public class SvcIme : ISvcIme
{
	readonly IServiceProvider _SvcProvider;
	readonly Lock _StateLock = new();
	readonly SemaphoreSlim _ConnectLock = new(1, 1);
	i32 _IsTogglingAscii;
	i32 _IsTogglingSimplification;
	volatile Task? _ConnectTask;
	RimeSetup? _RimeSetup;

	public SvcIme(
		IOsKeyProcessor OsKeyProcessor
		, IImeKeyProcessor ImeKeyProcessor
		, IServiceProvider SvcProvider
	) : base(OsKeyProcessor, ImeKeyProcessor){
		_SvcProvider = SvcProvider;
		AfterInput += OnAfterInput;
		RimeSetup.OnOptionChanged += OnOptionChanged;
	}

	/// 懶取後端 RimeSetup。
	/// 這裡故意不在構造函數裏碰 RimeSetup，讓 UI 可以先實例化 SvcIme 再異步初始化後端。
	RimeSetup GetRimeSetup()
	{
		_RimeSetup ??= _SvcProvider.GetRequiredService<RimeSetup>();
		return _RimeSetup;
	}

	/// 真正建立 Core 與 Rime session 的連通狀態。
	/// 首次調用時在後台懶初始化 Rime；後續重複調用則複用同一輪初始化任務。
	public override async Task ConnectAsy(CT Ct = default){
		Ct.ThrowIfCancellationRequested();
		var ConnectTask = _ConnectTask;
		if(ConnectTask is null || ConnectTask.IsCompleted){
			await _ConnectLock.WaitAsync(Ct);
			try{
				ConnectTask = _ConnectTask;
				if(ConnectTask is null || ConnectTask.IsCompleted){
					ConnectTask = ConnectCoreAsy(Ct);
					_ConnectTask = ConnectTask;
				}
			}finally{
				_ConnectLock.Release();
			}
		}
		await ConnectTask.WaitAsync(Ct);
	}

	/// 單次真正的初始化/連接實現。
	/// 這裡把同步重活包進後台執行，避免 IME 首屏 UI 被 RimeSetup 構造直接堵住。
	async Task ConnectCoreAsy(CT Ct){
		IsConnecting = true;
		StatusText = "正在連接 Rime";
		var IsSuccess = false;
		try{
			await Task.Run(() => {
				Ct.ThrowIfCancellationRequested();
				var RimeSetup = GetRimeSetup();
				_ = RimeSetup.rimeSessionId;
				SyncStateFromRime();
			}, Ct);
			IsConnected = true;
			StatusText = "Rime 已連接";
			IsSuccess = true;
		}catch(Exception Ex){
			IsConnected = false;
			StatusText = "Rime 連接失敗: " + Ex.Message;
			AppLog.Error(Ex, "[AvalimeRime] ConnectAsy failed");
			throw;
		}finally{
			IsConnecting = false;
			RaiseConnectCompleted(IsSuccess);
		}
	}

	/// 通過 Rime API 切換 ascii_mode。
	public override Task ToggleAsciiModeAsy(CT Ct = default){
		if(Interlocked.Exchange(ref _IsTogglingAscii, 1) != 0){
			return Task.CompletedTask;
		}
		return Task.Run(() => {
			try{
				Ct.ThrowIfCancellationRequested();
				unsafe{
					lock(_StateLock){
						var RimeSetup = GetRimeSetup();
						var Rime = RimeSetup.apiFn;
						var SessionId = RimeSetup.rimeSessionId;
						byte* Option = stackalloc byte[15];
						WriteAsciiMode(new Span<byte>(Option, 15));
						var Current = Rime.get_option(SessionId, Option);
						Rime.set_option(SessionId, Option, Current == RimeUtil.False ? RimeUtil.True : RimeUtil.False);
					}
				}
				SyncStateFromRime();
			}finally{
				Interlocked.Exchange(ref _IsTogglingAscii, 0);
			}
		}, Ct);
	}

	/// 通過 Rime API 切換 simplification。
	public override Task ToggleSimplificationAsy(CT Ct = default){
		if(Interlocked.Exchange(ref _IsTogglingSimplification, 1) != 0){
			return Task.CompletedTask;
		}
		return Task.Run(() => {
			try{
				Ct.ThrowIfCancellationRequested();
				unsafe{
					lock(_StateLock){
						var RimeSetup = GetRimeSetup();
						var Rime = RimeSetup.apiFn;
						var SessionId = RimeSetup.rimeSessionId;
						byte* Option = stackalloc byte[15];
						WriteSimplification(new Span<byte>(Option, 15));
						var Current = Rime.get_option(SessionId, Option);
						Rime.set_option(SessionId, Option, Current == RimeUtil.False ? RimeUtil.True : RimeUtil.False);
					}
				}
				SyncStateFromRime();
			}finally{
				Interlocked.Exchange(ref _IsTogglingSimplification, 0);
			}
		}, Ct);
	}

	/// 每次輸入後從 Rime 抓最新快照，回流到 Core 狀態。
	void OnAfterInput(object? Sender, IEnumerable<IKeyEvent> Args){
		try{
			var sw = System.Diagnostics.Stopwatch.StartNew();
			SyncStateFromRime();
			AppLog.Debug($"[Perf] AvalimeRime.OnAfterInput SyncStateFromRime total: {sw.ElapsedMilliseconds}ms");
		}catch(Exception Ex){
			AppLog.Error(Ex, "[AvalimeRime] SyncStateFromRime after input failed");
		}
	}

	/// 逆向 P/Invoke 回調只傳 option 名和值，實際狀態同步仍統一回到這裡。
	void OnOptionChanged(str OptionName, bool IsEnabled){
		try{
			switch(OptionName){
				case "ascii_mode":
					IsAsciiMode = IsEnabled;
					break;
				case "simplification":
					IsSimplification = IsEnabled;
					break;
			}
		}catch(Exception Ex){
			AppLog.Error(Ex, "[AvalimeRime] OnOptionChanged sync failed");
		}
	}

	/// 將 Rime session 的 status/context 轉成 Core 狀態。
	unsafe void SyncStateFromRime(){
		var swTotal = System.Diagnostics.Stopwatch.StartNew();
		lock(_StateLock){
			var RimeSetup = GetRimeSetup();
			var Rime = RimeSetup.apiFn;
			var SessionId = RimeSetup.rimeSessionId;
			if(SessionId == 0){
				IsConnected = false;
				StatusText = "Rime session 無效";
				ReplacePreedit("");
				ReplaceCandidates(new());
				IsComposing = false;
				return;
			}

			var Status = new RimeStatus{
				data_size = RimeUtil.DataSize<RimeStatus>()
			};
			var swStatus = System.Diagnostics.Stopwatch.StartNew();
			if(Rime.get_status(SessionId, &Status) != RimeUtil.False){
				try{
					IsAsciiMode = Status.is_ascii_mode != RimeUtil.False;
					IsSimplification = Status.is_simplified != RimeUtil.False;
					IsComposing = Status.is_composing != RimeUtil.False;
				}finally{
					Rime.free_status(&Status);
				}
			}
			AppLog.Debug($"[Perf] AvalimeRime.SyncStateFromRime get_status: {swStatus.ElapsedMilliseconds}ms");

			var Context = new RimeContext{
				data_size = RimeUtil.DataSize<RimeContext>()
			};
			var swContext = System.Diagnostics.Stopwatch.StartNew();
			if(Rime.get_context(SessionId, &Context) != RimeUtil.False){
				try{
					var swCopy = System.Diagnostics.Stopwatch.StartNew();
					ReplacePreedit(MkDisplayedPreedit(Context.composition));
					var page = ReadCandidates(&Context);
					ReplaceCandidates(page);
					AppLog.Debug($"[Perf] AvalimeRime.SyncStateFromRime copy_context_to_managed: {swCopy.ElapsedMilliseconds}ms, candidates: {page.Data?.Count ?? 0}");
				}finally{
					Rime.free_context(&Context);
				}
			}else{
				ReplacePreedit("");
				ReplaceCandidates(new());
			}
			AppLog.Debug($"[Perf] AvalimeRime.SyncStateFromRime get_context: {swContext.ElapsedMilliseconds}ms");
		}
		AppLog.Debug($"[Perf] AvalimeRime.SyncStateFromRime total: {swTotal.ElapsedMilliseconds}ms");
	}

	/// 先按 librime 的 soft cursor 思路，把 composition.preedit 與 cursor_pos 組裝成當前要顯示的 preedit。
	/// 目前先插入 U+2038 `‸`，優先補齊 Ngan.Ime 缺失的輸入游標顯示。
	unsafe str MkDisplayedPreedit(RimeComposition composition) {
		var preedit = ToolCStr.ToCsStr(composition.preedit) ?? "";
		if(preedit.Length <= 0){
			return preedit;
		}

		const str Caret = "\u2038";
		var cursorPos = composition.cursor_pos;
		if(cursorPos < 0){
			cursorPos = 0;
		}
		if(cursorPos > preedit.Length){
			cursorPos = preedit.Length;
		}
		return preedit.Insert(cursorPos, Caret);
	}

	/// 在 native context 有效期間把候選詞複製成 Core DTO，包含分頁訊息。
	unsafe CandidatePage ReadCandidates(RimeContext* Context){
		var Ans = new List<ICandidate>();
		var Menu = Context->menu;
		for(i32 Index = 0; Index < Menu.num_candidates; Index++){
			var Candidate = Menu.candidates[Index];
			Ans.Add(new Candidate{
				Text = ToolCStr.ToCsStr(Candidate.text) ?? ""
				, Comment = ToolCStr.ToCsStr(Candidate.comment) ?? ""
			});
		}
		return new CandidatePage{
			Data = Ans,
			PageIdx = (u64)Menu.page_no,
			PageSize = (u64)Menu.page_size,
			IsLastPage = Menu.is_last_page != RimeUtil.False,
			HighlightedIndex = Menu.highlighted_candidate_index,
		};
	}

	/// 把 ascii_mode 寫入 stackalloc buffer。
	static void WriteAsciiMode(Span<byte> Buffer){
		Buffer.Clear();
		"ascii_mode"u8.CopyTo(Buffer);
	}

	/// 把 simplification 寫入 stackalloc buffer。
	static void WriteSimplification(Span<byte> Buffer){
		Buffer.Clear();
		"simplification"u8.CopyTo(Buffer);
	}
}
