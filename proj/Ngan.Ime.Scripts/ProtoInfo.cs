namespace Ngan.Ime.Scripts;

using Tsinswreng.CsCore;
using Tsinswreng.CsSh;

[Doc($$"""
印出管道協議的機器可讀事實：協議版本、管道前綴、請求與回應樣本。

*為何要有這個子命令*：協議升到第 2 版之後，部署煙霧測試與 `pipetest.ps1` 還停在
第 1 版的請求，而且煙霧測試連的是固定的管道名——「驗證」因此變成假的。
這些值在 C\# 裡已經是常量（`TsfIpc.Version`／`PipeNamePrefix`），腳本不該再抄一份；
讓腳本呼叫這個命令，版本與格式就只有一個出處。

*輸出格式*：一行一個 `key=value`，值裡不含空白，PowerShell 直接 `ConvertFrom-StringData` 即可。

#Eg[
	```sh
	dotnet run --project proj/Ngan.Ime.Scripts -- ProtoInfo
	dotnet run --project proj/Ngan.Ime.Scripts -- ProtoInfo -Dir C:\NganImeVm\apps\ngan-server-x86
	```
]
""")]
internal static partial class ProtoInfo{

	[Doc($$"""
	執行：印出協議事實。
	#Prm[{{nameof(Ctx)}}][子命令上下文；本命令不需要倉庫路徑，保留參數只為與其他命令同形。]
	#Prm[{{nameof(Ct)}}][取消令牌；本命令沒有可取消的工作。]
	""")]
	internal static partial Task<nil> Main(ISCtx Ctx, CT Ct);
}
