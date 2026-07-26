using Android.App;
using Android.Content;
using Android.InputMethodServices;
using Android.OS;
using Android.Runtime;
using Android.Views;
using Android.Views.InputMethods;
using Avalime.Core.Infra;
using Avalime.Core.Infra.Log;
using Avalime.Core.Keys;
using Avalime.Rime;
using Avalime.UI;
using Avalime.UI.Views;
using Avalime.UI.Views.CandidatesBar;
using Avalime.UI.Views.Clipboard;
using Avalime.UI.Views.Ime;
using Avalime.UI.Views.Input;
using Avalime.UI.Views.Key;
using Avalime.UI.Views.KeyBoard;
using Avalime.UI.Views.RimeLog;
using Avalime.UI.Views.ToolBar;
using Avalime.Android.Voice;
using Avalonia.Android;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.ComponentModel;
using System.Diagnostics;
using Handler = Android.OS.Handler;
using Looper = Android.OS.Looper;

namespace Avalime.Android;

[Service(
	Label = "Avalime",
	Permission = global::Android.Manifest.Permission.BindInputMethod,
	Exported = true)]
[IntentFilter(["android.view.InputMethod"])]
[MetaData("android.view.im", Resource = "@xml/ime_method")]
public class AvalimeInputMethodService : InputMethodService {
	/// 把當前活躍的 IME service 暴露給通知恢復入口使用。
	static WeakReference<AvalimeInputMethodService?> ActiveServiceRef = new(null);

	public AvalimeInputMethodService() { }
	public AvalimeInputMethodService(nint javaReference, JniHandleOwnership transfer)
		: base(javaReference, transfer) { }

	public global::Android.Views.View? InputView{get;set;}
	LoggingAvaloniaView? AvaloniaInputView{get;set;}
	global::Android.Views.View? SplitPlaceholderInputView{get;set;}
	SplitKeyboardOverlayManager? _splitOverlayManager;
	ImeUiState? _uiState;
	PropertyChangedEventHandler? _uiStatePropertyChangedHandler;
	i32 _showSeq = 0;
	long _activeShowSeq = 0;
	long _activeShowStartTick = 0;

	/// 把一次“鍵盤顯示流程”標成遞增序號，便於在 logcat 裡串起同一輪 show 的多個回調。
	long BeginShowTrace() {
		var seq = Interlocked.Increment(ref _showSeq);
		_activeShowSeq = seq;
		_activeShowStartTick = Stopwatch.GetTimestamp();
		return seq;
	}

	/// 對當前 show 序列記錄階段性耗時。
	/// 旋轉後首次彈出若很慢，可直接從這組日誌看出是“回調本身來得晚”還是“某個回調內部執行慢”。
	void LogShowTrace(str Stage, long Seq, Stopwatch Sw) {
		var totalMs = GetShowElapsedMilliseconds(Seq);
		AppLog.Info($"[Perf][Show:{Seq}] {Stage} self={Sw.ElapsedMilliseconds}ms total={totalMs}ms");
	}

	long GetShowElapsedMilliseconds(long Seq) {
		if(_activeShowSeq != Seq || _activeShowStartTick == 0){
			return -1;
		}
		var elapsedTicks = Stopwatch.GetTimestamp() - _activeShowStartTick;
		return elapsedTicks * 1000 / Stopwatch.Frequency;
	}

	static str FormatOrientation(global::Android.Content.Res.Configuration? Cfg) {
		if(Cfg is null){
			return "null";
		}
		return Cfg.Orientation switch {
			global::Android.Content.Res.Orientation.Landscape => "landscape",
			global::Android.Content.Res.Orientation.Portrait => "portrait",
			global::Android.Content.Res.Orientation.Square => "square",
			_ => $"unknown({(i32)Cfg.Orientation})"
		};
	}

	/// 取得螢幕高度的一半，用於設定輸入法視窗高度。
	/// 若無法取得螢幕尺寸則回傳 <see cref="ViewGroup.LayoutParams.WrapContent"/>。
	int GetHalfScreenHeight() {
		var screenHeight = Resources?.DisplayMetrics?.HeightPixels ?? 0;
		return screenHeight > 0 ? screenHeight / 2 : ViewGroup.LayoutParams.WrapContent;
	}

