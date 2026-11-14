using Ngan.Ime.Tsf.Ipc;
using Tsinswreng.CsTreeTest;

namespace Ngan.Ime.Test.Domains.Tsf;

[Doc($$"""
被測對象：`{{nameof(TsfKeyRequest)}}` 與 `{{nameof(TsfReply)}}`，即外殼與服務端之間的管道協議。

測四類情形：請求往返、回應往返、畸形輸入一律回 null、版本與方向欄的邊界行為。
協議是兩支程式唯一的接觸面，這裡的每個用例都對應一個「兩邊對不上就整片失效」的點。
""")]
public class TestTsfIpc : Tester{
	[Doc("""
	掛上本測試類的全部用例。
	""")]
	public override void Init(){
		this.AddTestCase(nameof(KeyRequest_Should_RoundTrip), KeyRequest_Should_RoundTrip!);
		this.AddTestCase(nameof(KeyRequest_Should_CarryDirection), KeyRequest_Should_CarryDirection!);
		this.AddTestCase(nameof(KeyRequest_Should_RejectBadDirection), KeyRequest_Should_RejectBadDirection!);
		this.AddTestCase(nameof(KeyRequest_Should_DefaultDirectionToDown), KeyRequest_Should_DefaultDirectionToDown!);
		this.AddTestCase(nameof(HideRequest_Should_RoundTrip), HideRequest_Should_RoundTrip!);
		this.AddTestCase(nameof(Reply_Should_RoundTrip), Reply_Should_RoundTrip!);
		this.AddTestCase(nameof(Reply_Should_SurviveSeparatorsInText), Reply_Should_SurviveSeparatorsInText!);
		this.AddTestCase(nameof(Reply_Should_RejectMalformed), Reply_Should_RejectMalformed!);
		this.AddTestCase(nameof(KeyRequest_Should_RejectMalformed), KeyRequest_Should_RejectMalformed!);
	}

	[Doc("""
	一顆按鍵的請求要能原樣往返：版本、鍵碼、修飾位、座標都不走樣。
	""")]
	public async Task<nil> KeyRequest_Should_RoundTrip(obj? O){
		var T = Assert.IsTrue;
		var Req = new TsfKeyRequest{ VirtualKey = 65, Mods = TsfIpc.ModShift, X = 100, Y = 200 };
		var Back = TsfKeyRequest.Parse(Req.Format());
		T(Back is not null, "請求應可解析");
		T(Back!.VirtualKey == 65, $"鍵碼應為 65，實際 {Back.VirtualKey}");
		T(Back.Mods == TsfIpc.ModShift, "修飾位應保留");
		T(Back.X == 100 && Back.Y == 200, "座標應保留");
		T(Back.Ver == TsfIpc.Version, "版本應為當前版本");
		return NIL;
	}

	[Doc("""
	方向欄要能表達按下與擡起；這是「引擎看得到 release」的前提。
	""")]
	public async Task<nil> KeyRequest_Should_CarryDirection(obj? O){
		var T = Assert.IsTrue;
		var Down = TsfKeyRequest.Parse(new TsfKeyRequest{ VirtualKey = 65 }.Format());
		var Up = TsfKeyRequest.Parse(new TsfKeyRequest{ VirtualKey = 65, IsUp = true }.Format());
		T(Down is not null && !Down.IsUp, "按下應解析為 IsUp=false");
		T(Up is not null && Up.IsUp, "擡起應解析為 IsUp=true");
		return NIL;
	}

	[Doc("""
	方向欄只接受 0 與 1；其他值視為畸形請求，不可靜默當成按下。
	""")]
	public async Task<nil> KeyRequest_Should_RejectBadDirection(obj? O){
		var T = Assert.IsTrue;
		T(TsfKeyRequest.Parse("K|1|65|0|0|0|7") is null, "方向 7 應被拒絕");
		T(TsfKeyRequest.Parse("K|1|65|0|0|0|x") is null, "方向非數字應被拒絕");
		return NIL;
	}

	[Doc("""
	舊版外殼只送五欄，沒有方向欄；此時要當成按下，讓舊外殼仍能打字。
	""")]
	public async Task<nil> KeyRequest_Should_DefaultDirectionToDown(obj? O){
		var T = Assert.IsTrue;
		var Back = TsfKeyRequest.Parse("K|1|65|0|10|20");
		T(Back is not null, "五欄請求應可解析");
		T(!Back!.IsUp, "沒有方向欄時應視為按下");
		return NIL;
	}

