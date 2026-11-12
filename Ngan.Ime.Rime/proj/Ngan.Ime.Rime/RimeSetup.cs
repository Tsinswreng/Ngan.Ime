using Rime.Api;
using Rime.Api.Types;
using Tsinswreng.CsCore;
using Tsinswreng.CsInterop;
using Tsinswreng.CsErr;
using Microsoft.Extensions.Logging;

namespace Ngan.Ime.Rime;

[Doc($$"""
librime 的初始化與生命週期，一個行程只有一份。

*為什麼要單例*：librime 的 `setup/initialize` 是行程級狀態，重複呼叫會讓全域字典與日誌錯亂；
故 {{nameof(Inst)}} 是唯一入口，DI 註冊的也是它（見 `DiRime`）。

*初始化順序*（順序不能換）：
1. 讀配置取動態庫路徑並 `rime_get_api`；
2. 填 `RimeTraits`（可選字符串必須保持 null 而不是空串，否則會覆蓋 librime 的默認值）；
3. `setup` → `initialize` → `start_maintenance` → `join_maintenance_thread`；
4. `create_session`。
其中 3 會阻塞（部署詞典），故呼叫方必須在背景線程觸發（見 `RimeWarmup`）。

*失敗後可重試*：{{nameof(Reset)}} 會丟棄半初始化的實例；不這樣做的話 `Lazy<T>` 會永久快取例外，
使用者改好配置也只能重啟行程。

*資源*：`RimeTraits` 與所有 C 字符串都交給 {{nameof(Ptrs)}} 托管，`Dispose` 時統一釋放；
session 與 librime 全域狀態則由 {{nameof(Dispose)}} 依序 `destroy_session`／`finalize`。
`Ptrs.New<T>()` 不進托管表，故這裡改用 `AddNew<T>()`（舊版用 New 造成 `RimeTraits` 泄漏）。
""")]
unsafe public class RimeSetup : IDisposable {

	[Doc("單例本體；null 表示尚未建立或已被 Reset。")]
	static RimeSetup? InstField;

	[Doc("單例建立／重置的鎖。")]
	static readonly Lock InstLock = new();

	[Doc("全域唯一實例；第一次取用時才建立（建構子會做重活，呼叫方必須在背景線程）。")]
	public static RimeSetup Inst {
		get {
			lock(InstLock){
				return InstField ??= new RimeSetup();
			}
		}
	}

	[Doc("丟棄目前實例（若有）並釋放其資源；下一次取 Inst 會重新初始化。供初始化失敗後重試。")]
	public static void Reset(){
		lock(InstLock){
			InstField?.Dispose();
			InstField = null;
		}
	}

	[Doc("原生字符串與 traits 的所有者；Dispose 時統一釋放。")]
	public Ptrs ptrs = new();

	[Doc("librime 的函數表；{{nameof(Dispose)}} 後不再有效。")]
	public RimeApi apiFn { get; private set; }

	[Doc("唯一的輸入會話；0 表示尚未建立或已銷毀。")]
	RimeSessionId SessionIdField;

	[Doc("當前會話 id；0 表示無效。")]
	public RimeSessionId rimeSessionId => SessionIdField;

	[Doc("traits 的原生記憶體；由 {{nameof(ptrs)}} 托管。")]
	RimeTraits* TraitsField;

	[Doc("traits 指針；只供 librime 初始化使用。")]
	public RimeTraits* traits => TraitsField;

	[Doc("是否已釋放；重複 Dispose 直接返回。")]
	bool Disposed;

	[Doc("Rime 的 option 變更通知（ascii_mode／simplification）；回調在原生線程觸發。")]
	public static event Action<str, bool>? OnOptionChanged;

	[Doc("Rime 原始日誌；界面用它做「引擎日誌」頁。")]
	public static event EventHandler<EngineLogEntryEventArgs>? OnLog;

	[Doc("建立並完成一次完整初始化；失敗時把已配置的資源釋放掉再拋。")]
	public RimeSetup(){
		LogInfo("開始初始化 librime");
		try{
			SetupRimeApi();
			SetupRimeTraits();
			SetupRimeSession();
		}catch{
			// 半初始化狀態必須清乾淨，否則下一次重試會撞到已佔用的全域狀態。
			Dispose();
			throw;
		}
		LogInfo("librime 初始化完成");
	}