	int GetNormalInputWindowHeight()
	{
		// 普通鍵盤先完全恢復成原來的半屏行爲，
		// 分體功能不得再污染這條已驗證穩定的宿主路徑。
		return GetHalfScreenHeight();
	}

	/// <summary>
	/// 分體模式下，IME 主窗口本身只保留極薄佔位，真正可見鍵盤走 overlay。
	/// 這樣中間 50% 不再屬於 IME 視窗，可把交互還給底下 App。
	/// </summary>
	int GetSplitPlaceholderHeight()
	{
		return 1;
	}

	int GetCurrentInputWindowHeight()
	{
		if(IsSplitKeyboardActive()){
			return GetSplitPlaceholderHeight();
		}
		return GetNormalInputWindowHeight();
	}

	/// <summary>
	/// 普通鍵盤讓 AvaloniaView 跟隨 IME 窗口實際可用高度。
	/// Android 可能不會把整個 raw display half-height 都真正分給內容區；
	/// 若子 view 也硬塞同一個像素高度，豎屏下就更容易把最後一排裁掉。
	/// </summary>
	int GetCurrentInputViewHeight()
	{
		if(IsSplitKeyboardActive()){
			return GetSplitPlaceholderHeight();
		}
		return ViewGroup.LayoutParams.MatchParent;
	}

	bool IsSplitKeyboardActive()
	{
		return _uiState?.IsSplitKeyboardEnabled == true;
	}

	/// 創建新的 Avalonia IME 根視圖。
	/// 通知恢復路徑會丟棄舊 view，再調這裡生成全新視圖實例。
	LoggingAvaloniaView CreateInputView() {
		var inputView = new LoggingAvaloniaView(this) {
			Content = new MainView()
		};
		inputView.LayoutParameters = new ViewGroup.LayoutParams(
			ViewGroup.LayoutParams.MatchParent,
			GetCurrentInputViewHeight()
		);
		return inputView;
	}

	global::Android.Views.View CreateSplitPlaceholderView()
	{
		var ans = new global::Android.Views.View(this);
		ans.SetBackgroundColor(global::Android.Graphics.Color.Transparent);
		ans.LayoutParameters = new ViewGroup.LayoutParams(
			ViewGroup.LayoutParams.MatchParent,
			GetSplitPlaceholderHeight()
		);
		return ans;
	}

	/// IME 視圖只創建一次，後續複用同一個 AvaloniaView。
	/// 這樣可避免 hide/show 之間反覆重建整棵 UI 樹，減少黑屏與狀態丟失。
	LoggingAvaloniaView EnsureAvaloniaInputView()
	{
		AvaloniaInputView ??= CreateInputView();
		return AvaloniaInputView;
	}

	global::Android.Views.View EnsureSplitPlaceholderView()
	{
		SplitPlaceholderInputView ??= CreateSplitPlaceholderView();
		return SplitPlaceholderInputView;
	}

	global::Android.Views.View GetDesiredInputView()
	{
		if(IsSplitKeyboardActive()){
			return EnsureSplitPlaceholderView();
		}
		return EnsureAvaloniaInputView();
	}

	/// 同一個輸入框重新顯示 IME 時，Android 可能只把窗口拉回來，
	/// 但不會重新創建 input view。這裡主動把複用的 view 從舊父節點摘下，
	/// 再交回 InputMethodService 重掛，盡量避免窗口回來但內容發黑的情況。
	void ReattachInputView() {
		var inputView = GetDesiredInputView();
		if(inputView.Parent is ViewGroup parent){
			parent.RemoveView(inputView);
		}
		SetInputView(inputView);
		InputView = inputView;
		// 某些 split -> normal 回切場景下，新的普通 AvaloniaView 掛回去時，
		// 宿主窗口仍可能暫時保留上一輪 placeholder 的佈局狀態。
		// 這裡在實例真正掛上後立刻再同步一次窗口與內容高度，避免內容樹被錯誤裁成只剩一條。
		UpdateInputWindowLayout();
		AppLog.Info($"[IME] ReattachInputView attached={inputView.IsAttachedToWindow} lpH={inputView.LayoutParameters?.Height} view={inputView.GetType().Name}");
		inputView.RequestLayout();
		inputView.Invalidate();
	}