	[Doc("""
	收起候選窗是一種請求；它不帶鍵碼，也不能被當成按鍵請求。
	""")]
	public async Task<nil> HideRequest_Should_RoundTrip(obj? O){
		var T = Assert.IsTrue;
		var Req = new TsfKeyRequest{ Kind = ETsfReqKind.Hide };
		var Back = TsfKeyRequest.Parse(Req.Format());
		T(Back is not null, "收起請求應可解析");
		T(Back!.Kind == ETsfReqKind.Hide, "種類應為收起");
		T(Req.Format() == "H|1", $"收起請求應為 H|1，實際 {Req.Format()}");
		return NIL;
	}

	[Doc("""
	回應要能原樣往返：吃掉與否、錯誤碼、高亮、組字、候選、上屏。
	""")]
	public async Task<nil> Reply_Should_RoundTrip(obj? O){
		var T = Assert.IsTrue;
		var Reply = new TsfReply{ Eaten = true, Highlight = 2, Preedit = "ni" };
		Reply.Candidates.Add(new TsfCandidate{ Text = "你", Comment = "ni3" });
		Reply.Candidates.Add(new TsfCandidate{ Text = "泥", Comment = "" });
		Reply.Commits.Add("你好");
		var Back = TsfReply.Parse(Reply.Format());
		T(Back is not null, "回應應可解析");
		T(Back!.Eaten, "吃掉旗標應保留");
		T(Back.Highlight == 2, $"高亮應為 2，實際 {Back.Highlight}");
		T(Back.Preedit == "ni", $"組字應為 ni，實際 {Back.Preedit}");
		T(Back.Candidates.Count == 2, $"候選應為 2 個，實際 {Back.Candidates.Count}");
		T(Back.Candidates[0].Text == "你" && Back.Candidates[0].Comment == "ni3", "第一個候選的內容不對");
		T(Back.Commits.Count == 1 && Back.Commits[0] == "你好", "上屏文字不對");
		return NIL;
	}

	[Doc("""
	候選文字裡出現分隔符（`|`、`~`）時，Base64 要保證不破壞欄位切分。
	""")]
	public async Task<nil> Reply_Should_SurviveSeparatorsInText(obj? O){
		var T = Assert.IsTrue;
		var Reply = new TsfReply{ Eaten = true, Preedit = "a|b" };
		Reply.Candidates.Add(new TsfCandidate{ Text = "x~y|z", Comment = "|" });
		var Back = TsfReply.Parse(Reply.Format());
		T(Back is not null, "回應應可解析");
		T(Back!.Preedit == "a|b", "組字中的分隔符應還原");
		T(Back.Candidates[0].Text == "x~y|z", "候選中的分隔符應還原");
		T(Back.Candidates[0].Comment == "|", "註釋中的分隔符應還原");
		return NIL;
	}

	[Doc("""
	畸形回應一律回 null，呼叫端才能據此放行按鍵而不是拋例外。
	""")]
	public async Task<nil> Reply_Should_RejectMalformed(obj? O){
		var T = Assert.IsTrue;
		T(TsfReply.Parse("") is null, "空行應被拒絕");
		T(TsfReply.Parse("R|1|1") is null, "欄位不足應被拒絕");
		T(TsfReply.Parse("X|1|1|0|0||0|0|") is null, "首欄不是 R 應被拒絕");
		T(TsfReply.Parse("R|1|1|0|0||9|") is null, "候選數與實際欄位不符應被拒絕");
		return NIL;
	}

	[Doc("""
	畸形請求一律回 null；服務端據此回 BadRequest，而不是崩潰。
	""")]
	public async Task<nil> KeyRequest_Should_RejectMalformed(obj? O){
		var T = Assert.IsTrue;
		T(TsfKeyRequest.Parse("") is null, "空行應被拒絕");
		T(TsfKeyRequest.Parse("K|1|65") is null, "欄位不足應被拒絕");
		T(TsfKeyRequest.Parse("K|x|65|0|0|0") is null, "版本非數字應被拒絕");
		T(TsfKeyRequest.Parse("K|1|a|0|0|0") is null, "鍵碼非數字應被拒絕");
		T(TsfKeyRequest.Parse("Z|1|65|0|0|0") is null, "未知首欄應被拒絕");
		return NIL;
	}
}
