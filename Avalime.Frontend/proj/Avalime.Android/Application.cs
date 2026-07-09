using Android.App;
using Android.Runtime;
using Avalime.Core.Infra;
using Avalime.Core.Infra.Log;
using Avalime.Core.Keys;
using Avalime.Rime;
using Avalime.UI;
using Avalime.UI.Views.CandidatesBar;
using Avalime.UI.Views.Clipboard;
using Avalime.UI.Views.Ime;
using Avalime.UI.Views.Input;
using Avalime.UI.Views.Key;
using Avalime.UI.Views.KeyBoard;
using Avalime.UI.Views.RimeLog;
using Avalime.UI.Views.ToolBar;
using Avalonia;
using Avalonia.Android;
using Microsoft.Extensions.DependencyInjection;
using Tsinswreng.CsCfg;
using Tsinswreng.CsTools;
using System.IO;
using AssetManager = Android.Content.Res.AssetManager;
namespace Avalime.Android;

[Application]
public class Application : AvaloniaAndroidApplication<App>
{
	const string LocalRimeSoName = "librime.so";

	public Application(nint javaReference, JniHandleOwnership transfer)
		: base(javaReference, transfer)
	{
		AppLog.Inst.InnerLogger = new AndroidLogger("Avalime");
		InitCfg(global::Android.App.Application.Context);
		SetupSoFiles(global::Android.App.Application.Context);
	}

	static void InitCfg(global::Android.Content.Context ctx)
	{
		var externalDir = ctx.GetExternalFilesDir(null)?.AbsolutePath
			?? throw new InvalidOperationException("Cannot get ExternalFilesDir");
		EnsureDir(externalDir);

		var roCfgPath = Path.Combine(externalDir, "Avalime.Ro.jsonc");
		var dfltRwCfgPath = Path.Combine(externalDir, "Avalime.Rw.jsonc");
		if(!File.Exists(roCfgPath)){
			EnsureAssetFile(ctx.Assets!, "Avalime.Ro.jsonc", roCfgPath, overwrite: false);
		}

		var dualSrcCfg = AppCfg.Inst;
		var roCfg = new JsonFileCfgAccessor();
		dualSrcCfg.RoCfg = roCfg;
		roCfg.FromFile(roCfgPath);

		var rwCfgPath = KeysCfg.RwCfgPath.GetFrom(dualSrcCfg) ?? dfltRwCfgPath;
		if(!Path.IsPathRooted(rwCfgPath)){
			rwCfgPath = Path.Combine(externalDir, rwCfgPath);
		}
		if(!File.Exists(rwCfgPath)){
			EnsureAssetFile(ctx.Assets!, "Avalime.Rw.jsonc", rwCfgPath, overwrite: false);
		}

		var rwCfg = new JsonFileCfgAccessor();
		dualSrcCfg.RwCfg = rwCfg;
		rwCfg.FromFile(rwCfgPath);
	}