	/// 輸入法窗口已經可見後，只做輕量 layout / draw 刷新。
	/// 避免同一次 show 流程內再次 detach/attach AvaloniaView，
	/// 尤其在橫豎屏切換後首次彈出時，二次重掛更容易放大 surface 重建成本。
	void RefreshVisibleInputView() {
		var inputView = GetDesiredInputView();
		InputView = inputView;
		UpdateInputWindowLayout();
		inputView.RequestLayout();
		inputView.Invalidate();
	}

	/// 把輸入視圖從舊父節點摘下，避免後續丟棄 / 重建時殘留在舊窗口樹中。
	void DetachInputViewFromParent(global::Android.Views.View? inputView) {
		if(inputView?.Parent is ViewGroup parent){
			parent.RemoveView(inputView);
		}
	}

	/// 系統自己調用 `setInputView(...)` 前，也要確保複用 view 已經脫離舊父節點。
	/// 否則旋轉後首次 show 或 configuration reset 時，
	/// `InputMethodService` 內部再次 addView 會直接崩成
	/// “The specified child already has a parent”。
	global::Android.Views.View PrepareInputViewForSystemAttach() {
		var inputView = GetDesiredInputView();
		DetachInputViewFromParent(inputView);
		InputView = inputView;
		return inputView;
	}

	/// 丟棄當前複用的 AvaloniaView。
	/// 這是通知“強制恢復”路徑的核心：一旦黑屏不可恢復，就不要再信任舊 surface。
	void ResetInputViewInstance() {
		var oldInputView = InputView;
		if(oldInputView is null){
			return;
		}

		DetachInputViewFromParent(oldInputView);
		if(AvaloniaInputView?.Content is IDisposable disposableContent){
			disposableContent.Dispose();
		}
		if(AvaloniaInputView is not null){
			AvaloniaInputView.Content = null;
			AvaloniaInputView.Dispose();
		}else{
			SplitPlaceholderInputView?.Dispose();
		}
		InputView = null;
		AvaloniaInputView = null;
		SplitPlaceholderInputView = null;
	}

	/// <summary>
	/// 分體 -> 普通模式時，不再複用舊的普通 AvaloniaInputView。
	/// 目前實測表明，一旦進過分體，舊普通 view 可能殘留錯亂的命中/顯示狀態；
	/// 通知“強制恢復”之所以能暫時救回來，本質上就是整棵普通 view 被重建了。
	/// </summary>
	void ResetNormalInputViewForSplitExit()
	{
		if(AvaloniaInputView is null){
			return;
		}
		DetachInputViewFromParent(AvaloniaInputView);
		if(ReferenceEquals(InputView, AvaloniaInputView)){
			InputView = null;
		}
		if(AvaloniaInputView.Content is IDisposable disposableContent){
			disposableContent.Dispose();
		}
		AvaloniaInputView.Content = null;
		AvaloniaInputView.Dispose();
		AvaloniaInputView = null;
		AppLog.Info("[IME] ResetNormalInputViewForSplitExit done");
	}

	/// 真正執行通知恢復：丟棄舊 view、創建新 view、重新掛回 IME window，
	/// 最後主動請求系統再次顯示輸入法，盡量把已黑屏的鍵盤拉回可用狀態。
	void ForceRecoverImeView() {
		AppLog.Info("[IME] ForceRecoverImeView begin");
		ResetInputViewInstance();
		SyncSplitKeyboardPresentation();
		ReattachInputView();
		RequestShowSelfCompat();
		AppLog.Info($"[IME] ForceRecoverImeView end inputViewNull={InputView is null}");
	}

	void SyncSplitKeyboardPresentation()
	{
		AppLog.Info($"[IME] SyncSplitKeyboardPresentation active={IsSplitKeyboardActive()} inputView={InputView?.GetType().Name ?? "null"}");
		UpdateInputWindowLayout();
		if(!IsSplitKeyboardActive()){
			_splitOverlayManager?.Hide();
			return;
		}
		if(!AvalimeOverlayPermission.CanDraw(this)){
			AppLog.Warn("[IME] split keyboard requested without overlay permission");
			if(!AvalimeOverlayPermission.Ensure(this) && _uiState is not null){
				// 沒權限時回退到普通鍵盤，避免把輸入法切到“只有 1px 佔位但沒有 overlay”的不可用狀態。
				_uiState.IsSplitKeyboardEnabled = false;
			}
			return;
		}
		_splitOverlayManager ??= new SplitKeyboardOverlayManager(this);
		_splitOverlayManager.Show();
	}

