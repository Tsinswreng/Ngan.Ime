namespace Ngan.Ime.Tsf.Registration;

using Tsinswreng.CsCore;
using Microsoft.Win32;
using System;
using System.IO;
using System.Runtime.InteropServices;

internal static partial class TsfRegistration {
	[Doc($$"""
	Executes the explicit register or unregister command.
	""")]
	internal static partial i32 Run(str[] Args) {
		// 先讀本版本的識別：CLSID／profile GUID／顯示名稱都由安裝器寫在 exe 旁邊。
		LoadIdentity();
		if (Args.Length == 2 && Args[0] == "--register") {
			return RegisterSystem(Args[1], null);
		}
		if (Args.Length == 4 && Args[0] == "--register" && Args[2] == "--name") {
			// 讓每個版本的顯示名稱不同：輸入法清單裏一眼看得出裝的是哪一版。
			return RegisterSystem(Args[1], Args[3]);
		}
		if (Args.Length == 1 && Args[0] == "--install-user") {
			return InstallUser();
		}
		if (Args.Length == 1 && Args[0] == "--uninstall-user") {
			return UninstallUser();
		}
		if (Args.Length == 1 && Args[0] == "--unregister-system") {
			return UnregisterSystem();
		}
		// 用法文字要和實際命令列一致：舊名是 POC 時期的 AvlnImeDemo，早已改成 Ngan。
		Console.Error.WriteLine("用法：Ngan.Ime.Tsf.Registration --register <Ngan.Ime.Tsf.dll> [--name <顯示名稱>]");
		Console.Error.WriteLine("      Ngan.Ime.Tsf.Registration --install-user");
		Console.Error.WriteLine("      Ngan.Ime.Tsf.Registration --uninstall-user");
		Console.Error.WriteLine("      Ngan.Ime.Tsf.Registration --unregister-system");
		return 2;
	}

	[Doc($$"""
	註冊機器層的簡體中文鍵盤 TIP，不修改提權帳戶的使用者輸入法清單。
	""")]
	private static partial i32 RegisterSystem(str ComHostPath, str? DisplayName) {
		var fullPath = Path.GetFullPath(ComHostPath);
		if (!File.Exists(fullPath)) {
			Console.Error.WriteLine("找不到 TSF COM host DLL：" + fullPath);
			return 2;
		}
		try {
			// 先完成 COM 與鍵盤分類，否則 profile 雖寫入登錄檔仍不會被 Windows 當成可選輸入法。
			WriteComRegistration(fullPath);
			VerifyTextServiceInterface();
			RegisterCategories();
			TryRemoveLegacyProfile();
			RegisterProfile(fullPath, DisplayName);
			Console.WriteLine("已完成 Ngan IME 的系統註冊。");
			return 0;
		} catch (Exception Exception) {
			Console.Error.WriteLine("Ngan IME 註冊失敗：" + Exception.Message);
			// 任一步驟失敗都清除本 POC 自己留下的狀態，讓下一次啟動可乾淨重試。
			TryRemoveProfile();
			TryUnregisterCategories();
			Registry.LocalMachine.DeleteSubKeyTree(GetComKeyPath(), false);
			return 1;
		}
	}

	[Doc($$"""
	將已註冊 profile 加到目前呼叫使用者的輸入法清單。
	""")]
	private static partial i32 InstallUser() {
		try {
			InstallTip(false);
			Console.WriteLine("已將 Ngan IME 加入目前使用者的簡體中文輸入法列表。");
			return 0;
		} catch (Exception Exception) {
			Console.Error.WriteLine("加入目前使用者輸入法列表失敗：" + Exception.Message);
			return 1;
		}
	}

	[Doc($$"""
	從目前呼叫使用者的輸入法清單移除 profile，不影響系統層註冊。
	""")]
	private static partial i32 UninstallUser() {
		try {
			InstallTip(true);
		} catch (Exception Exception) {
			Console.Error.WriteLine("移除使用者輸入法清單項目失敗：" + Exception.Message);
		}
		Console.WriteLine("已從目前使用者輸入法列表移除 Ngan IME。");
		return 0;
	}

	[Doc($$"""
	移除系統 profile、分類與 COM 對應；每個步驟都容忍半完成狀態。
	""")]
	private static partial i32 UnregisterSystem() {
		TryRemoveProfile();
		TryUnregisterCategories();
		Registry.LocalMachine.DeleteSubKeyTree(GetComKeyPath(), false);
		Console.WriteLine("已移除 Ngan IME 的系統輸入法註冊。");
		return 0;
	}

