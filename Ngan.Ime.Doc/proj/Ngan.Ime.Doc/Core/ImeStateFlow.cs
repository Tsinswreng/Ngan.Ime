namespace Ngan.Ime.Doc.Core;

using Tsinswreng.CsCore;

[Doc($$"""
#H[用途][
	`ImeState` 是整個輸入鏈的狀態樞紐。
	它接收按鍵事件，按順序分發給 `IImeKeyProcessor`，再通知 `AfterInput`。
]

#H[核心流程][
	- `Input(IEnumerable<IKeyEvent>)`
	- `InputSafely(IEnumerable<IKeyEvent>, Action<Exception>?)`
	- `BeforeInput`
	- `ImeKeyProcessor.OnKeyEventsAsy(...)` → 返回 `RespOnKeyEvent`（含 `Commits`、`UnhandledKeys`）
	- 若有 commit 文字，逐一觸發 `OnCommit`
	- `AfterInput`
	- 若有未處理按鍵，轉發給 `OsKeyProcessor.OnKeyEventsAsy`
]

#H[異常與線程][
	`InputSafely(...)` 用於 UI 端的按鍵入口。
	它把 `Input(...)` 放到後台 `Task` 執行，避免按鍵直接阻塞 UI 線程，
	並在同一入口統一捕獲異常、寫日誌、回調錯誤處理器。

	因此 `AfterInput`、`OnCommit` 的訂閱方不能假定自己一定運行在 UI 線程，
	凡是要改綁定屬性或調平台 UI API，都要自行切回 UI / 主線程。
]

#H[Commit 機制][
	`OnCommit` 事件在 `AfterInput` 之前觸發。
	它接收 Rime 引擎 commit 的文字（由 `RimeKeyProcessor` 通過 `get_commit` 取得），
	並通知平台層將文字輸出到 OS 輸入框。
	每個 commit 文字會單獨觸發一次事件。
]

#H[未處理按鍵轉發][
	當 Rime 引擎不處理某個按鍵（`process_key` 返回 `False`），
	該按鍵被收集到 `RespOnKeyEvent.UnhandledKeys`。
	`ImeState` 將這些按鍵轉發給 `OsKeyProcessor`，
	由平台層輸出到 OS（如 Android 的 `InputConnection.SendKeyEvent` 或 `CommitText`）。
]

#H[觀察點][
	- `VmInput` 用 `AfterInput` 讀 preedit
	- `VmCandidatesBar` 用 `AfterInput` 刷候選詞（最多 16 個）
	- Android `AvalimeInputMethodService` 訂閱 `OnCommit`，通過 `CommitText` 輸出到目標 App
	- 入口層註冊 `Ngan.Ime.Rime.SvcIme : ISvcIme`，由它負責依賴 `RimeSetup` 並在 `AfterInput` 後把 `RimeStatus/RimeContext` 回填到 `Preedit` / `Candidates` / `IsComposing`
	- `AndroidOsKeyProcessor` 實現 `I_OsKeyProcessor`，將未處理按鍵轉發給 OS
	- `RimeConnectionState` 維護 `IsAsciiMode` 屬性，通過 `RimeSetup.OnOptionChanged` 靜態事件同步引擎狀態（回調一律經 `Dispatcher.UIThread.Post` 確保在 UI 線程更新）
	- `ToggleAsciiMode()` 在後台線程調用 `get_option`/`set_option`（避免 ~350ms 原生調用阻塞 UI），通過 `Interlocked` 防並發，直接更新 `IsAsciiMode` 後經 `Dispatcher.UIThread.Post` 同步 UI
]

""")]
file class _{
}
