
using System.Runtime.InteropServices;
using Rime.Api;
using static System.Runtime.InteropServices.NativeMemory;
using static System.Runtime.InteropServices.Marshal;
using System;
using System.Diagnostics;


#region RimeTypes
// using Bool = System.Int32;
// using size_t = nuint;
// using RimeSessionId = nuint;
#endregion RimeTypes

using static Tsinswreng.CsInterop.Ptr;
using Tsinswreng.CsInterop;
using Rime.Api.Types;
using Avalime.Core.Infra;
using Avalime.Core.Infra.Log;
using Tsinswreng.CsCfg;
using Microsoft.Extensions.Logging;
namespace Avalime.Rime;


unsafe public class RimeSetup
	:IDisposable
{
	static readonly Lazy<RimeSetup> _InstLazy = new(() => new RimeSetup(), LazyThreadSafetyMode.ExecutionAndPublication);

	static void LogInfo(str message){ RaiseLog(LogLevel.Information, message); }
	static void LogError(str message){ RaiseLog(LogLevel.Error, message); }

	static void RaiseLog(LogLevel level, string message)
	{
		AppLog.Inst.Log(level, 0, "[AvalimeRime] " + message, null, static (state, _) => state?.ToString() ?? "");
		RimeLogStore.Inst.Append(level, message);
		OnLog?.Invoke(null, new RimeLogEventArgs{
			Level = level,
			Message = message,
		});
	}

	public static RimeSetup Inst{
		get{return _InstLazy.Value;}
	}
	public PtrMgr ptrMgr = new PtrMgr();


	~RimeSetup(){
		Dispose(false);
	}
	protected bool _disposed = false;
	public void Dispose() {
		Dispose(true);
		GC.SuppressFinalize(this);
	}
	protected virtual void Dispose(bool disposing) {
		if(_disposed){
			return;
		}
		if(disposing){
			// dispose managed resources
			ptrMgr.Dispose();
		}
		// dispose unmanaged resources
		//FreeEtNull(&_traits);
		//FreeIfNotNull(_traits);_traits=null;
		//FreeEtNull(ref _traits);//TODO traitsʹ成員 有 byte*、恐未釋放
		_traits = null;
		_disposed = true;
	}

	protected RimeTraits* _traits;

	public RimeTraits* traits{
		get{return _traits;}
	}

	protected RimeSessionId _rimeSessionId;
	public RimeSessionId rimeSessionId{
		get{return _rimeSessionId;}
	}

	public RimeApi apiFn{get;protected set;}

	public RimeSetup(){
		LogInfo("RimeSetup ctor begin");
		_setupRimeApi();
		LogInfo("_setupRimeApi done");
		_setupRimeTraits();
		LogInfo("_setupRimeTraits done");
		_setupRimeSession();
		LogInfo("_setupRimeSession done");
	}

	protected zero _setupRimeApi(){
		//traits = (RimeTraits*)AllocZeroed((nuint)SizeOf<RimeTraits>());
		//var rimeApi = RimeApiFn.rime_get_api();
		var dllPath = KeysCfg.Librime.DllPath.GetFrom(AppCfg.Inst) ?? "";
		if(string.IsNullOrWhiteSpace(dllPath)){
			throw new InvalidOperationException("Librime.DllPath is empty");
		}
		LogInfo("_setupRimeApi: loading " + dllPath);
		var rime_get_api = RimeDllLoader.loadFn_rime_get_api(dllPath);
		LogInfo("_setupRimeApi: loadFn_rime_get_api done");
		var rimeApi = rime_get_api();
		LogInfo("_setupRimeApi: rime_get_api invoked");
		apiFn = *rimeApi;
		return 0;
	}


	protected zero _setupRimeTraits(){
		LogInfo("_setupRimeTraits: begin");
		var sharedDataDir = KeysCfg.Librime.RimeTraits.shared_data_dir.GetFrom(AppCfg.Inst) ?? "";
		var userDataDir = KeysCfg.Librime.RimeTraits.user_data_dir.GetFrom(AppCfg.Inst) ?? "";
		if(string.IsNullOrWhiteSpace(userDataDir)){
			throw new InvalidOperationException("Librime.RimeTraits.user_data_dir is empty");
		}
		var distributionName = KeysCfg.Librime.RimeTraits.distribution_name.GetFrom(AppCfg.Inst) ?? "";
		var distributionCodeName = KeysCfg.Librime.RimeTraits.distribution_code_name.GetFrom(AppCfg.Inst) ?? "";
		var distributionVersion = KeysCfg.Librime.RimeTraits.distribution_version.GetFrom(AppCfg.Inst) ?? "";
		var appName = KeysCfg.Librime.RimeTraits.app_name.GetFrom(AppCfg.Inst) ?? "rime.avalime";
		var minLogLevel = KeysCfg.Librime.RimeTraits.min_log_level.GetFrom(AppCfg.Inst);
		var logDir = KeysCfg.Librime.RimeTraits.log_dir.GetFrom(AppCfg.Inst) ?? "";
		var prebuiltDataDir = KeysCfg.Librime.RimeTraits.prebuilt_data_dir.GetFrom(AppCfg.Inst) ?? "";
		var stagingDir = KeysCfg.Librime.RimeTraits.staging_dir.GetFrom(AppCfg.Inst) ?? "";
		_traits = New<RimeTraits>();
		traits->data_size = RimeUtil.DataSize<RimeTraits>();
		traits->shared_data_dir = ptrMgr.Str(sharedDataDir);
		traits->user_data_dir = ptrMgr.Str(userDataDir);
		traits->distribution_name = ptrMgr.Str(distributionName);
		traits->distribution_code_name = ptrMgr.Str(distributionCodeName);
		traits->distribution_version = ptrMgr.Str(distributionVersion);
		traits->app_name = ptrMgr.Str(appName);
		traits->min_log_level = minLogLevel;
		traits->log_dir = ptrMgr.Str(logDir);
		traits->prebuilt_data_dir = ptrMgr.Str(prebuiltDataDir);
		traits->staging_dir = ptrMgr.Str(stagingDir);
		LogInfo("_setupRimeTraits: sharedDataDir=" + sharedDataDir);
		LogInfo("_setupRimeTraits: userDataDir=" + userDataDir);
		LogInfo("_setupRimeTraits: distributionName=" + distributionName);
		LogInfo("_setupRimeTraits: distributionCodeName=" + distributionCodeName);
		LogInfo("_setupRimeTraits: distributionVersion=" + distributionVersion);
		LogInfo("_setupRimeTraits: appName=" + appName);
		LogInfo("_setupRimeTraits: minLogLevel=" + minLogLevel);
		LogInfo("_setupRimeTraits: logDir=" + logDir);
		LogInfo("_setupRimeTraits: prebuiltDataDir=" + prebuiltDataDir);
		LogInfo("_setupRimeTraits: stagingDir=" + stagingDir);
		return 0;
	}

	protected zero _freeRimeTraits(){
		//TODO
		return 0;
	}

	public static str? S(u8* cStr){
		return ToolCStr.ToCsStr(cStr);
	}

	/// <summary>option 改變時觸發。參數: option_name, is_enabled</summary>
	public static event Action<string, bool>? OnOptionChanged;
	public static event EventHandler<RimeLogEventArgs>? OnLog;

	/// <summary>
	/// 零分配字節比較，避免逆向 P/Invoke 中觸發 GC 導致 Mono !ji->async 斷言崩潰
	/// </summary>
	static bool _bytesEq(byte* p, str s){
		for(i32 i = 0; i < s.Length; i++){
			if(p[i] != (byte)s[i]) return false;
		}
		return p[s.Length] == 0; // null terminator
	}

	public static void on_message(
		void* context_object
		,RimeSessionId session_id
		,byte* message_type
		,byte* message_value
	){
		// 空指針檢查
		if(message_type == null || message_value == null) return;

		var messageType = S(message_type);
		var messageValue = S(message_value);
		if(!string.IsNullOrWhiteSpace(messageType) || !string.IsNullOrWhiteSpace(messageValue)){
			RaiseLog(LogLevel.Information, $"notify: {messageType} {messageValue}".Trim());
		}

		// 快速路徑：只處理 "option" 類型
		if(!_bytesEq(message_type, "option")) return;

		// 零分配檢測 ascii_mode 切換
		if(_bytesEq(message_value, "ascii_mode"))
			OnOptionChanged?.Invoke("ascii_mode", true);
		else if(_bytesEq(message_value, "!ascii_mode"))
			OnOptionChanged?.Invoke("ascii_mode", false);
		else if(_bytesEq(message_value, "simplification"))
			OnOptionChanged?.Invoke("simplification", true);
		else if(_bytesEq(message_value, "!simplification"))
			OnOptionChanged?.Invoke("simplification", false);
	}

	public RimeNotificationHandler ManagedRimeNotificationHandler = on_message;

	protected zero _setupRimeSession(){
		LogInfo("_setupRimeSession: apiFn.setup");
		apiFn.setup(traits);
		ManagedRimeNotificationHandler = on_message;
		LogInfo("_setupRimeSession: set_notification_handlerManaged");
		apiFn.set_notification_handlerManaged(
			ManagedRimeNotificationHandler
			,null
		);
		LogInfo("_setupRimeSession: initialize");
		apiFn.initialize(null);
		var full_check = RimeUtil.True;
		LogInfo("_setupRimeSession: start_maintenance");
		if(apiFn.start_maintenance(full_check) != RimeUtil.False){
			LogInfo("_setupRimeSession: join_maintenance_thread");
			apiFn.join_maintenance_thread();
		}
		LogInfo("_setupRimeSession: create_session");
		_rimeSessionId = apiFn.create_session();
		LogInfo("_setupRimeSession: session=" + _rimeSessionId);
		return 0;
	}

	//TODO rime.destroy_session(rimeSessionId); rime.finalize();


}