	[Doc($$"""
	使用 Windows profile manager 註冊無 .ime 替代鍵盤的純 TSF profile。
	""")]
	private static void RegisterProfile(str ComHostPath, str? DisplayName) {
		var profiles = CreateProfiles();
		try {
			ThrowOnFailure(profiles.RegisterProfile(in TextServiceClass, LanguageId, in LanguageProfile, (DisplayName ?? ProfileName), (u32)((DisplayName ?? ProfileName).Length * sizeof(char)), ComHostPath, (u32)(ComHostPath.Length * sizeof(char)), 0, nint.Zero, 0, true, 0), "ITfInputProcessorProfileMgr.RegisterProfile");
		} finally {
			Marshal.ReleaseComObject(profiles);
		}
	}

	[Doc($$"""
	盡力移除 profile，避免註冊失敗時因清理錯誤遮蔽原始原因。
	""")]
	private static void TryRemoveProfile() {
		try {
			var profiles = CreateProfiles();
			try {
				profiles.UnregisterProfile(in TextServiceClass, LanguageId, in LanguageProfile, 0);
			} finally {
				Marshal.ReleaseComObject(profiles);
			}
		} catch (COMException Exception) {
			Console.Error.WriteLine("移除 TSF profile 失敗：" + Exception.Message);
		}
	}

	[Doc($$"""
	盡力移除鍵盤分類，避免未完成註冊留下可被 Windows 誤識別的服務。
	""")]
	private static void TryUnregisterCategories() {
		try {
			UnregisterCategories();
		} catch (COMException Exception) {
			Console.Error.WriteLine("移除 TSF 分類失敗：" + Exception.Message);
		}
	}

	[Doc($$"""
	Writes the system-wide in-process COM mapping required by a Windows text service.
	*先確認這個 CLSID 不是別人的*：CLSID 與 profile GUID 由 `install.ps1` 每次安裝時生成、
	寫進同層的 `identity.txt`，再由這裡讀取。這支工具是以管理員身分執行的，若安裝目錄裡的
	`identity.txt` 被改過，就等於讓提權行程把**任意 CLSID** 指向**任意 DLL**（COM 劫持）。
	因此：目標 CLSID 底下已經有 `InprocServer32` 且指向別的 DLL 時，直接拒絕，不覆寫。
	同一版重複註冊（同一 CLSID、同一 DLL）仍然允許。
	""")]
	private static partial void WriteComRegistration(str ComHostPath) {
		var keyPath = GetComKeyPath();
		using (var existing = Registry.LocalMachine.OpenSubKey(keyPath)) {
			var Current = existing?.GetValue(str.Empty) as str;
			if (!str.IsNullOrEmpty(Current)
				&& !str.Equals(Current, ComHostPath, StringComparison.OrdinalIgnoreCase)) {
				throw new InvalidOperationException(
					"CLSID " + TextServiceClass.ToString("B") + " 已註冊給另一個 DLL：" + Current
					+ "。本次要註冊的是：" + ComHostPath
					+ "。這通常是 identity.txt 與實際安裝目錄不符（或被改過），拒絕覆寫。");
			}
		}
		using var Key = Registry.LocalMachine.CreateSubKey(keyPath);
		Key.SetValue(str.Empty, ComHostPath, RegistryValueKind.String);
		Key.SetValue("ThreadingModel", "Both", RegistryValueKind.String);
	}

	[Doc($$"""
	Verifies that COM exposes the exact interface TSF will request before profile registration.
	""")]
	private static partial void VerifyTextServiceInterface() {
		var hResult = CoCreateTextService(in TextServiceClass, nint.Zero, ClassContextAll, in TextInputProcessorInterface, out var textService);
		ThrowOnFailure(hResult, "CoCreateInstance(ITfTextInputProcessor)");
		Marshal.Release(textService);
	}

	[Doc($$"""
	Creates the Windows-managed TSF profile service.
	""")]
	private static partial ITfInputProcessorProfileMgr CreateProfiles() {
		var hResult = CoCreateInstance(in ProfileManagerClass, nint.Zero, ClassContextAll, in ProfileManagerInterface, out var profiles);
		ThrowOnFailure(hResult, "CoCreateInstance(ITfInputProcessorProfileMgr)");
		return profiles;
	}

	[Doc($$"""
	清理舊版錯誤使用 0409 建立的 profile；不存在時 Windows 會自然忽略。
	""")]
	private static partial void TryRemoveLegacyProfile() {
		var profiles = CreateProfiles();
		try {
			profiles.UnregisterProfile(in TextServiceClass, LegacyLanguageId, in LanguageProfile, 0);
		} finally {
			Marshal.ReleaseComObject(profiles);
		}
	}

