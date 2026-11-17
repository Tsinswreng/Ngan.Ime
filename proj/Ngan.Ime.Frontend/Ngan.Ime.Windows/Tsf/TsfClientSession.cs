namespace Ngan.Ime.Windows.Tsf;

using Ngan.Ime.Core.Ime;
using Ngan.Ime.Tsf.Ipc;

[Doc($$"""
一條管道連線（＝一個應用的外殼行程）自己的狀態。

*為何一定要按連線分*：`Seq` 是每個外殼行程各自從 1 遞增的（`NativeTsf.Pipe.cs` 的
`KeySeqCounter`），而服務端的 `LastPosSeq`／`PendingSeq` 先前是**全域唯一**的一份。
於是「序號最小的那個行程」送來的每一條位置都被當成過期丟掉，面板永遠不出現——
2026-11-17 實測：VSCode（外殼從前一晚就在，序號 3629）正常，QQ（新行程，序號 64）
的 `[Pos]` 全部 `stale seq=64 last=1771`，內容一直 `pending`，使用者看到的是「能盲打、沒候選框」。

*Weasel 為何沒這個問題*：它的 IPC 是同步交易，`UpdateInputPosition` 送到就套用，
既沒有序號，也沒有「丟棄遲到者」這種跨行程判斷。序號是我們自己加的，
所以「每條連線一份」才是它原本的語義，而不是把全域序號改成全域更大。

*共享的部分*：引擎與浮窗仍然各只有一個（照 Weasel），所以 `SvcTsfPipeServer.Anchor`
（給模式提示用）會在任一條連線套用位置時跟着更新，代表「最後一個在打字的客戶端」。
""")]
public sealed class TsfClientSession{

	[Doc("對端行程 id；只用在日誌，取不到時為 0。")]
	public u32 ClientPid{get;set;}

	[Doc("待顯示的引擎回應：等這條連線推來插入點位置才放上浮窗。")]
	public TsfReply? PendingReply{get;set;}

	[Doc("待顯示內容所屬的按鍵序號；位置更早於它就代表「這是上一顆鍵的位置」。")]
	public i32 PendingSeq{get;set;}

	[Doc("這條連線已套用過的最新位置序號；只和自己比，不跨行程比較。")]
	public i32 LastPosSeq{get;set;}

	[Doc("這條連線最近一次量到的插入點；位置訊息沒帶上緣時用它兜底。")]
	public CaretAnchor Anchor{get;set;} = new();

	[Doc("待顯示內容的互斥；按鍵執行緒與位置更新執行緒都會碰它。")]
	public object Gate{get;} = new();

	[Doc($$"""
	建立一條連線的狀態。
	#Prm[{{nameof(ClientPid)}}][對端行程 id；只用在日誌。]
	""")]
	public TsfClientSession(u32 ClientPid){
		this.ClientPid = ClientPid;
	}
}
