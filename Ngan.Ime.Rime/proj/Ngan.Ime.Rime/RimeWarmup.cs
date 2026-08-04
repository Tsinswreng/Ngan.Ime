using System.Threading;
using Ngan.Ime.Core.Infra.Log;

namespace Ngan.Ime.Rime;

/// <summary>
/// 負責在後台提前拉起 <see cref="RimeSetup"/> 單例，避免首次打開鍵盤時才同步初始化 librime。
/// </summary>
public static class RimeWarmup
{
	static readonly Lock WarmupLock = new();
	static Task<nil>? _WarmupTask;

	/// <summary>
	/// 觸發一次可重入的後台預熱。
	/// 多個入口可重複調用；真正的初始化任務全程只會跑一次。
	/// </summary>
	public static Task<nil> EnsureWarmAsy(CT Ct = default)
	{
		Ct.ThrowIfCancellationRequested();
		lock(WarmupLock){
			if(_WarmupTask is null){
				_WarmupTask = WarmCoreAsy(Ct);
			}
			return _WarmupTask;
		}
	}

	/// <summary>
	/// 把真正的 <see cref="RimeSetup.Inst"/> 構造放到線程池中，避免阻塞調用方的 UI / IME 主線程。
	/// </summary>
	static async Task<nil> WarmCoreAsy(CT Ct)
	{
		try{
			await Task.Run(() => {
				Ct.ThrowIfCancellationRequested();
				_ = RimeSetup.Inst;
			}, Ct);
			AppLog.Info("[AvalimeRime] warmup finished");
		}catch(Exception Ex){
			AppLog.Error(Ex, "[AvalimeRime] warmup failed");
			throw;
		}
		return NIL;
	}
}
