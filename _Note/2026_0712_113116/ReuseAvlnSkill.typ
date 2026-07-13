-T[2026_0712_114341][
先看當前skill。

看
````yaml
    - E:\_code\CsNgaq\Doc\Spec\Frontend.typ
    - E:\_code\CsNgaq\Ngaq.Frontend\proj\Ngaq.Ui\CodeTemplate\Sample\**
    - E:\_code\CsNgaq\Ngaq.Frontend\proj\Ngaq.Ui\Infra\ViewModelBase.cs
    - E:\_code\CsNgaq\Ngaq.Frontend\proj\Ngaq.Ui\Views\MainView.Decl.cs
````
看 `E:\_code\CsRime\SyncSkills.sh`

我現在有很多個avalonia項目、他們都需要遵守大致相同的代碼規範。
比如我在開發Avalime前端項目的時候、讓AI寫代碼的時候 我就讓AI看CsNgaq的前端的規範。
但是這樣很麻煩。每次都要我手動說、他不是一個現成的skill、
而且複用性不高。

但是如果抽成可複用的skill的話、又會有一些問題。
比如裏面的 ViewSample VmSamle用到了一些CsNgaq項目專有的基礎設施
如HandleErr IMk 啥的、不同項目的基礎設施可能不太一樣。

你覺得怎麼做比較好?

][
````md
我看完之後，結論很明確：**不要直接把 `CsNgaq` 的 sample 原樣抽成一個通用 skill**。
因爲你現在那套規範裡其實混了兩種東西：

- 真正可跨項目複用的“規範”
- `CsNgaq` 專有的“基礎設施實現細節”

像這些基本屬於前者，適合抽成通用 skill：

- `Avalonia + 純 C# + 不用 XAML`
- `MVVM`
- `AOT-safe 綁定，禁止字符串 Binding`
- `View / Vm 同目錄、一對一爲主`
- `導航只在 View 層做`
- `UI 默認按豎屏移動端設計`
- `不要硬編碼字體大小`
- `i18n / 禁止硬編碼文案`
- `跨平臺限制`
- `不要讀某些高 token 文件，只讀 decl / interface`

像這些則明顯是後者，不適合直接放進通用 skill：

- `HandleErr`
- `IMk<T>`
- `App.DiOrMk<Ctx>()`
- `Ctx.Bind / CBE / OpBtn / GridStack / ToolView`
- `MainView.Inst`
- 具體 DI 註冊位置
- 具體圖標入口文件
- `ViewModelBase` 裏那套現成能力

所以比較好的做法是分成兩層。

**推薦方案**

1. 做一個“通用前端規範 skill”
內容只放不依賴某個項目的規則，名字可以類似：
- `tsinswreng-avalonia-frontend-common`
- 或 `tsinswreng-avalonia-csharp-mvvm`

這個 skill 只描述“原則”和“抽象角色”，不綁定具體 API。
比如只說：
- View 如何組織
- Vm 如何組織
- 綁定必須 AOT-safe
- 導航責任在 View
- DI 要可測試/可降級
- 字體/i18n/跨平臺約束
- 代碼目錄結構規則

2. 每個項目再做一個“項目適配 skill”
例如：
- `avalime-doc`
- `ngaq-frontend-adapter`

它的職責是把通用 skill 裏的抽象概念，映射到本項目的具體設施。
比如明確說：
- 本項目的 View 基類是什麼
- 本項目的 Vm 基類是什麼
- 綁定要用哪套 helper
- DI 從哪裏拿
- 錯誤處理用哪個 API
- 導航入口在哪
- 圖標看哪個 decl 文件
- 哪些 impl 文件禁止打開

這樣 AI 工作時讀法就是：

1. 先讀通用 skill
2. 再讀當前項目的 adapter skill
3. 最後只讀當前項目的 sample / decl / base 類作爲具體示例

這樣複用性最高，也不會被 `CsNgaq` 的私貨綁死。

**我更推薦再加一個結構約定**

你既然有很多 Avalonia 項目，最好統一每個項目都提供同一組“AI 規範入口文件”，例如：

- `Doc/Spec/FrontendCommon.typ`
- `Doc/Spec/FrontendProjectAdapter.typ`
- `CodeTemplate/Sample/ViewSample.cs`
- `CodeTemplate/Sample/VmSample.cs`
- `Infra/ViewModelBase.cs`
- `Views/MainView.Decl.cs`

然後通用 skill 直接規定：

- 先讀 common spec
- 再讀 project adapter spec
- 再讀 sample/base/decl

這樣以後你對 AI 說一句“看 frontend skill”，它就能按固定入口走，不用每次手工列文件。

**對你現在材料的一個關鍵判斷**

目前 `ViewSample/VmSample` 很有價值，但它更適合做：

- `項目 adapter skill` 的示例來源
- 或“示例庫”

不適合直接作爲“通用 skill 主體”，因爲它已經深度依賴 `CsNgaq.Ui` 的 DSL 和基建。

**我建議你接下來這麼落地**

1. 先把現有 `Frontend.typ` 拆成兩份
2. 通用規則放 `FrontendCommon.typ`
3. `CsNgaq` 專有映射放 `FrontendProjectAdapter.typ`
4. 再做一個真正可複用的 skill，只引用 `FrontendCommon.typ`
5. 每個項目各自保留自己的 adapter skill

**現在有兩個關鍵問題，我不想帶着疑問往下推：**

1. 你的這些 Avalonia 項目，是否都會堅持同一類 DSL / helper 思路？
比如都會有類似 `AppViewBase`、AOT-safe bind helper、某種導航基類。
如果不是，那通用 skill 的粒度就要再往上提，只保留更抽象的規則。

2. 你想要的“可複用”是：
- `A.` 做成一個真正跨倉庫共享的 skill 倉庫
- 還是 `B.` 在每個項目內都保留一份本地 skill，只是靠腳本同步模板

這兩個方向我都能幫你設計，但結構會不一樣。
你回我這兩點，我再給你下一步的具體目錄結構和 skill 拆分草案。
````
]


-T[2026_0712_114400][
	你的理解有很大問題。

][]