	static string SetupSoFiles(global::Android.Content.Context ctx)
	{
		var internalDir = ctx.FilesDir!.AbsolutePath!;
		var externalDir = ctx.GetExternalFilesDir(null)?.AbsolutePath
			?? throw new InvalidOperationException("Cannot get ExternalFilesDir");
		EnsureDir(externalDir);
		string[] passthroughSoFiles = ["libc++_shared.so", "libCsRimeLua.so"];

		foreach (var so in passthroughSoFiles)
		{
			var dst = System.IO.Path.Combine(internalDir, so);
			// 優先：內部目錄已有且非空就不複製（開發者用 run-as cp 放進去的）
			if (System.IO.File.Exists(dst) && new FileInfo(dst).Length > 0)
			{
				AppLog.Info("[Avalime] already in place: " + dst);
				continue;
			}
			if(File.Exists(dst)){
				try{
					File.Delete(dst);
				}catch(Exception ex){
					AppLog.Warn("[Avalime] failed to delete invalid so " + dst + ": " + ex.Message);
				}
			}
			// 嘗試從 /sdcard/rime/ 複製（需要儲存權限，現代 Android 通常失敗）
			var src = System.IO.Path.Combine(externalDir, so);
			try
			{
				if (System.IO.File.Exists(src))
				{
					System.IO.File.Copy(src, dst, overwrite: true);
					AppLog.Info("[Avalime] copied " + src + " -> " + dst);
				}
			}
			catch (System.Exception ex)
			{
				AppLog.Warn("[Avalime] copy skipped for " + so + ": " + ex.Message);
			}
		}

		var localRime = System.IO.Path.Combine(internalDir, LocalRimeSoName);
		ToolFile.EnsureFile(localRime);
		try
		{
			ExtractAssetFile(ctx.Assets!, "rime/librime.bin", localRime);
			AppLog.Info("[Avalime] extracted asset rime/librime.bin -> " + localRime);
		}
		catch (System.Exception ex)
		{
			AppLog.Error(ex, "[Avalime] extract asset rime failed");
		}

		// preload libc++_shared.so, required by librime.so
		var libcpp = System.IO.Path.Combine(internalDir, "libc++_shared.so");
		if (System.IO.File.Exists(libcpp) && new FileInfo(libcpp).Length > 0)
		{
			try
			{
				System.Runtime.InteropServices.NativeLibrary.Load(libcpp);
				AppLog.Info("[Avalime] preloaded libc++_shared.so");
			}
			catch (System.Exception ex)
			{
				AppLog.Error(ex, "[Avalime] preload libc++_shared.so failed");
			}
		}

		var userDataDir = KeysCfg.Librime.RimeTraits.user_data_dir.GetFrom(AppCfg.Inst);
		if(string.IsNullOrWhiteSpace(userDataDir)){
			userDataDir = Path.Combine(externalDir, "UserData");
		}

		EnsureDir(userDataDir);
		// 配置優先；只有明確未配置時，宿主才補當前設備上的默認值。
		EnsureCfgOrDefault(KeysCfg.Librime.DllPath, localRime);
		EnsureCfgOrDefault(KeysCfg.Librime.RimeTraits.user_data_dir, userDataDir);
		EnsureCfgOrDefault(KeysCfg.Librime.RimeTraits.distribution_code_name, "Tsinswreng.Avalime.Android");
		EnsureCfgOrDefault(KeysCfg.Librime.RimeTraits.app_name, ctx.PackageName ?? "rime.avalime");

		return internalDir;
	}

	/// <summary>
	/// 雙源配置以顯式配置爲準；只有缺省值爲空時，才由宿主補上可運行的默認值。
	/// </summary>
	static void EnsureCfgOrDefault(ICfgNode<str> Key, str DefaultValue)
	{
		var Cur = Key.GetFrom(AppCfg.Inst) ?? "";
		if(!string.IsNullOrWhiteSpace(Cur)){
			return;
		}
		AppCfg.Inst.Set(Key, DefaultValue);
	}

	static IServiceProvider BuildAndroidActivityServices()
	{
		var services = new ServiceCollection();
		services.AddSingleton<RimeSetup>(_ => RimeSetup.Inst);
		services.AddSingleton<IRimeLogSource>(_ => RimeLogStore.Inst);
		services.AddSingleton<IImeKeyProcessor, RimeKeyProcessor>();
		services.AddSingleton<IOsKeyProcessor, AndroidStubOsKeyProcessor>();
		services.AddSingleton<IKeyboardHost, ActivityKeyboardHost>();
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
		services.AddSingleton<Microsoft.Extensions.Logging.ILogger>(_ => AppLog.Inst);
		return services.BuildServiceProvider(new ServiceProviderOptions { ValidateOnBuild = false, ValidateScopes = false });
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
		if(DiTryNeedsInit()){
			Di.SvcProvider = BuildAndroidActivityServices();
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

	static bool DiTryNeedsInit(){
		try{
			_ = Di.SvcProvider;
			return false;
		}catch(InvalidOperationException){
			return true;
		}
	}
}

file class ActivityKeyboardHost : IKeyboardHost
{
	public void HideKeyboard() { }

	public void CommitText(str text) { }
}
