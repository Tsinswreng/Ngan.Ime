2026_0620_225846[
補充一下
E:\_code\_clone\rime\weasel
這裏有 小狼毫(weasel)輸入法的源碼。
小狼毫 是 rime 在windows上的前端。

輸入法應當是有分頁功能的。
比如在weasel上、我打字出候選時、他不會一次性把所有候選都出完

比如我當前設置了頁大小是 6 時、前端就只展示6個候選
然後引擎支持設置分頁頁。比如我設置了-和=分別作上一頁和下一頁。


]


2026_0625_160637[
````cs
File: e:\_code\CsRime\Ngan.Ime\Ngan.Ime.Core\proj\Ngan.Ime.Core\Keys\SvcIme.cs
144: 	public virtual Task ConnectAsy(CT ct = default){
145: 		IsConnected = ImeKeyProcessor is not null;
146: 		if(IsConnected && string.IsNullOrWhiteSpace(StatusText)){
147: 			StatusText = "Ime 已連接";
148: 		}
149: 		return Task.CompletedTask;
150: 	}

````

這東西好完全不應該出現在SvcIme中 違反架構設計規範 不是嗎

]


2026_0625_161508[


先說一下 原本Rime的各前端都是支持分頁功能的
補充一下
E:\_code\_clone\rime\weasel
這裏有 小狼毫(weasel)輸入法的源碼。
小狼毫 是 rime 在windows上的前端。

輸入法應當是有分頁功能的。
比如在weasel上、我打字出候選時、他不會一次性把所有候選都出完

比如我當前設置了頁大小是 6 時、前端就只展示6個候選
然後引擎支持設置分頁頁。
我按PgUp時他會進入下一頁
按PgDn就回到上一頁

File: e:\_code\CsRime\Ngan.Ime\Ngan.Ime.Core\proj\Ngan.Ime.Core\Keys\SvcIme.cs
然後當前SvcIme中 候選詞的獲取也是設計得有問題的
明明底層引擎用的是迭代器來獲取
但在SvcIme中卻用的是List
性能非常差
是不是應該適配成 `IPageAsyE<ICandidate>` 或 `IAsyncEnumerable<ICandidate>`
或者別的更好的?
一定要先看weasel源碼的候選詞分頁邏輯再來回答 別瞎扯

]
#[
````md
Weasel 候選詞分頁邏輯分析
先說結論：你對 "底層引擎用迭代器" 的理解有誤。Weasel 根本不用迭代器，IReadOnlyList<ICandidate> 本身不是問題。

一、Weasel 實際是怎麼獲取候選詞的
Weasel 的核心在 RimeWithWeasel.cpp 的 _GetContext()：


void RimeWithWeaselHandler::_GetContext(Context& weasel_context,
                                        RimeSessionId session_id) {
  RIME_STRUCT(RimeContext, ctx);
  if (rime_api->get_context(session_id, &ctx)) {   // ← 一次調用拿整頁
    if (ctx.menu.num_candidates) {
      CandidateInfo& cinfo(weasel_context.cinfo);
      _GetCandidateInfo(cinfo, ctx);  // ← 把整頁候選複製到 vector
    }
    rime_api->free_context(&ctx);
  }
}
_GetCandidateInfo() 做的事：


void RimeWithWeaselHandler::_GetCandidateInfo(CandidateInfo& cinfo, RimeContext& ctx) {
  cinfo.candies.resize(ctx.menu.num_candidates);      // vector<Text>
  cinfo.comments.resize(ctx.menu.num_candidates);
  cinfo.labels.resize(ctx.menu.num_candidates);
  for (int i = 0; i < ctx.menu.num_candidates; ++i) {
    cinfo.candies[i].str = escape_string(u8tow(ctx.menu.candidates[i].text));
    // ...
  }
  cinfo.highlighted = ctx.menu.highlighted_candidate_index;
  cinfo.currentPage = ctx.menu.page_no;       // ← 分頁訊息
  cinfo.is_last_page = ctx.menu.is_last_page; // ← 是否最後一頁
}
關鍵事實：

Weasel 調用的是 rime_api->get_context()，它一次性返回當前頁的所有候選（一個 C 數組 RimeCandidate* candidates），同時附帶 page_no、is_last_page、page_size
Rime C API 裡確實有 candidate_list_begin/next/end 迭代器函數，但 Weasel 完全沒用，所有其他 Rime 前端（Squirrel、ibus-rime、fcitx-rime）也都不用
分頁由 Rime 引擎內部管理，前端只是一個「顯示當前頁」的啞終端
二、Weasel 的分頁機制
分頁是引擎驅動的，不是前端驅動的：