	[Doc("寫一條 Info 級引擎日誌，並同步到引擎日誌流與 {{nameof(OnLog)}}。")]
	static void LogInfo(str Message){
		RaiseLog(LogLevel.Information, Message);
	}

	[Doc("寫一條引擎日誌：AppLog、引擎日誌緩衝、{{nameof(OnLog)}} 三路同步。")]
	static void RaiseLog(LogLevel Level, str Message){
		AppLog.Inst.Log(Level, 0, "[Rime] " + Message, null, static (S, _) => S?.ToString() ?? "");
		RimeLogStore.Inst.Append(Level, Message);
		OnLog?.Invoke(null, new EngineLogEntryEventArgs{
			Level = Level,
			Message = Message,
			FormattedLine = $"[{Level}] {Message}",
		});
	}

	[Doc("載入動態庫並取得函數表；路徑空或載入失敗都拋帶鍵的異常。")]
	void SetupRimeApi(){
		var DllPath = AppCfg.Inst.Get(KeysAppCfg.Librime.DllPath) ?? "";
		if(string.IsNullOrWhiteSpace(DllPath)){
			throw KeysErr.Rime.DllPathEmpty.ToErr();
		}
		LogInfo("載入 " + DllPath);
		try{
			var GetApi = RimeDllLoader.loadFn_rime_get_api(DllPath);
			if(GetApi is null){
				throw KeysErr.Rime.EngineLoadErr__.ToErr(DllPath);
			}
			var Api = GetApi();
			if(Api is null){
				throw KeysErr.Rime.EngineLoadErr__.ToErr(DllPath);
			}
			apiFn = *Api;
		}catch(TypedErr){
			throw;
		}catch(Exception Ex){
			AppLog.Error(Ex, "[Rime] 動態庫載入失敗：" + DllPath);
			throw KeysErr.Rime.EngineLoadErr__.ToErr(DllPath).AddDebugArgs(Ex.Message);
		}
	}

	[Doc("讀配置填 RimeTraits；可選字符串為空時保持 null，避免覆蓋 librime 的默認值。")]
	void SetupRimeTraits(){
		var SharedDataDir = AppCfg.Inst.Get(KeysAppCfg.Librime.RimeTraits.shared_data_dir) ?? "";
		var UserDataDir = AppCfg.Inst.Get(KeysAppCfg.Librime.RimeTraits.user_data_dir) ?? "";
		if(string.IsNullOrWhiteSpace(UserDataDir)){
			throw KeysErr.Rime.UserDataDirEmpty.ToErr();
		}
		var DistributionName = AppCfg.Inst.Get(KeysAppCfg.Librime.RimeTraits.distribution_name) ?? "";
		var DistributionCodeName = AppCfg.Inst.Get(KeysAppCfg.Librime.RimeTraits.distribution_code_name) ?? "";
		var DistributionVersion = AppCfg.Inst.Get(KeysAppCfg.Librime.RimeTraits.distribution_version) ?? "";
		var AppName = AppCfg.Inst.Get(KeysAppCfg.Librime.RimeTraits.app_name) ?? "rime.ngan.ime";
		var MinLogLevel = AppCfg.Inst.Get(KeysAppCfg.Librime.RimeTraits.min_log_level);
		var LogDir = AppCfg.Inst.Get(KeysAppCfg.Librime.RimeTraits.log_dir);
		var PrebuiltDataDir = AppCfg.Inst.Get(KeysAppCfg.Librime.RimeTraits.prebuilt_data_dir) ?? "";
		var StagingDir = AppCfg.Inst.Get(KeysAppCfg.Librime.RimeTraits.staging_dir) ?? "";

		// AddNew 會登記到托管表，Dispose 時連同 traits 一起釋放。
		TraitsField = ptrs.AddNew<RimeTraits>();
		traits->data_size = RimeUtil.DataSize<RimeTraits>();
		// 可選字符串字段爲空時必須保持 null。
		// 若傳空串指針，librime 的 PROVIDED(...) 會把它當成「已顯式提供」，反而覆蓋內部默認值；
		// 尤其 shared_data_dir="" 會直接讓部署／讀 schema 失效。
		traits->shared_data_dir = NullIfBlank(SharedDataDir);
		traits->user_data_dir = ptrs.CStr(UserDataDir);
		traits->distribution_name = NullIfBlank(DistributionName);
		traits->distribution_code_name = NullIfBlank(DistributionCodeName);
		traits->distribution_version = NullIfBlank(DistributionVersion);
		traits->app_name = ptrs.CStr(AppName);
		traits->min_log_level = MinLogLevel;
		// librime 約定：null=臨時目錄，空串=只寫 stderr。這裡不能把空串吞成 null。
		traits->log_dir = LogDir is null ? null : ptrs.CStr(LogDir);
		traits->prebuilt_data_dir = NullIfBlank(PrebuiltDataDir);
		traits->staging_dir = NullIfBlank(StagingDir);

		LogInfo($"traits: shared={SharedDataDir} user={UserDataDir} app={AppName} log_dir={LogDir ?? "(null)"}");
	}

