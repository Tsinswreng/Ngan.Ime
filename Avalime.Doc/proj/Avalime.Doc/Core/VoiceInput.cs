namespace Avalime.Doc.Core;

using Avalime.Core.Voice;
using Avalime.Backend.Voice.Volcengine;
using Avalime.Windows.Voice;
using Tsinswreng.CsCore;

[Doc($$"""
#H[語音輸入分層][
	- `{{nameof(IAudioInputSource)}}` / `{{nameof(IAudioInputSession)}}`：跨平臺麥克風 PCM 分片來源。
	- `{{nameof(IVoiceRecognitionProvider)}}` / `{{nameof(IVoiceRecognitionSession)}}`：供應商無關的流式識別會話。
	- `{{nameof(VolcengineVoiceRecognitionProvider)}}`：跨 Windows / Android 複用的火山 v3 WebSocket 適配器。
	- `{{nameof(WindowsAudioInputSource)}}`：Windows 專屬 WASAPI 音頻採集入口。
	- `VmVoiceInput`：組合音頻與遠端識別管線，只維護 UI 狀態和文本草稿。
]

#H[Windows 第一版依賴][
	Windows 音頻採集暫時使用 NAudio 的 WASAPI 封裝，以先完成可驗證原型。
	NAudio 的 NativeAOT 相容性列爲後續驗證項；若發布測試不通過，只替換 `WindowsAudioInputSource` / `WindowsAudioInputSession`，跨平臺契約和 `Avalime.Backend` 不變。
]

#H[音頻數據流][
	`{{nameof(IAudioInputSession.ReadChunks)}}` 返回 `IAsyncEnumerable<ReadOnlyMemory<byte>>`：
	- 外層異步序列表示持續產生多個音頻包。
	- 內層只讀內存表示一個保持分包邊界的 PCM 包。
	- 音頻包默認爲 16 kHz、16-bit、單聲道，每包 200ms。
	- 音頻隊列有容量上限；網絡長時間跟不上時終止會話，不靜默丟音頻。
]

#H[火山協議][
	使用 v3 WebSocket 二進制協議：
	- 默認端點 `wss://openspeech.bytedance.com/api/v3/sauc/bigmodel_nostream`。
	- 新控制臺優先使用 `X-Api-Key`；仍保留舊版 AppId / AccessToken 配置。
	- full client request 使用 JSON + GZip。
	- audio-only request 保留每個 PCM 分片邊界，最後發送帶 last-package 標誌的尾包。
	- 服務端響應依據 utterance 的 definite 狀態拆分穩定文本與臨時文本。
	- 解析大端序消息頭、序號、負載長度和錯誤碼；限制單條響應最大尺寸。
]

#H[配置][
	配置位於 `Avalime.Rw.jsonc` 的 `VoiceInput` 節點：
	- `Audio`：採樣率、聲道、位深、分片時長、最大緩衝包數。
	- `Volcengine`：ApiKey、舊版憑據、端點、資源 ID、模型名、連接/尾包超時及結果選項。
	- 密鑰只存本機可寫配置，不寫入代碼和日誌。
]

#H[會話時序][
	1. VM 固定本輪語言與音頻配置快照。
	2. 建立火山 WebSocket 並發送 full client request。
	3. 啓動 Windows 麥克風會話。
	4. 一條後台管線按序發送 PCM 分片，另一條管線消費識別快照。
	5. 用戶停止時先停止採集，再發送最後音頻包並等待最終結果。
	6. 完成、失敗、取消或頁面退出均走統一資源釋放流程。
	錄音按鈕承擔三階段操作：空閒時開始；錄音中點擊正常停止採集、送尾包並等待最終結果；等待最終結果時再次點擊取消。取消會先行觸發會話令牌以中斷等待；Windows 音頻緩衝在空輸入時不補零，避免無聲 PCM 被無限重採樣發送。診斷日誌只記錄 PCM 分片大小與協議幀元資料，不記錄音頻內容或憑據。
	空尾包仍以 gzip 編碼爲合法的空流，不能發送零長壓縮負載，否則火山服務端解壓時會回報 EOF。
]

#H[錯誤契約][
	`{{nameof(VoiceRecognitionException)}}` 使用 `{{nameof(EVoiceRecognitionErrorKind)}}` 統一配置、權限、設備、網絡、鑒權、限額、語言、協議與取消錯誤。
	供應商錯誤碼和 X-Tt-Logid 只作診斷資料，不泄露憑據。
]
""")]
file class _{
}