	/// <summary>
	/// 分體開關切換時，只在 input view 實例需要切換時才重掛 IME 視圖。
	/// 若仍是同一個實例，則只刷新 layout，避免無意義的 detach/attach 放大卡頓。
	/// </summary>
	void ApplySplitKeyboardToggle()
	{
		if(!IsSplitKeyboardActive()){
			ResetNormalInputViewForSplitExit();
		}
		var desiredInputView = GetDesiredInputView();
		var needsReattach = !ReferenceEquals(InputView, desiredInputView);
		AppLog.Info($"[IME] ApplySplitKeyboardToggle desired={desiredInputView.GetType().Name} current={InputView?.GetType().Name ?? "null"} reattach={needsReattach}");
		SyncSplitKeyboardPresentation();
		if(needsReattach){
			ReattachInputView();
			return;
		}
		RefreshVisibleInputView();
	}

	/// <summary>
	/// 預先創建 split overlay 內容樹，把首次切到分體時的 Avalonia 構造成本前移。
	/// 僅創建 view，不 add 到 WindowManager，因此不會提前攔截畫面。
	/// </summary>
	void WarmSplitOverlayIfPossible()
	{
		if(!AvalimeOverlayPermission.CanDraw(this)){
			AppLog.Info("[IME] WarmSplitOverlayIfPossible skipped: no permission");
			return;
		}
		_splitOverlayManager ??= new SplitKeyboardOverlayManager(this);
		_splitOverlayManager.EnsureCreated();
		AppLog.Info("[IME] WarmSplitOverlayIfPossible created");
	}

	void UpdateInputWindowLayout()
	{
		var windowHeight = GetCurrentInputWindowHeight();
		var inputViewHeight = GetCurrentInputViewHeight();
		AppLog.Info($"[IME] UpdateInputWindowLayout windowHeight={windowHeight} inputViewHeight={inputViewHeight} inputView={InputView?.GetType().Name ?? "null"}");
		if(InputView?.LayoutParameters is ViewGroup.LayoutParams lp){
			lp.Width = ViewGroup.LayoutParams.MatchParent;
			lp.Height = inputViewHeight;
			InputView.LayoutParameters = lp;
			InputView.RequestLayout();
		}
		Window?.Window?.SetLayout(ViewGroup.LayoutParams.MatchParent, windowHeight);
	}

	/// 安卓 9+ 可以明確請求重新顯示 IME。
	/// 更低版本上缺少這個 API，則退化為只做 view 重建與重掛。
	void RequestShowSelfCompat() {
		if(OperatingSystem.IsAndroidVersionAtLeast(28)){
			RequestShowSelf(ShowFlags.Forced);
			return;
		}

		AppLog.Info("[IME] RequestShowSelf skipped: Android < 28");
	}

	/// 從通知廣播進來後，統一切回主線程執行恢復。
	/// IME view 的摘掛與重建都屬於 UI 操作，不能在廣播線程直接做。
	internal static void TryRecoverFromNotification() {
		if(!ActiveServiceRef.TryGetTarget(out var service) || service is null){
			AppLog.Warn("[IME] Recover notification ignored: no active service");
			return;
		}

		var mainHandler = new Handler(Looper.MainLooper!);
		mainHandler.Post(service.ForceRecoverImeView);
	}

	/// 評估當前是否應進入全螢幕輸入模式。
	/// Android 輸入法在橫屏等場景下預設會切換為全螢幕模式（佔滿整個螢幕、隱藏應用內容）。
	/// <returns>永遠回傳 <c>false</c>，強制禁用全螢幕模式，始終以嵌入式鍵盤視窗顯示。</returns>
	/// 此方法在輸入法視窗即將顯示時被系統調用。
	/// 回傳 <c>false</c> 可確保鍵盤不會遮擋整個螢幕，保持與普通軟鍵盤一致的行為。
	public override bool OnEvaluateFullscreenMode() {
		return false;
	}

