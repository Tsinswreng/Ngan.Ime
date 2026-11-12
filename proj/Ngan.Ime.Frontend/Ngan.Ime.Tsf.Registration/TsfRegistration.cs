namespace Ngan.Ime.Tsf.Registration;

using Microsoft.Win32;
using System;
using System.Runtime.InteropServices;

/// <summary>Windows TSF profile manager used to register a selectable input-method profile.</summary>
[ComImport]
[Guid("71C6E74C-0F28-11D8-A82A-00065B84435C")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface ITfInputProcessorProfileMgr {
    /// <summary>Activates one existing profile.</summary>
    [PreserveSig]
    int ActivateProfile(uint ProfileType, ushort LanguageId, in Guid ClassId, in Guid ProfileId, nint KeyboardLayout, uint Flags);
    /// <summary>Deactivates one existing profile.</summary>
    [PreserveSig]
    int DeactivateProfile(uint ProfileType, ushort LanguageId, in Guid ClassId, in Guid ProfileId, nint KeyboardLayout, uint Flags);
    /// <summary>Reads one profile into a native structure not required by this POC.</summary>
    [PreserveSig]
    int GetProfile(uint ProfileType, ushort LanguageId, in Guid ClassId, in Guid ProfileId, nint KeyboardLayout, out nint Profile);
    /// <summary>Enumerates profiles; retained to preserve the Windows vtable layout.</summary>
    [PreserveSig]
    int EnumProfiles(ushort LanguageId, out nint Enumerator);
    /// <summary>Releases a registered text processor.</summary>
    [PreserveSig]
    int ReleaseInputProcessor(in Guid ClassId, uint Flags);
    /// <summary>Registers the profile metadata that Windows presents as an input method.</summary>
    [PreserveSig]
    int RegisterProfile(in Guid ClassId, ushort LanguageId, in Guid ProfileId, [MarshalAs(UnmanagedType.LPWStr)] string Description, uint DescriptionLength, [MarshalAs(UnmanagedType.LPWStr)] string IconPath, uint IconPathLength, uint IconIndex, nint SubstituteKeyboardLayout, uint PreferredLayout, [MarshalAs(UnmanagedType.Bool)] bool IsEnabledByDefault, uint Flags);
    /// <summary>Removes the registered profile metadata.</summary>
    [PreserveSig]
    int UnregisterProfile(in Guid ClassId, ushort LanguageId, in Guid ProfileId, uint Flags);
}

/// <summary>Windows TSF 分類管理器；Windows 只會將標為鍵盤的 TIP 視為輸入法。</summary>
[ComImport]
[Guid("C3ACEFB5-F69D-4905-938F-FCADCF4BE830")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface ITfCategoryMgr {
    /// <summary>將文字服務歸入一個 TSF 分類。</summary>
    [PreserveSig]
    int RegisterCategory(in Guid ClassId, in Guid CategoryId, in Guid ItemId);

    /// <summary>移除本 POC 擁有的一個 TSF 分類。</summary>
    [PreserveSig]
    int UnregisterCategory(in Guid ClassId, in Guid CategoryId, in Guid ItemId);
}

/// <summary>Per-user COM and TSF profile registration for the x64 proof of concept.</summary>
internal static partial class TsfRegistration {
    /// <summary>COM class identity exported by the C# TSF assembly.</summary>
    private static readonly Guid TextServiceClass = new("9B7E5C42-3A1D-4F6E-8B2A-5C1D0E9F7A31");
    /// <summary>Stable TSF language-profile identity.</summary>
    private static readonly Guid LanguageProfile = new("D3A1C7E2-6B4F-4E8A-9C15-7F2B8D4E6A90");
    /// <summary>簡體中文語言識別碼；必須與目前使用者的中文輸入法清單一致。</summary>
    private const ushort LanguageId = 0x0804;
    /// <summary>第一版錯誤建立的英文 profile 語言識別碼；僅用於升級時清理遺留資料。</summary>
    private const ushort LegacyLanguageId = 0x0409;
    /// <summary>Display name presented by Windows input settings.</summary>
    private const string ProfileName = "Ngan IME";
    /// <summary>CLSID of the Windows TSF profile-manager COM class.</summary>
    private static readonly Guid ProfileManagerClass = new("33C53A50-F456-4884-B049-85FD643ECFED");
    /// <summary>IID of the modern TSF profile registration interface.</summary>
    private static readonly Guid ProfileManagerInterface = new("71C6E74C-0F28-11D8-A82A-00065B84435C");
    /// <summary>CLSID of the Windows TSF category manager.</summary>
    private static readonly Guid CategoryManagerClass = new("A4B544A1-438D-4B41-9325-869523E2D6C7");
    /// <summary>IID of the Windows TSF category manager.</summary>
    private static readonly Guid CategoryManagerInterface = new("C3ACEFB5-F69D-4905-938F-FCADCF4BE830");
    /// <summary>TSF category identifying one component as a text input processor.</summary>
    private static readonly Guid TipCategory = new("534C48C1-0607-4098-A521-4FC899C73E90");
    /// <summary>TSF category identifying one text input processor as a keyboard.</summary>
    private static readonly Guid KeyboardCategory = new("34745C63-B2F0-4784-8B67-5E12C8701A31");
    /// <summary>IID that every TSF text service must expose to Windows.</summary>
    private static readonly Guid TextInputProcessorInterface = new("AA80E7F7-2021-11D2-93E0-0060B067B86E");
    /// <summary>Allows the system to create the in-process TSF profile manager.</summary>
    private const uint ClassContextAll = 0x17;
    /// <summary>Creates the Windows TSF profile manager through the standard COM factory.</summary>
    [DllImport("ole32.dll")]
    private static extern int CoCreateInstance(in Guid ClassId, nint Outer, uint ClassContext, in Guid InterfaceId, [MarshalAs(UnmanagedType.Interface)] out ITfInputProcessorProfileMgr Profiles);
    /// <summary>以指定 TSF 介面直接啟用文字服務，供註冊前驗證使用。</summary>
    [DllImport("ole32.dll", EntryPoint = "CoCreateInstance")]
    private static extern int CoCreateTextService(in Guid ClassId, nint Outer, uint ClassContext, in Guid InterfaceId, out nint TextService);
    /// <summary>Creates the Windows TSF category manager through the standard COM factory.</summary>
    [DllImport("ole32.dll", EntryPoint = "CoCreateInstance")]
    private static extern int CoCreateCategoryManager(in Guid ClassId, nint Outer, uint ClassContext, in Guid InterfaceId, [MarshalAs(UnmanagedType.Interface)] out ITfCategoryMgr Categories);
    /// <summary>將一個已註冊 TSF profile 安裝到呼叫使用者的語言與輸入法清單。</summary>
    [DllImport("input.dll", CharSet = CharSet.Unicode, ExactSpelling = true)]
    private static extern int InstallLayoutOrTip(string TipSpecifier, uint Flags);

    /// <summary>Executes the explicit register or unregister command.</summary>
    internal static partial int Run(string[] Args);

    /// <summary>Registers the machine-wide COM server, keyboard categories, and Chinese TSF profile.</summary>
    private static partial int RegisterSystem(string ComHostPath);

    /// <summary>Installs the already-registered profile into the invoking user's input-method list.</summary>
    private static partial int InstallUser();

    /// <summary>Removes the installed profile from the invoking user's input-method list.</summary>
    private static partial int UninstallUser();

    /// <summary>Removes all machine-wide registration state owned by this POC.</summary>
    private static partial int UnregisterSystem();

    /// <summary>Writes the current-user COM server mapping used by the .NET COM host.</summary>
    private static partial void WriteComRegistration(string ComHostPath);

    /// <summary>Verifies that the registered COM class exposes the mandatory TSF text-service interface.</summary>
    private static partial void VerifyTextServiceInterface();

    /// <summary>Creates the Windows-managed TSF input-processor profile service.</summary>
    private static partial ITfInputProcessorProfileMgr CreateProfiles();

    /// <summary>Removes the obsolete English profile written by the first POC registration implementation.</summary>
    private static partial void TryRemoveLegacyProfile();

    /// <summary>Creates the Windows-managed TSF category service.</summary>
    private static partial ITfCategoryMgr CreateCategories();

    /// <summary>Registers the minimum categories required for Windows to expose this TIP as a keyboard.</summary>
    private static partial void RegisterCategories();

    /// <summary>Removes the categories registered by <see cref="RegisterCategories"/>.</summary>
    private static partial void UnregisterCategories();

    /// <summary>Returns the `0804:{CLSID}{Profile}` identifier consumed by Windows input.dll.</summary>
    private static partial string GetTipSpecifier();

    /// <summary>Installs or removes this profile from the current user's Windows input-method list.</summary>
    private static partial void InstallTip(bool IsUninstall);

    /// <summary>Returns the exact current-user registry path owned by this POC.</summary>
    private static partial string GetComKeyPath();

    /// <summary>Converts a failed native result into an exception containing the failed operation name.</summary>
    private static partial void ThrowOnFailure(int HResult, string Operation);
}