	[Doc("對 librime 的可選 `const char*` 字段：空白就回 null，否則回托管 C 字符串。")]
	u8* NullIfBlank(str? Value){
		if(string.IsNullOrWhiteSpace(Value)){
			return null;
		}
		return ptrs.CStr(Value);
	}

	[Doc("setup → initialize → 維護 → 建立 session；其中的阻塞由呼叫方在背景線程承擔。")]
	void SetupRimeSession(){
		LogInfo("rime setup");
		apiFn.setup(traits);
		LogInfo("rime initialize");
		apiFn.initialize(null);
		LogInfo("rime start_maintenance");
		if(apiFn.start_maintenance(RimeUtil.True) != RimeUtil.False){
			LogInfo("rime join_maintenance_thread（部署詞典，可能較久）");
			apiFn.join_maintenance_thread();
		}
		SessionIdField = apiFn.create_session();
		LogInfo("rime session = " + SessionIdField);
		if(SessionIdField == 0){
			throw KeysErr.Rime.SessionInvalid.ToErr();
		}
	}

	[Doc("原生通知回調的托管端；只處理 option 變更，其餘記日誌。")]
	public static void OnMessage(
		void* ContextObject
		,RimeSessionId SessionId
		,u8* MessageType
		,u8* MessageValue
	){
		// 空指針檢查：librime 在部署階段允許 message_value 為 null。
		if(MessageType is null || MessageValue is null){
			return;
		}
		// 快速路徑：option 變更不做任何托管分配（逆向 P/Invoke 中分配會誘發 GC，風險見 Doc 註釋）。
		if(BytesEq(MessageType, "option")){
			if(BytesEq(MessageValue, "ascii_mode")){
				OnOptionChanged?.Invoke("ascii_mode", true);
			}else if(BytesEq(MessageValue, "!ascii_mode")){
				OnOptionChanged?.Invoke("ascii_mode", false);
			}else if(BytesEq(MessageValue, "simplification")){
				OnOptionChanged?.Invoke("simplification", true);
			}else if(BytesEq(MessageValue, "!simplification")){
				OnOptionChanged?.Invoke("simplification", false);
			}
			return;
		}
		// 非熱路徑（部署、schema 切換等）才做字符串轉換與日誌。
		RaiseLog(LogLevel.Information, $"{ToolCStr.ToCsStr(MessageType)}: {ToolCStr.ToCsStr(MessageValue)}");
	}

	[Doc("托管通知處理器實例；必須保持存活，否則原生端呼叫到已回收的委託。")]
	public RimeNotificationHandler ManagedRimeNotificationHandler = OnMessage;

	[Doc(@"零分配字節比較：只有 p 恰好等於 s 且以 \0 結尾才回 true。
	舊版在 s 較長時會讀過終止符之後，這裡先遇 \0 就返回 false。")]
	static bool BytesEq(u8* P, str S){
		for(i32 I = 0; I < S.Length; I++){
			if(P[I] == 0){
				return false;
			}
			if(P[I] != (u8)S[I]){
				return false;
			}
		}
		return P[S.Length] == 0;
	}

	[Doc("銷毀 session、finalize librime，並釋放所有托管原生記憶體；可重複呼叫。")]
	public void Dispose(){
		if(Disposed){
			return;
		}
		Disposed = true;
		if(SessionIdField != 0){
			try{
				apiFn.destroy_session(SessionIdField);
			}catch(Exception Ex){
				AppLog.Error(Ex, "[Rime] destroy_session 失敗");
			}
			SessionIdField = 0;
		}
		try{
			apiFn.finalize();
		}catch(Exception Ex){
			AppLog.Error(Ex, "[Rime] finalize 失敗");
		}
		TraitsField = null;
		ptrs.Dispose();
		GC.SuppressFinalize(this);
	}
}
