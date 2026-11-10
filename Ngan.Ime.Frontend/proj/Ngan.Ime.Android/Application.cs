using Android.App;
using Android.Runtime;
using Ngan.Ime.Core.Keys;
using Ngan.Ime.Rime;
using Ngan.Ime.UI;
using Ngan.Ime.Android.Voice;
using Avalonia;
using Avalonia.Android;
using Microsoft.Extensions.DependencyInjection;
using Tsinswreng.CsCfg;
using Tsinswreng.CsKeyNode;
using Tsinswreng.CsTools;
using System.IO;
using AssetManager = Android.Content.Res.AssetManager;
namespace Ngan.Ime.Android;

[Application]
public class Application : AvaloniaAndroidApplication<App>
{
	const string LocalRimeSoName = "librime.so";
	/// 本宿主在日誌與配置裡的自稱。
	const str AppName = "Ngan.Ime";

	public Application(nint javaReference, JniHandleOwnership transfer)
		: base(javaReference, transfer)
	{
		// 1: 日誌先落地。內層實現愈早指定愈好，啟動期失敗才有地方看。
		AppLog.Inst.InnerLogger = new AndroidLogger(AppName);

		var Ctx = global::Android.App.Application.Context;
		// 2: 儲存區根目錄＝應用的外部私有目錄；Context 由本類注入（它就是那個 Context）。
		ExternalFilesBaseDirMgr.Inst._Ctx = Ctx;
		var BaseDir = ExternalFilesBaseDirMgr.Inst;
		EnsureDir(BaseDir.GetBaseDir());

		// 3: 先把配置檔從 assets 放到外部私有目錄（缺檔才複製），再接配置。
		InitCfgFiles(Ctx, BaseDir);
		AppCfg.Inst.Setup(BaseDir);

		// 4: 引擎與語音所需的 so／資源落地，並在配置缺失時補本機默認值。
		SetupSoFiles(Ctx, BaseDir);
	}

	/// <summary>
	/// 把 assets 裡的兩份配置檔放到應用儲存區。已存在就不覆蓋——那是使用者或前一次運行留下的內容。
	/// 日常配置檔的位置由出廠配置的引導鍵決定，故先落出廠檔再問路徑。
	/// </summary>
	static void InitCfgFiles(global::Android.Content.Context Ctx, I_GetBaseDir BaseDir)
	{
		var Dir = BaseDir.GetBaseDir();
		var RoPath = Path.Combine(Dir, AppCfg.RoCfgFileName);
		EnsureAssetFile(Ctx.Assets!, AppCfg.RoCfgFileName, RoPath, overwrite: false);

		// 用臨時讀者取出引導鍵；AppCfg.Setup 之後還會再讀一次，這點成本可忽略。
		var Ro = new Tsinswreng.CsCfg.Json.JsonFileCfgReader(RoPath, AppCfg.RoSrcName);
		var RwRelPath = Ro.Get(KeysAppCfg.RwCfgPath);
		if(string.IsNullOrWhiteSpace(RwRelPath)){
			throw KeysErr.Cfg.RwCfgPathEmpty.ToErr(nameof(KeysAppCfg.RwCfgPath));
		}
		var RwPath = Path.IsPathRooted(RwRelPath) ? RwRelPath : Path.Combine(Dir, RwRelPath);
		EnsureAssetFile(Ctx.Assets!, Path.GetFileName(RwPath), RwPath, overwrite: false);
	}

	static string SetupSoFiles(global::Android.Content.Context ctx, I_GetBaseDir BaseDir)
	{
		var internalDir = ctx.FilesDir!.AbsolutePath!;
		var externalDir = BaseDir.GetBaseDir();
		EnsureDir(externalDir);
		string[] passthroughSoFiles = ["libc++_shared.so", "libCsRimeLua.so"];

		foreach (var so in passthroughSoFiles)
		{
			var dst = System.IO.Path.Combine(internalDir, so);
			// 優先：內部目錄已有且非空就不複製（開發者用 run-as cp 放進去的）
			if (System.IO.File.Exists(dst) && new FileInfo(dst).Length > 0)
			{
				AppLog.Info("[" + AppName + "] already in place: " + dst);
				continue;
			}
			if(File.Exists(dst)){
				try{
					File.Delete(dst);
				}catch(Exception ex){
					AppLog.Warn("[" + AppName + "] failed to delete invalid so " + dst + ": " + ex.Message);
				}
			}
			// 嘗試從外部私有目錄複製（需要儲存權限，現代 Android 通常失敗）
			var src = System.IO.Path.Combine(externalDir, so);
			try
			{
				if (System.IO.File.Exists(src))
				{
					System.IO.File.Copy(src, dst, overwrite: true);
					AppLog.Info("[" + AppName + "] copied " + src + " -> " + dst);
				}
			}
			catch (System.Exception ex)
			{
				AppLog.Warn("[" + AppName + "] copy skipped for " + so + ": " + ex.Message);
			}
		}

		var localRime = System.IO.Path.Combine(internalDir, LocalRimeSoName);
		ToolFile.EnsureFile(localRime);
		try
		{
			ExtractAssetFile(ctx.Assets!, "rime/librime.bin", localRime);
			AppLog.Info("[" + AppName + "] extracted asset rime/librime.bin -> " + localRime);
		}
		catch (System.Exception ex)
		{
			AppLog.Error(ex, "[" + AppName + "] extract asset rime failed");
		}

		// preload libc++_shared.so, required by librime.so
		var libcpp = System.IO.Path.Combine(internalDir, "libc++_shared.so");
		if (System.IO.File.Exists(libcpp) && new FileInfo(libcpp).Length > 0)
		{
			try
			{
				System.Runtime.InteropServices.NativeLibrary.Load(libcpp);
				AppLog.Info("[" + AppName + "] preloaded libc++_shared.so");
			}
			catch (System.Exception ex)
			{
				AppLog.Error(ex, "[" + AppName + "] preload libc++_shared.so failed");
			}
		}

		var userDataDir = AppCfg.Inst.Get(KeysAppCfg.Librime.RimeTraits.user_data_dir);
		if(string.IsNullOrWhiteSpace(userDataDir)){
			userDataDir = Path.Combine(externalDir, "UserData");
		}

		EnsureDir(userDataDir);
		// 配置優先；只有明確未配置時，宿主才補當前設備上的默認值。
		EnsureCfgOrDefault(KeysAppCfg.Librime.DllPath, localRime);
		EnsureCfgOrDefault(KeysAppCfg.Librime.RimeTraits.user_data_dir, userDataDir);
		EnsureCfgOrDefault(KeysAppCfg.Librime.RimeTraits.distribution_code_name, "Tsinswreng.Ngan.Ime.Android");
		EnsureCfgOrDefault(KeysAppCfg.Librime.RimeTraits.app_name, ctx.PackageName ?? "rime.ngan.ime");

		return internalDir;
	}

