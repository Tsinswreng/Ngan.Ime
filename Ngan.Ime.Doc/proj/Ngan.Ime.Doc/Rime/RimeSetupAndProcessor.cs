namespace Ngan.Ime.Doc.Rime;

using Tsinswreng.CsCore;

[Doc($$"""
#H[用途][
	`RimeSetup` 負責載入 `librime.so`、初始化 traits、建立 session。
	`RimeKeyProcessor` 負責把按鍵轉成 Rime key code 並送入 session。
]

#H[Android 初始化][
	Android 端在 `Application` 啟動時初始化雙源配置：
	- `Ngan.Ime.Ro.jsonc` 只提供 `RwCfgPath`
	- `Ngan.Ime.Rw.jsonc` 提供 `Librime` 結構
	- 宿主再把實際 `DllPath`、`RimeTraits.user_data_dir`、`RimeTraits.distribution_code_name`、`RimeTraits.app_name` 寫入可寫層
	- 之後由 `RimeSetup.Inst` 從 `AppCfg.Inst` 讀取配置並完成初始化
	- 另外新增 `RimeWarmup`：`Application` 與 Android IME `OnCreate()` 會在後台先觸發一次預熱
	- 預熱的本質是提早構造 `RimeSetup.Inst`，把 `initialize/start_maintenance/join_maintenance_thread/create_session` 儘量前移
	- 這樣首次 editor 聚焦時，UI 雖然仍可能拿到同一個 `RimeSetup` 單例，但大概率不必再同步等待整套 librime 初始化
]

#H[按鍵轉換][
	`RimeKeyCharConverter` 負責把 `IKeyEvent` 轉成 Rime 的 keycode / mask。
]

#H[Commit 檢測][
	`RimeKeyProcessor.OnKeyEventsAsy` 在每個 `process_key` 之後調用 `get_commit`。
	若有 commit 文字，收集到 `RespOnKeyEvent.Commits` 中返回給 `ImeState`，然後調用 `free_commit` 釋放。
]

#H[Option 通知][
	`RimeSetup.on_message` 以零分配字節比較解析 Rime 的 `option` 通知（如 `ascii_mode` / `!ascii_mode`），
	通過靜態事件 `OnOptionChanged` 通知訂閱者。
	**注意**：此回調為逆向 P/Invoke（原生→託管），必須零分配以避免在回調期間觸發 GC。
	若在 `process_key` / `set_option` 原生調用中分配託管字符串，Mono 運行時的 `!ji->async` 斷言會觸發 SIGABRT 崩潰。
	已有教訓：之前在逆向 P/Invoke 中做 `ToolCStr.ToCsStr` 這類託管分配，再配合即時日誌輸出，會增加回調期觸發 GC 的風險；
	多次滑動 Y 鍵後曾導致 Finalizer 線程崩潰。
]

#H[未處理按鍵檢測][
	`process_key` 返回值表示 Rime 是否處理了該按鍵。
	若返回 `False`，按鍵被收集到 `RespOnKeyEvent.UnhandledKeys`。
	此方法為同步方法（`Task.FromResult`），不在 async 狀態機中取 stack 變數地址，避免原生崩潰。
]

""")]
file class _{
}