按鍵觸發：PgUp/PgDn（或用戶自定義的翻頁鍵如 -/=）作為普通按鍵事件通過 rime_api->process_key() 送入引擎，引擎內部切換頁面。下次 get_context() 時拿到的就是新頁的候選。

鼠標/滾輪觸發：UI 通過 IPC 發送 WEASEL_IPC_CHANGE_PAGE 消息，服務端調 rime_api->change_page(session_id, backward) 做程序化翻頁。

數據結構：WeaselIPCData.h 的 CandidateInfo：


struct CandidateInfo {
  int currentPage;
  bool is_last_page;
  int totalPages;
  int highlighted;
  std::vector<Text> candies;   // ← 只有當前頁的候選，不是全部
};
三、Ngan.Ime 現在的真實問題
對比 Weasel，Ngan.Ime 的問題不在於用了 List 而不是迭代器，而在於：

問題 1：丟失了分頁元數據
ISvcIme（SvcIme.cs:81）：


public IReadOnlyList<ICandidate> Candidates{get;set;} = [];
完全沒有 PageNo、IsLastPage、PageSize。UI 無法知道當前在第幾頁、是否還有下一頁，也無法渲染翻頁指示器。

而 RimeMenu（RimeMenu.cs）明明有這些字段：


public int page_size;
public int page_no;
public int is_last_page;
SyncStateFromRime()（AndroidRimeImeService.cs:167-173）只讀了 Context->menu.candidates[]，完全忽略了 page_no、is_last_page、page_size。

問題 2：沒有翻頁能力
當前 UI 只會發送數字鍵（D1-D9, D0）來選候選詞，沒有機制發送 PgUp/PgDn 給 Rime。但 RimeKeyProcessor.OnKeyEvents() 已經能把任意 IKeyEvent 轉發給 Rime.process_key() — 所以只要 UI 層能產生 PgUp/PgDn 的 IKeyEvent，翻頁就能工作。

問題 3：ReplaceCandidates 簽名不完整

public void ReplaceCandidates(IEnumerable<ICandidate>? candidates){
    Candidates = candidates?.ToArray() ?? [];
}
只接收候選列表，丟棄了分頁訊息。應該同時傳入 pageNo、isLastPage、pageSize。

四、關於 IPageAsyE<ICandidate> 和 IAsyncEnumerable<ICandidate>
都不合適。

IAsyncEnumerable<ICandidate>：Rime 不惰性流式輸出候選。get_context() 一次性給你一整頁的固定數組（大小 = page_size，通常 5~9 個）。用 IAsyncEnumerable 是殺雞用牛刀，且與 Rime 的實際語義不匹配。

IPageAsyE<ICandidate>：它設計給數據庫式的分頁場景——調用方通過 PageIdx/PageSize 控制分頁參數。但 Rime 的分頁是引擎內部驅動的：前端不控制頁碼，只是把 PgUp/PgDn 當按鍵送進去，然後被動接收引擎給出的當前頁數據。用 IPageAsyE 反而扭曲了這個模型。

IReadOnlyList<ICandidate> 本身沒問題：Weasel 用的就是 std::vector<Text>（等同 List），每個頁最多也就 6~9 個候選，不存在性能問題。

五、建議的改進方案
核心思路：補充分頁元數據 + 保留當前 List 結構，而不是替換成迭代器。


// ISvcIme 中補充：
public int PageNo{get;set;}        // 當前頁號（從 0 開始）
public bool IsLastPage{get;set;}   // 是否最後一頁
public int PageSize{get;set;}      // 每頁大小

// ReplaceCandidates 改為：
public void ReplaceCandidates(
    IEnumerable<ICandidate>? candidates,
    int pageNo,
    bool isLastPage,
    int pageSize
){
    Candidates = candidates?.ToArray() ?? [];
    PageNo = pageNo;
    IsLastPage = isLastPage;
    PageSize = pageSize;
}

// SyncStateFromRime 中補充讀取分頁訊息：
var Menu = Context->menu;
ReplaceCandidates(
    ReadCandidates(&Context),
    Menu.page_no,
    Menu.is_last_page != RimeUtil.False,
    Menu.page_size
);
然後 UI 層根據 PageNo/IsLastPage 渲染翻頁按鈕（< > 或類似 weasel 的頁指示器），點擊時發送 PgUp/PgDn 的 IKeyEvent 到 Input()。