	/// 記錄配置切換到達 IME service 的時機。
	/// 若旋轉後第一次彈鍵盤明顯變慢，這裡可先確認 service 是否已經及時收到 orientation 變更。
	public override void OnConfigurationChanged(global::Android.Content.Res.Configuration? newConfig) {
		// base 內部會走 resetStateForNewConfiguration / showWindow，
		// 其中可能再次把當前 input view 掛回 IME window。
		// 若舊 view 還殘留在前一個父節點上，這裡就會因重複 addView 直接崩潰。
		DetachInputViewFromParent(InputView);
		base.OnConfigurationChanged(newConfig);
		AppLog.Info($"[Perf][Cfg] orientation={FormatOrientation(newConfig)} size={Resources?.DisplayMetrics?.WidthPixels}x{Resources?.DisplayMetrics?.HeightPixels}");
	}

	/// 配置輸入法視窗的佈局屬性。
	/// <param name="win">輸入法視窗。可能是候選窗 (<c>isCandidatesOnly=true</c>) 或完整輸入視窗。</param>
	/// <param name="isFullscreen">是否為全螢幕模式。<see cref="OnEvaluateFullscreenMode"/> 已強制回傳 <c>false</c>，故此處始終為 <c>false</c>。</param>
	/// <param name="isCandidatesOnly">是否僅顯示候選詞視窗（不包含鍵盤）。</param>
	/// 將視窗寬度設為 <see cref="ViewGroup.LayoutParams.MatchParent"/>（填滿螢幕寬度），
	/// 高度設為螢幕的一半（通過 <see cref="GetHalfScreenHeight"/>），確保鍵盤區域合理可視。
	public override void OnConfigureWindow(Window? win, bool isFullscreen, bool isCandidatesOnly) {
		var sw = Stopwatch.StartNew();
		base.OnConfigureWindow(win, false, isCandidatesOnly);
		win?.SetLayout(ViewGroup.LayoutParams.MatchParent, GetCurrentInputWindowHeight());
		AppLog.Info($"[Perf][Window] OnConfigureWindow self={sw.ElapsedMilliseconds}ms fullscreen={isFullscreen} candidatesOnly={isCandidatesOnly} height={GetCurrentInputWindowHeight()} orientation={FormatOrientation(Resources?.Configuration)}");
	}

	/// 創建輸入法的主輸入視圖（鍵盤 + 候選欄等 UI）。
	/// 此方法是輸入法 UI 的入口，返回的 <see cref="global::Android.Views.View"/> 會嵌入到系統輸入法視窗中。
	/// <returns>包含 Avalonia 內容的 <see cref="LoggingAvaloniaView"/>，作為輸入法 UI 的根視圖。</returns>
	/// <para><b>調用時機：</b>系統在需要顯示輸入法鍵盤區域時調用。</para>
	/// <para><b>緩存邏輯：</b>
	///   只創建一次 <see cref="InputView"/>，後續持續複用同一個視圖實例。
	///   每次顯示前透過 <see cref="ReattachInputView"/> 重新掛回 IME window，
	///   避免同焦點 editor 下窗口重顯示時內容樹沒有正確回到窗口。
	/// </para>
	/// <para><b>視圖內容：</b><see cref="MainView"/> 是 Avalime 的頂層 UI 組件，包含鍵盤、候選欄、工具欄等。</para>
	/// <para><b>佈局：</b>寬度填滿螢幕、高度為螢幕的一半。</para>
	public override global::Android.Views.View OnCreateInputView() {
		var sw = Stopwatch.StartNew();
		var inputView = PrepareInputViewForSystemAttach();
		AppLog.Info($"[IME] OnCreateInputView self={sw.ElapsedMilliseconds}ms orientation={FormatOrientation(Resources?.Configuration)} inputViewType={inputView.GetType().Name}");
		return inputView;
	}

	/// 請求 Android 隱藏當前輸入法窗口。
	/// 這裡不再把 hide 綁定到“下次重建整個輸入視圖”，而是保留同一個 view 複用。
	public void HideKeyboard() {
		AppLog.Info("[IME] HideKeyboard requested");
		RequestHideSelf(0);
	}