	/// <summary>
	/// 雙源配置以顯式配置爲準；只有缺省值爲空時，才由宿主補上可運行的默認值。
	/// </summary>
	static void EnsureCfgOrDefault(KeyNode<str> Key, str DefaultValue)
	{
		var Cur = AppCfg.Inst.Get(Key) ?? "";
		if(!string.IsNullOrWhiteSpace(Cur)){
			return;
		}
		AppCfg.Inst.SetNoSave(Key, DefaultValue);
	}

	/// <summary>
	/// 建給 MainActivity 用的容器。輸入法服務另有自己的容器（需要以 Service 為鍵盤宿主），
	/// 但兩者的服務清單都由 DiXxx 提供，避免分岔。
	/// </summary>
	static IServiceProvider BuildActivityServices()
	{
		var Svc = new ServiceCollection();
		Svc.SetupCore();
		Svc.AddAppLog(new AndroidLogger(AppName));
		Svc.AddSingleton<I_GetBaseDir>(ExternalFilesBaseDirMgr.Inst);
		Svc.SetupCfg(AppCfg.Inst);
		Svc.SetupRime();
		Svc.SetupAndroid();
		Svc.SetupUi();
		Svc.AddSingleton<IKeyboardHost, ActivityKeyboardHost>();
		Svc.AddSingleton<IClipboardService, AndroidClipboardService>();
		AndroidVoiceComposition.AddAndroidVoiceInput(Svc, global::Android.App.Application.Context);
		return Svc.BuildServiceProvider(new ServiceProviderOptions { ValidateOnBuild = false, ValidateScopes = false });
	}

	static void EnsureAssetFile(AssetManager assets, string assetPath, string outputPath, bool overwrite)
	{
		EnsureParentDir(outputPath);
		if(File.Exists(outputPath) && !overwrite){
			return;
		}
		ExtractAssetFile(assets, assetPath, outputPath);
	}

	static void EnsureDir(string dirPath)
	{
		if(string.IsNullOrWhiteSpace(dirPath)){
			return;
		}
		var keepFile = Path.Combine(dirPath, ".keep");
		ToolFile.EnsureFile(keepFile);
		if(File.Exists(keepFile)){
			File.Delete(keepFile);
		}
	}

	static void ExtractAssetFile(AssetManager assets, string assetPath, string outputPath)
	{
		using var input = assets.Open(assetPath);
		using var output = System.IO.File.Create(outputPath);
		input.CopyTo(output);
	}

	static void EnsureParentDir(string filePath)
	{
		if(string.IsNullOrWhiteSpace(filePath)){
			return;
		}
		var dir = Path.GetDirectoryName(filePath);
		if(string.IsNullOrWhiteSpace(dir)){
			return;
		}
		EnsureDir(dir);
	}

	protected override AppBuilder CustomizeAppBuilder(AppBuilder builder)
	{
		if(Di.SvcProviderOrNull is null){
			Di.SvcProvider = BuildActivityServices();
		}
		return base.CustomizeAppBuilder(builder)
			//用CPU渲染、否則在安卓端鍵盤隱藏又彈出有概率變黑 但仍可點擊交互
			.With(new AndroidPlatformOptions{

				RenderingMode = [
					Avalonia.AndroidRenderingMode.Software
				]
			})
			.With(new Avalonia.Media.FontManagerOptions{
				DefaultFamilyName = "serif"
			});
	}
}

file class ActivityKeyboardHost : IKeyboardHost
{
	public bool CanCommitText => false;

	public void HideKeyboard() { }

	public void CommitText(str text) { }
}