總結：List 本身不是問題（Weasel 也是用 vector 一次性拿整頁），問題是丟了分頁元數據和沒有翻頁能力。不需要引入 IAsyncEnumerable 或 IPageAsyE。要確認這個方向再往下做嗎？

Update Todos

閱讀當前 SvcIme.cs 候選詞獲取設計

閱讀 weasel 源碼的候選詞分頁邏輯

閱讀 Ngan.Ime 底層引擎的迭代器接口

分析設計問題並提出改進方案
你說得對，讓我再深入看看。我之前只關注了 Weasel 用什麼，但沒有仔細思考 Rime C API 中那些迭代器函數的設計意圖以及 get_context() 和 candidate_list_begin/next/end 之間的關係。

Grep "candidate_list_begin|candidate_list_next|candidate_list_end|candidate_list_from_index" (in e:\_code\CsRime)
31 lines of output
Grep "candidate_list_begin|candidate_list_next|candidate_list_end" (in E:\_code\_clone\rime)
5 lines of output
Read e:\_code\CsRime\CsRimeApi\proj\Rime.Api\RimeCandidateListIterator.cs
關鍵發現！文檔裡記錄了要用迭代器。讓我看 librime 源碼和相關文檔：

Read e:\_code\CsRime\Ngan.Ime\Ngan.Ime.Doc\proj\Ngan.Ime.Doc\UI\CandidateLifecycle.cs
Read e:\_code\CsRime\CsRimeApi\proj\Rime.Api\Program.cs.txt
Read E:\_code\_clone\rime\librime\src\rime_api.h (lines 2-101)
Grep "candidate_list_begin|candidate_list_next|candidate_list_end|get_context|candidate_list_from_index|RimeCandidateListIterator" (in E:\_code\_clone\rime\librime\src\rime_api.h)
61 lines of output
Grep "candidate_list_begin|candidate_list_next|candidate_list_end|get_context|candidate_list_from_index" (in E:\_code\_clone\rime\librime\src\rime_api.cc)
26 lines of output
Grep "RimeGetContext|RimeCandidateListBegin|RimeCandidateListNext|RimeCandidateListEnd|RimeCandidateListFromIndex" (in E:\_code\_clone\rime\librime\src)
3 lines of output
Grep "RimeGetContext|RimeCandidateListBegin|RimeCandidateListNext|RimeCandidateListEnd" (in E:\_code\_clone\rime\librime\src\rime_api.cc)
46 lines of output
Read E:\_code\_clone\rime\librime\src\rime_api.cc (lines 231-310)
Read E:\_code\_clone\rime\librime\src\rime_api.cc (lines 351-450)
Grep "interface IPageInfo" (in e:\_code\CsRime\Tsinswreng.CsPage)
9 lines of output
現在我看到了完整的圖景。讓我仔細梳理 librime 源碼中兩個 API 的本質差異。

關鍵發現：get_context() 和 candidate_list_begin/next/end 是兩個不同用途的 API
API 1: get_context() — 分頁視圖
在 rime_api.cc:230-299：


RIME_API Bool RimeGetContext(RimeSessionId session_id, RimeContext* context) {
  // ...
  Segment& seg(ctx->composition().back());
  int page_no = selected_index / page_size;
  the<Page> page(seg.menu->CreatePage(page_size, page_no));
  // ↑ 從 Menu 創建一個 Page 子視圖
  if (page) {
    context->menu.page_size = page_size;
    context->menu.page_no = page_no;
    context->menu.is_last_page = Bool(page->is_last_page);
    context->menu.num_candidates = page->candidates.size();
    context->menu.candidates = new RimeCandidate[page->candidates.size()];
    for (const an<Candidate>& cand : page->candidates) {
      // 複製當前頁的候選
    }
  }
}
get_context() 做的事：從 Menu 中取第 N 頁，只返回那一頁的候選，附帶分頁元數據。

API 2: candidate_list_begin/next/end — 遍歷整個 Menu
在 rime_api.cc:384-430：


RIME_API Bool RimeCandidateListFromIndex(RimeSessionId session_id,
                                         RimeCandidateListIterator* iterator,
                                         int index) {
  // ...
  iterator->ptr = ctx->composition().back().menu.get();  // ← 直接指向 Menu 對象
  iterator->index = index - 1;
  return True;
}