	[Doc($$"""
	Creates the Windows-managed TSF category service.
	""")]
	private static partial ITfCategoryMgr CreateCategories() {
		var hResult = CoCreateCategoryManager(in CategoryManagerClass, nint.Zero, ClassContextAll, in CategoryManagerInterface, out var Categories);
		ThrowOnFailure(hResult, "CoCreateInstance(ITfCategoryMgr)");
		return Categories;
	}

	[Doc($$"""
	Registers the two categories Windows needs to enumerate this profile as a keyboard TIP.
	""")]
	private static partial void RegisterCategories() {
		var Categories = CreateCategories();
		try {
			ThrowOnFailure(Categories.RegisterCategory(in TextServiceClass, in TipCategory, in TextServiceClass), "ITfCategoryMgr.RegisterCategory(TIP)");
			ThrowOnFailure(Categories.RegisterCategory(in TextServiceClass, in KeyboardCategory, in TextServiceClass), "ITfCategoryMgr.RegisterCategory(Keyboard)");
		} finally {
			Marshal.ReleaseComObject(Categories);
		}
	}

	[Doc($$"""
	Removes only the two categories owned by this POC.
	""")]
	private static partial void UnregisterCategories() {
		var Categories = CreateCategories();
		try {
			Categories.UnregisterCategory(in TextServiceClass, in KeyboardCategory, in TextServiceClass);
			Categories.UnregisterCategory(in TextServiceClass, in TipCategory, in TextServiceClass);
		} finally {
			Marshal.ReleaseComObject(Categories);
		}
	}

	[Doc($$"""
	Builds the Windows-defined profile specifier used by InstallLayoutOrTip.
	""")]
	private static partial str GetTipSpecifier() {
		return LanguageId.ToString("X4") + ":" + TextServiceClass.ToString("B") + LanguageProfile.ToString("B");
	}

	[Doc($$"""
	Installs or removes this TIP in the current user's input-method list.
	""")]
	private static partial void InstallTip(bool IsUninstall) {
		const u32 UninstallFlag = 0x00000001;
		ThrowOnFailure(InstallLayoutOrTip(GetTipSpecifier(), IsUninstall ? UninstallFlag : 0), "InstallLayoutOrTip");
	}

	[Doc($$"""
	Returns the system COM registry path owned by this one class.
	""")]
	private static partial str GetComKeyPath() {
		return "Software\\Classes\\CLSID\\" + TextServiceClass.ToString("B") + "\\InprocServer32";
	}

	[Doc($$"""
	Converts a failed HRESULT to a standard managed COM exception.
	""")]
	private static partial void ThrowOnFailure(i32 HResult, str Operation) {
		if (HResult < 0) {
			throw new COMException(Operation + " 失敗，HRESULT: 0x" + HResult.ToString("X8"), HResult);
		}
	}

	[Doc($$"""
	本版本識別檔名；與 exe 同層，安裝時由 install.ps1 寫入。
	""")]
	private const str IdentityFileName = "identity.txt";

	[Doc($$"""
	讀取本版本的 CLSID／profile／顯示名稱。
	格式是逐行 `key=value`（不引入 JSON 序列化，AOT 下最省事）：
	`clsid`、`profile`、`name`。缺檔或某行壞掉就沿用內建值，
	這樣舊的註冊流程仍然可用。
	""")]
	private static partial void LoadIdentity() {
		try {
			var identityPath = System.IO.Path.Combine(AppContext.BaseDirectory, IdentityFileName);
			if (!File.Exists(identityPath)) {
				return;
			}
			foreach (var line in File.ReadAllLines(identityPath)) {
				var Index = line.IndexOf('=');
				if (Index <= 0) {
					continue;
				}
				var Key = line[..Index].Trim();
				var Value = line[(Index + 1)..].Trim();
				if (Value.Length == 0) {
					continue;
				}
				if (Key.Equals("clsid", StringComparison.OrdinalIgnoreCase) && Guid.TryParse(Value, out var Clsid)) {
					TextServiceClass = Clsid;
				} else if (Key.Equals("profile", StringComparison.OrdinalIgnoreCase) && Guid.TryParse(Value, out var Profile)) {
					LanguageProfile = Profile;
				} else if (Key.Equals("name", StringComparison.OrdinalIgnoreCase)) {
					ProfileName = Value;
				}
			}
		} catch (Exception Exception) {
			// 讀不到就用內建值，不能讓註冊因此失敗。
			Console.Error.WriteLine("讀取 identity.txt 失敗：" + Exception.Message);
		}
	}
}
