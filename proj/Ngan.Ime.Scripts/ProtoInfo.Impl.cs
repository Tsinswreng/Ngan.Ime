namespace Ngan.Ime.Scripts;

using Ngan.Ime.Tsf.Ipc;
using Tsinswreng.CsCore;

[Doc($$"""
{{nameof(ProtoInfo)}} 的實現：把 `TsfIpc` 的常量與格式器印成 `key=value`。
這裡刻意只用 `TsfKeyRequest`／`TsfReply` 自己的 `Format`：樣本是「服務端真正會收到的字串」，
而不是手寫的模板。協議改了，這裡的輸出自己跟著改，腳本不必動。
""")]
internal static partial class ProtoInfo{

	[Doc($$"""
	可寫管道名參數的旗標名稱。
	""")]
	const str DirFlag = "-dir";

	internal static partial Task<nil> Main(ISCtx Ctx, CT Ct){
		Console.WriteLine("version=" + TsfIpc.Version);
		Console.WriteLine("pipe_prefix=" + TsfIpc.PipeNamePrefix);
		Console.WriteLine("max_line_length=" + TsfIpc.MaxLineLength);

		// 樣本：一顆按下的鍵（'C'），座標與修飾位都給非零值，讓格式的欄位數一眼可數。
		Console.WriteLine("key_sample=" + new TsfKeyRequest{
			Kind = ETsfReqKind.Key,
			VirtualKey = 0x43,
			Mods = 0,
			X = 100,
			Y = 100,
		}.Format());
		Console.WriteLine("key_up_sample=" + new TsfKeyRequest{
			Kind = ETsfReqKind.Key,
			VirtualKey = 0x43,
			IsUp = true,
		}.Format());
		Console.WriteLine("echo=" + new TsfKeyRequest{ Kind = ETsfReqKind.Echo }.Format());
		Console.WriteLine("hide=" + new TsfKeyRequest{ Kind = ETsfReqKind.Hide }.Format());

		// 回應樣本：讓腳本知道回應的欄位順序，不必再從註釋裡猜。
		var reply = new TsfReply{ Eaten = true, Preedit = "a" };
		reply.Candidates.Add(new TsfCandidate{ Text = "你", Comment = "ni3" });
		Console.WriteLine("reply_sample=" + reply.Format());

		var Dir = ParseDirArg();
		if(Dir.Length > 0){
			Console.WriteLine("pipe=" + TsfIpc.PipeNameFor(Dir));
		}
		return Task.FromResult(NIL);
	}

	[Doc($$"""
	讀命令列的 `-Dir <目錄>`；沒有給就回空串。
	#Rtn[目錄路徑；未指定時為空串。]
	""")]
	static str ParseDirArg(){
		var args = Environment.GetCommandLineArgs();
		for(var index = 0; index < args.Length - 1; index++){
			if(args[index].Equals(DirFlag, StringComparison.OrdinalIgnoreCase)){
				return args[index + 1];
			}
		}
		return "";
	}
}
