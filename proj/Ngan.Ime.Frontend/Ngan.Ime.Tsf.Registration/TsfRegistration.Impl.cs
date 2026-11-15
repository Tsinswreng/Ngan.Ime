namespace Ngan.Ime.Tsf.Registration;

using Microsoft.Win32;
using System;
using System.IO;
using System.Runtime.InteropServices;

internal static partial class TsfRegistration {
    /// <summary>Executes the explicit register or unregister command.</summary>
    internal static partial int Run(string[] Args) {
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

    /// <summary>註冊機器層的簡體中文鍵盤 TIP，不修改提權帳戶的使用者輸入法清單。</summary>
    private static partial int RegisterSystem(string ComHostPath, string? DisplayName) {
        var FullPath = Path.GetFullPath(ComHostPath);
        if (!File.Exists(FullPath)) {
            Console.Error.WriteLine("找不到 TSF COM host DLL：" + FullPath);
            return 2;
        }
        try {
            // 先完成 COM 與鍵盤分類，否則 profile 雖寫入登錄檔仍不會被 Windows 當成可選輸入法。
            WriteComRegistration(FullPath);
            VerifyTextServiceInterface();
            RegisterCategories();
            TryRemoveLegacyProfile();
            RegisterProfile(FullPath, DisplayName);
            Console.WriteLine("已完成 Ngan IME 的系統註冊。");
            return 0;
        }
        catch (Exception Exception) {
            Console.Error.WriteLine("Ngan IME 註冊失敗：" + Exception.Message);
            // 任一步驟失敗都清除本 POC 自己留下的狀態，讓下一次啟動可乾淨重試。
            TryRemoveProfile();
            TryUnregisterCategories();
            Registry.LocalMachine.DeleteSubKeyTree(GetComKeyPath(), false);
            return 1;
        }
    }

    /// <summary>將已註冊 profile 加到目前呼叫使用者的輸入法清單。</summary>
    private static partial int InstallUser() {
        try {
            InstallTip(false);
            Console.WriteLine("已將 Ngan IME 加入目前使用者的簡體中文輸入法列表。");
            return 0;
        }
        catch (Exception Exception) {
            Console.Error.WriteLine("加入目前使用者輸入法列表失敗：" + Exception.Message);
            return 1;
        }
    }

    /// <summary>從目前呼叫使用者的輸入法清單移除 profile，不影響系統層註冊。</summary>
    private static partial int UninstallUser() {
        try {
            InstallTip(true);
        }
        catch (Exception Exception) {
            Console.Error.WriteLine("移除使用者輸入法清單項目失敗：" + Exception.Message);
        }
        Console.WriteLine("已從目前使用者輸入法列表移除 Ngan IME。");
        return 0;
    }

    /// <summary>移除系統 profile、分類與 COM 對應；每個步驟都容忍半完成狀態。</summary>
    private static partial int UnregisterSystem() {
        TryRemoveProfile();
        TryUnregisterCategories();
        Registry.LocalMachine.DeleteSubKeyTree(GetComKeyPath(), false);
        Console.WriteLine("已移除 Ngan IME 的系統輸入法註冊。");
        return 0;
    }

    /// <summary>使用 Windows profile manager 註冊無 .ime 替代鍵盤的純 TSF profile。</summary>
    private static void RegisterProfile(string ComHostPath, string? DisplayName) {
        var Profiles = CreateProfiles();
        try {
            ThrowOnFailure(Profiles.RegisterProfile(in TextServiceClass, LanguageId, in LanguageProfile, (DisplayName ?? ProfileName), (uint)((DisplayName ?? ProfileName).Length * sizeof(char)), ComHostPath, (uint)(ComHostPath.Length * sizeof(char)), 0, nint.Zero, 0, true, 0), "ITfInputProcessorProfileMgr.RegisterProfile");
        }
        finally {
            Marshal.ReleaseComObject(Profiles);
        }
    }

    /// <summary>盡力移除 profile，避免註冊失敗時因清理錯誤遮蔽原始原因。</summary>
    private static void TryRemoveProfile() {
        try {
            var Profiles = CreateProfiles();
            try {
                Profiles.UnregisterProfile(in TextServiceClass, LanguageId, in LanguageProfile, 0);
            }
            finally {
                Marshal.ReleaseComObject(Profiles);
            }
        }
        catch (COMException Exception) {
            Console.Error.WriteLine("移除 TSF profile 失敗：" + Exception.Message);
        }
    }

    /// <summary>盡力移除鍵盤分類，避免未完成註冊留下可被 Windows 誤識別的服務。</summary>
    private static void TryUnregisterCategories() {
        try {
            UnregisterCategories();
        }
        catch (COMException Exception) {
            Console.Error.WriteLine("移除 TSF 分類失敗：" + Exception.Message);
        }
    }

    /// <summary>Writes the system-wide in-process COM mapping required by a Windows text service.</summary>
    private static partial void WriteComRegistration(string ComHostPath) {
        using var Key = Registry.LocalMachine.CreateSubKey(GetComKeyPath());
        Key.SetValue(string.Empty, ComHostPath, RegistryValueKind.String);
        Key.SetValue("ThreadingModel", "Both", RegistryValueKind.String);
    }

    /// <summary>Verifies that COM exposes the exact interface TSF will request before profile registration.</summary>
    private static partial void VerifyTextServiceInterface() {
        var HResult = CoCreateTextService(in TextServiceClass, nint.Zero, ClassContextAll, in TextInputProcessorInterface, out var TextService);
        ThrowOnFailure(HResult, "CoCreateInstance(ITfTextInputProcessor)");
        Marshal.Release(TextService);
    }

    /// <summary>Creates the Windows-managed TSF profile service.</summary>
    private static partial ITfInputProcessorProfileMgr CreateProfiles() {
        var HResult = CoCreateInstance(in ProfileManagerClass, nint.Zero, ClassContextAll, in ProfileManagerInterface, out var Profiles);
        ThrowOnFailure(HResult, "CoCreateInstance(ITfInputProcessorProfileMgr)");
        return Profiles;
    }

    /// <summary>清理舊版錯誤使用 0409 建立的 profile；不存在時 Windows 會自然忽略。</summary>
    private static partial void TryRemoveLegacyProfile() {
        var Profiles = CreateProfiles();
        try {
            Profiles.UnregisterProfile(in TextServiceClass, LegacyLanguageId, in LanguageProfile, 0);
        }
        finally {
            Marshal.ReleaseComObject(Profiles);
        }
    }

    /// <summary>Creates the Windows-managed TSF category service.</summary>
    private static partial ITfCategoryMgr CreateCategories() {
        var HResult = CoCreateCategoryManager(in CategoryManagerClass, nint.Zero, ClassContextAll, in CategoryManagerInterface, out var Categories);
        ThrowOnFailure(HResult, "CoCreateInstance(ITfCategoryMgr)");
        return Categories;
    }

    /// <summary>Registers the two categories Windows needs to enumerate this profile as a keyboard TIP.</summary>
    private static partial void RegisterCategories() {
        var Categories = CreateCategories();
        try {
            ThrowOnFailure(Categories.RegisterCategory(in TextServiceClass, in TipCategory, in TextServiceClass), "ITfCategoryMgr.RegisterCategory(TIP)");
            ThrowOnFailure(Categories.RegisterCategory(in TextServiceClass, in KeyboardCategory, in TextServiceClass), "ITfCategoryMgr.RegisterCategory(Keyboard)");
        }
        finally {
            Marshal.ReleaseComObject(Categories);
        }
    }

    /// <summary>Removes only the two categories owned by this POC.</summary>
    private static partial void UnregisterCategories() {
        var Categories = CreateCategories();
        try {
            Categories.UnregisterCategory(in TextServiceClass, in KeyboardCategory, in TextServiceClass);
            Categories.UnregisterCategory(in TextServiceClass, in TipCategory, in TextServiceClass);
        }
        finally {
            Marshal.ReleaseComObject(Categories);
        }
    }

    /// <summary>Builds the Windows-defined profile specifier used by InstallLayoutOrTip.</summary>
    private static partial string GetTipSpecifier() {
        return LanguageId.ToString("X4") + ":" + TextServiceClass.ToString("B") + LanguageProfile.ToString("B");
    }

    /// <summary>Installs or removes this TIP in the current user's input-method list.</summary>
    private static partial void InstallTip(bool IsUninstall) {
        const uint UninstallFlag = 0x00000001;
        ThrowOnFailure(InstallLayoutOrTip(GetTipSpecifier(), IsUninstall ? UninstallFlag : 0), "InstallLayoutOrTip");
    }

    /// <summary>Returns the system COM registry path owned by this one class.</summary>
    private static partial string GetComKeyPath() {
        return "Software\\Classes\\CLSID\\" + TextServiceClass.ToString("B") + "\\InprocServer32";
    }

    /// <summary>Converts a failed HRESULT to a standard managed COM exception.</summary>
    private static partial void ThrowOnFailure(int HResult, string Operation) {
        if (HResult < 0) {
            throw new COMException(Operation + " 失敗，HRESULT: 0x" + HResult.ToString("X8"), HResult);
        }
    }

    /// <summary>本版本識別檔名；與 exe 同層，安裝時由 install.ps1 寫入。</summary>
    private const string IdentityFileName = "identity.txt";

    /// <summary>
    /// 讀取本版本的 CLSID／profile／顯示名稱。
    /// </summary>
    /// <remarks>
    /// 格式是逐行 `key=value`（不引入 JSON 序列化，AOT 下最省事）：
    /// `clsid`、`profile`、`name`。缺檔或某行壞掉就沿用內建值，
    /// 這樣舊的註冊流程仍然可用。
    /// </remarks>
    private static partial void LoadIdentity() {
        try {
            var IdentityPath = System.IO.Path.Combine(AppContext.BaseDirectory, IdentityFileName);
            if (!File.Exists(IdentityPath)) {
                return;
            }
            foreach (var Line in File.ReadAllLines(IdentityPath)) {
                var Index = Line.IndexOf('=');
                if (Index <= 0) {
                    continue;
                }
                var Key = Line[..Index].Trim();
                var Value = Line[(Index + 1)..].Trim();
                if (Value.Length == 0) {
                    continue;
                }
                if (Key.Equals("clsid", StringComparison.OrdinalIgnoreCase) && Guid.TryParse(Value, out var Clsid)) {
                    TextServiceClass = Clsid;
                }
                else if (Key.Equals("profile", StringComparison.OrdinalIgnoreCase) && Guid.TryParse(Value, out var Profile)) {
                    LanguageProfile = Profile;
                }
                else if (Key.Equals("name", StringComparison.OrdinalIgnoreCase)) {
                    ProfileName = Value;
                }
            }
        }
        catch (Exception Exception) {
            // 讀不到就用內建值，不能讓註冊因此失敗。
            Console.Error.WriteLine("讀取 identity.txt 失敗：" + Exception.Message);
        }
    }
}