	/// 輸入法服務創建時的回調。
	/// 在 Android 系統首次綁定輸入法服務時調用，是整個輸入法生命週期的起點。
	/// <para><b>執行順序：</b></para>
	/// <list type="number">
	/// <item>設定輸入法主題樣式（<c>MyTheme_Ime</c>）。</item>
	/// <item>構建 DI 容器，註冊所有服務：
	///   <list type="bullet">
	///     <item><see cref="IImeKeyProcessor"/> → <see cref="RimeKeyProcessor"/>（Rime 按鍵處理）</item>
	///     <item><see cref="IOsKeyProcessor"/> → <see cref="AndroidStubOsKeyProcessor"/>（stub，後續替換為 <see cref="AndroidOsKeyProcessor"/>）</item>
	///     <item><see cref="IKeyboardHost"/> → <see cref="AndroidKeyboardHost"/>（鍵盤宿主）</item>
	///     <item><see cref="IClipboardService"/> → <see cref="AndroidClipboardService"/>（剪貼簿）</item>
	///     <item><see cref="ISvcIme"/> → <c>Avalime.Rime.SvcIme</c>（Rime 引擎實例）</item>
	///     <item>各 ViewModel（VmIme、VmToolBar、VmKeyBoard 等）</item>
	///   </list>
	/// </item>
	/// <item>初始化 <see cref="ISvcIme"/>，設置狀態文字為 "正在連接 Rime"。</item>
	/// <item>配置 <see cref="IOsKeyProcessor"/> 為 <see cref="AndroidOsKeyProcessor"/>，將 Rime 未處理的按鍵轉發到 <see cref="InputMethodService.CurrentInputConnection"/>。</item>
	/// <item>訂閱 <see cref="ISvcIme.OnCommit"/>，在 main looper 上調用 <c>InputConnection.CommitText</c> 將 Rime commit 的文字上屏。</item>
	/// </list>
	public override void OnCreate() {
		SetTheme(Resource.Style.MyTheme_Ime);
		base.OnCreate();
		AppLog.Info("[IME] OnCreate");
		ActiveServiceRef.SetTarget(this);

		var services = new ServiceCollection();
		AndroidVoiceComposition.AddAndroidVoiceInput(services, this);
		services.AddSingleton<RimeSetup>(_ => RimeSetup.Inst);
		services.AddSingleton<IRimeLogSource>(_ => RimeLogStore.Inst);
		services.AddSingleton<IImeKeyProcessor, RimeKeyProcessor>();
		services.AddSingleton<IOsKeyProcessor, AndroidStubOsKeyProcessor>();
		services.AddSingleton<IKeyboardHost>(_ => new AndroidKeyboardHost(() => this));
		services.AddSingleton<IClipboardService, AndroidClipboardService>();
		services.AddSingleton<ImeUiState>();
		services.AddSingleton<ISvcIme, SvcIme>();
		services.AddSingleton<RimeLogBuffer>();
		services.AddTransient<VmIme>();
		services.AddTransient<VmToolBar>();
		services.AddTransient<VmCandidatesBar>();
		services.AddTransient<VmInput>();
		services.AddTransient<VmClipboard>();
		services.AddTransient<VmRimeLog>();
		services.AddTransient<VmKey>();
		services.AddTransient<VmKeyBoard>();
		services.AddSingleton<ILogger>(_ => AppLog.Inst);
		var provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateOnBuild = false, ValidateScopes = false });
		Di.SvcProvider = provider;

		var imeState = Di.GetRSvc<ISvcIme>();
		_uiState = Di.GetRSvc<ImeUiState>();
		imeState.StatusText = "正在連接 Rime";

		// 未處理按鍵先過可切換映射器，再轉發給 Android OS。
		// 這樣不會改動 Rime 收到的原始鍵，只修正最終交給宿主應用的鍵值。
		imeState.OsKeyProcessor = new KeyRemappingOsKeyProcessor(
			new AndroidOsKeyProcessor(() => CurrentInputConnection),
			new KeyEventRemapper(
				() => _uiState?.IsSystemKeyRemappingEnabled == true,
				[
					new KeyRemapRule(KeyChars.Backspace, KeyChars.Alt_R),
				]
			)
		);

		// Rime commit 文字上屏
		imeState.OnCommit += (sender, text) => {
			var swPost = System.Diagnostics.Stopwatch.StartNew();
			var mainHandler = new Handler(Looper.MainLooper!);
			mainHandler.Post(() => {
				var swMain = System.Diagnostics.Stopwatch.StartNew();
				var ic = CurrentInputConnection;
				if (ic is not null) {
					var swCommit = System.Diagnostics.Stopwatch.StartNew();
					ic.CommitText(text, 1);
					AppLog.Debug($"[Perf] Android.OnCommit CommitText call: {swCommit.ElapsedMilliseconds}ms, text: {text}");
					AppLog.Info($"[IME] CommitText: {text}");
				} else {
					AppLog.Warn("[IME] CommitText skipped: no InputConnection");
				}
				AppLog.Debug($"[Perf] Android.OnCommit main-thread handler total: {swMain.ElapsedMilliseconds}ms, text: {text}");
			});
			AppLog.Debug($"[Perf] Android.OnCommit post_to_main: {swPost.ElapsedMilliseconds}ms, text: {text}");
		};
		_uiStatePropertyChangedHandler = (_, e) => {
			if(e.PropertyName != nameof(ImeUiState.IsSplitKeyboardEnabled)){
				return;
			}
			var mainHandler = new Handler(Looper.MainLooper!);
			mainHandler.Post(() => {
				ApplySplitKeyboardToggle();
			});
		};
		_uiState.PropertyChanged += _uiStatePropertyChangedHandler;
		var warmHandler = new Handler(Looper.MainLooper!);
		warmHandler.Post(WarmSplitOverlayIfPossible);
	}

	/// 輸入視圖即將顯示時的回調。
	/// 在編輯框獲得焦點、輸入法鍵盤即將彈出時由系統調用。
	/// <param name="info">當前編輯框的 <see cref="EditorInfo"/>，包含輸入類型、IME 選項等訊息。可為 <c>null</c>。</param>
	/// <param name="restarting">
	/// 是否為重啟場景。<c>true</c> 表示輸入法在同一編輯框中重新開始輸入（例如輸入類型切換），
	/// <c>false</c> 表示全新開始（例如焦點從一個編輯框移到另一個）。
	/// </param>
	/// 每次開始顯示輸入視圖時，都主動重掛複用的 <see cref="InputView"/>。
	/// 這一層專門覆蓋“同一個 editor 焦點未丟，但 IME window 被隱藏後又重新顯示”的場景。
	public override void OnStartInputView(EditorInfo? info, bool restarting) {
		var seq = BeginShowTrace();
		var sw = Stopwatch.StartNew();
		base.OnStartInputView(info, restarting);
		SyncSplitKeyboardPresentation();
		ReattachInputView();
		LogShowTrace($"OnStartInputView restarting={restarting} orientation={FormatOrientation(Resources?.Configuration)}", seq, sw);
	}

	/// 輸入法視窗已顯示時的回調。
	/// 在輸入法視窗對用戶可見後由系統調用，通常在動畫結束後觸發。
	/// 這裡只做輕量重繪，不再重掛複用 view。
	/// 同一次顯示流程裡若在 <see cref="OnStartInputView"/> 之後再次 detach/attach，
	/// 旋轉後首次顯示更容易把 Avalonia surface 的重建成本放大成肉眼可見卡頓。
	public override void OnWindowShown() {
		var seq = _activeShowSeq == 0 ? BeginShowTrace() : _activeShowSeq;
		var sw = Stopwatch.StartNew();
		base.OnWindowShown();
		SyncSplitKeyboardPresentation();
		RefreshVisibleInputView();
		LogShowTrace($"OnWindowShown inputViewNull={InputView is null} orientation={FormatOrientation(Resources?.Configuration)}", seq, sw);
	}

	/// 輸入法視窗已隱藏時的回調。
	/// 在輸入法視窗完全不可見後由系統調用（例如用戶切換到其他 App、按下返回鍵隱藏鍵盤）。
	/// 視窗隱藏時不會立即銷毀輸入視圖，<c>_inputView</c> 會被保留以便下次快速顯示。
	public override void OnWindowHidden() {
		base.OnWindowHidden();
		_splitOverlayManager?.Hide();
		AppLog.Info("[IME] OnWindowHidden");
	}

	/// 在 main looper 上向當前輸入連接提交文字（上屏）。
	/// 與 <see cref="ISvcIme.OnCommit"/> 訂閱不同，此方法是提供給外部（如 ViewModel）直接調用的提交入口。
	/// <param name="text">要上屏的文字。</param>
	/// 必須在主執行緒上操作 <see cref="InputMethodService.CurrentInputConnection"/>，
	/// 因此通過 <see cref="Handler"/> Post 到 <see cref="Looper.MainLooper"/>。
	public void CommitText(str text) {
		var mainHandler = new Handler(Looper.MainLooper!);
		mainHandler.Post(() => {
			var ic = CurrentInputConnection;
			if (ic is not null) {
				ic.CommitText(text, 1);
			}
		});
	}

	/// 輸入視圖結束時的回調。
	/// 在編輯框失去焦點或輸入法鍵盤即將隱藏時由系統調用。
	/// <param name="finishingInput">
	/// <c>true</c> 表示本次輸入會話完全結束（焦點離開編輯框、切換到其他 App）；
	/// <c>false</c> 表示僅是暫時隱藏或過渡狀態。
	/// </param>
	/// 這裡不再在 hide 後強制重建整棵 UI 樹。
	/// 實際驗證表明，多次創建 Avalonia IME 視圖更容易把問題變成“重新顯示後黑屏或狀態亂掉”。
	/// 因此改為保留單例 view，真正的重掛工作放在 <see cref="OnStartInputView"/> / <see cref="OnWindowShown"/>。
	public override void OnFinishInputView(bool finishingInput) {
		base.OnFinishInputView(finishingInput);
		_splitOverlayManager?.Hide();
		AppLog.Info("[IME] OnFinishInputView");
	}

	/// 輸入法服務銷毀時的回調。
	/// 在 Android 系統終止輸入法服務時調用（如用戶卸載 App、系統回收記憶體、或進程被殺死）。
	/// 此為生命週期的最後一站。目前主要記錄日誌用於追蹤服務生命週期。
	/// 若需要在銷毀前釋放 Rime 引擎資源（如 <c>rime_finalize()</c>），應在此處添加清理邏輯。
	public override void OnDestroy() {
		ActiveServiceRef.SetTarget(null);
		if(_uiState is not null && _uiStatePropertyChangedHandler is not null){
			_uiState.PropertyChanged -= _uiStatePropertyChangedHandler;
		}
		_splitOverlayManager?.Dispose();
		_splitOverlayManager = null;
		ResetInputViewInstance();
		base.OnDestroy();
		AppLog.Info("[IME] OnDestroy");
	}
}