RIME_API Bool RimeCandidateListNext(RimeCandidateListIterator* iterator) {
  Menu* menu = reinterpret_cast<Menu*>(iterator->ptr);
  ++iterator->index;
  if (auto cand = menu->GetCandidateAt((size_t)iterator->index)) {
    // ← 直接從 Menu 取候選，不經過 Page
    return True;
  }
  return False;
}
candidate_list_begin/next/end 做的事：繞過分頁，直接迭代 Menu 中的全部候選。它完全沒有分頁概念。

這意味著什麼
get_context()	candidate_list_begin/next/end
分頁	✅ 引擎分頁，只返回一頁	❌ 無分頁，遍歷全部候選
使用場景	前端顯示（有分頁）	需要遍歷全部候選的場景
Weasel 用哪個	✅ 用這個	❌ 完全不用
回傳數據量	page_size（通常 5~9）	全部候選（可能上百）
性能	無所謂 List/Iterator	如果全部遍歷則 Iterator 有意義
這就是為什麼 Weasel 不用迭代器 API — 因為它會拿到全部候選而不是一頁。對於有分頁的前端，get_context() 才是正確的 API。

而 Ngan.Ime 文檔 CandidateLifecycle.cs:33-37 記的設計是用 candidate_list_begin/next/end + 硬 cap 16 個 — 這其實繞過了 Rime 的分頁機制，自己手動限制數量。當前實作 AndroidRimeImeService.SyncStateFromRime() 改用 get_context() 反而是正確的方向。

回答你的問題
IAsyncEnumerable<ICandidate>？不合適
Rime 候選不是異步流式輸出的。get_context() 是一次性同步快照。數據本身就在 native memory 中，沒有任何 async I/O。用 IAsyncEnumerable 是憑空增加 async state machine 的開銷。

IPageAsyE<ICandidate>？也不合適
IPageAsyE 是為調用方驅動分頁設計的 — 調用方通過 PageIdx/PageSize 發起查詢。但 Rime 分頁是引擎驅動的 — 前端不控制頁碼，只是把 PgUp/PgDn 當按鍵送進去，引擎內部切換頁面後，前端被動接收新頁數據。套用 IPageAsyE 會強行把引擎驅動模型扭曲成調用方驅動模型。

IPageAsyE.DataAsyE 是 IAsyncEnumerable<T>，同樣有上述 async 不合適的問題。

IReadOnlyList<ICandidate> 本身沒問題
Weasel 用的就是 std::vector<Text>（等同 List），每個頁最多 6~9 個候選，List 的分配開銷可以忽略。

真正的改進方向
問題不在於 List vs Iterator，而在於當前設計丟掉了分頁元數據和翻頁能力。應該做的改動是：

1. ISvcIme 補充分頁元數據：


public int PageNo{get;set;}       // 當前頁號
public bool IsLastPage{get;set;}  // 是否最後一頁
public int PageSize{get;set;}     // 每頁大小
public int HighlightedIndex{get;set;} // 高亮候選索引
2. ReplaceCandidates 同時接收分頁訊息：


public void ReplaceCandidates(
    IEnumerable<ICandidate>? candidates,
    int pageNo, bool isLastPage, int pageSize, int highlightedIndex
);
3. SyncStateFromRime 讀取 RimeMenu 的分頁字段（它們已經存在但被忽略了）：


ReplaceCandidates(
    ReadCandidates(&Context),
    Menu.page_no,
    Menu.is_last_page != RimeUtil.False,
    Menu.page_size,
    Menu.highlighted_candidate_index
);
4. 確保 PgUp/PgDn 按鍵經過 process_key() 送入 Rime — RimeKeyProcessor 已經支援任意 IKeyEvent，只要 UI 層能產生 PgUp/PgDn 的 key event，翻頁就能工作。

5. UI 層根據 PageNo/IsLastPage 渲染翻頁指示器（類似 weasel 的 < > 或頁碼顯示）。

總結
你的提問	實際情況
"底層用迭代器"	有兩個 API：get_context()（分頁）和 candidate_list_begin/next/end（遍歷全部）。Weasel 用前者
"用 List 性能差"	每頁只有 ~6 個候選，List 無性能問題
"改成 IPageAsyE / IAsyncEnumerable"	都不合適。Rime 是同步快照 + 引擎驅動分頁，不是 DB 分頁也不是異步流
真正的問題是：分頁元數據被丟棄了，翻頁能力缺失了。List 本身不是問題。你覺得這個方向可以嗎？
````
]