/// 通知點擊後的恢復廣播接收器。
/// 它本身不持有 UI 狀態，只負責把“請求恢復”轉交給當前活躍的 IME service。
[BroadcastReceiver(Enabled = true, Exported = false)]
[IntentFilter([AvalimeRecoveryNotification.ActionRecoverIme])]
public class AvalimeImeRecoveryReceiver : BroadcastReceiver {
	public override void OnReceive(Context? context, Intent? intent) {
		if(intent?.Action != AvalimeRecoveryNotification.ActionRecoverIme){
			return;
		}

		AppLog.Info("[IME] Recovery notification clicked");
		AvalimeInputMethodService.TryRecoverFromNotification();
	}
}

class AndroidStubOsKeyProcessor : IOsKeyProcessor {
	public event ErrHandler? OnErr;
	public Task<IRespOnKeyEvent> OnKeyEvents(IEnumerable<IKeyEvent> keyEvents, CT Ct)
		=> Task.FromResult<IRespOnKeyEvent>(new RespOnKeyEvent());
}

class AndroidStubImeKeyProcessor : IImeKeyProcessor {
	public event ErrHandler? OnErr;
	public Task<IRespOnKeyEvent> OnKeyEvents(IEnumerable<IKeyEvent> keyEvents, CT Ct)
		=> Task.FromResult<IRespOnKeyEvent>(new RespOnKeyEvent());
}
