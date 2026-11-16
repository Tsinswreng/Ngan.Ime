namespace Ngan.Ime.Tsf.Registration;

using Tsinswreng.CsCore;
using Microsoft.Win32;
using System;
using System.Runtime.InteropServices;

[Doc($$"""
Windows TSF profile manager used to register a selectable input-method profile.
""")]
[ComImport]
[Guid("71C6E74C-0F28-11D8-A82A-00065B84435C")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface ITfInputProcessorProfileMgr {
	[Doc($$"""
	Activates one existing profile.
	""")]
	[PreserveSig]
	i32 ActivateProfile(u32 ProfileType, u16 LanguageId, in Guid ClassId, in Guid ProfileId, nint KeyboardLayout, u32 Flags);
	[Doc($$"""
	Deactivates one existing profile.
	""")]
	[PreserveSig]
	i32 DeactivateProfile(u32 ProfileType, u16 LanguageId, in Guid ClassId, in Guid ProfileId, nint KeyboardLayout, u32 Flags);
	[Doc($$"""
	Reads one profile into a native structure not required by this POC.
	""")]
	[PreserveSig]
	i32 GetProfile(u32 ProfileType, u16 LanguageId, in Guid ClassId, in Guid ProfileId, nint KeyboardLayout, out nint Profile);
	[Doc($$"""
	Enumerates profiles; retained to preserve the Windows vtable layout.
	""")]
	[PreserveSig]
	i32 EnumProfiles(u16 LanguageId, out nint Enumerator);
	[Doc($$"""
	Releases a registered text processor.
	""")]
	[PreserveSig]
	i32 ReleaseInputProcessor(in Guid ClassId, u32 Flags);
	[Doc($$"""
	Registers the profile metadata that Windows presents as an input method.
	""")]
	[PreserveSig]
	i32 RegisterProfile(in Guid ClassId, u16 LanguageId, in Guid ProfileId, [MarshalAs(UnmanagedType.LPWStr)] str Description, u32 DescriptionLength, [MarshalAs(UnmanagedType.LPWStr)] str IconPath, u32 IconPathLength, u32 IconIndex, nint SubstituteKeyboardLayout, u32 PreferredLayout, [MarshalAs(UnmanagedType.Bool)] bool IsEnabledByDefault, u32 Flags);
	[Doc($$"""
	Removes the registered profile metadata.
	""")]
	[PreserveSig]
	i32 UnregisterProfile(in Guid ClassId, u16 LanguageId, in Guid ProfileId, u32 Flags);
}

[Doc($$"""
Windows TSF 分類管理器；Windows 只會將標為鍵盤的 TIP 視為輸入法。
""")]
[ComImport]
[Guid("C3ACEFB5-F69D-4905-938F-FCADCF4BE830")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface ITfCategoryMgr {
	[Doc($$"""
	將文字服務歸入一個 TSF 分類。
	""")]
	[PreserveSig]
	i32 RegisterCategory(in Guid ClassId, in Guid CategoryId, in Guid ItemId);

	[Doc($$"""
	移除本 POC 擁有的一個 TSF 分類。
	""")]
	[PreserveSig]
	i32 UnregisterCategory(in Guid ClassId, in Guid CategoryId, in Guid ItemId);
}

[Doc($$"""
Per-user COM and TSF profile registration for the x64 proof of concept.
""")]
internal static partial class TsfRegistration {
	[Doc($$"""
	COM class identity exported by the C# TSF assembly.
	""")]
	private static Guid TextServiceClass = new("9B7E5C42-3A1D-4F6E-8B2A-5C1D0E9F7A31");
	[Doc($$"""
	Stable TSF language-profile identity.
	""")]
	private static Guid LanguageProfile = new("D3A1C7E2-6B4F-4E8A-9C15-7F2B8D4E6A90");
	[Doc($$"""
	簡體中文語言識別碼；必須與目前使用者的中文輸入法清單一致。
	""")]
	private const u16 LanguageId = 0x0804;
	[Doc($$"""
	第一版錯誤建立的英文 profile 語言識別碼；僅用於升級時清理遺留資料。
	""")]
	private const u16 LegacyLanguageId = 0x0409;
	[Doc($$"""
	Display name presented by Windows input settings.
	""")]
	private static str ProfileName = "Ngan IME";
	[Doc($$"""
	CLSID of the Windows TSF profile-manager COM class.
	""")]
	private static readonly Guid ProfileManagerClass = new("33C53A50-F456-4884-B049-85FD643ECFED");
	[Doc($$"""
	IID of the modern TSF profile registration interface.
	""")]
	private static readonly Guid ProfileManagerInterface = new("71C6E74C-0F28-11D8-A82A-00065B84435C");
	[Doc($$"""
	CLSID of the Windows TSF category manager.
	""")]
	private static readonly Guid CategoryManagerClass = new("A4B544A1-438D-4B41-9325-869523E2D6C7");
	[Doc($$"""
	IID of the Windows TSF category manager.
	""")]
	private static readonly Guid CategoryManagerInterface = new("C3ACEFB5-F69D-4905-938F-FCADCF4BE830");
	[Doc($$"""
	TSF category identifying one component as a text input processor.
	""")]
	private static readonly Guid TipCategory = new("534C48C1-0607-4098-A521-4FC899C73E90");
	[Doc($$"""
	TSF category identifying one text input processor as a keyboard.
	""")]
	private static readonly Guid KeyboardCategory = new("34745C63-B2F0-4784-8B67-5E12C8701A31");
	[Doc($$"""
	IID that every TSF text service must expose to Windows.
	""")]
	private static readonly Guid TextInputProcessorInterface = new("AA80E7F7-2021-11D2-93E0-0060B067B86E");
	[Doc($$"""
	Allows the system to create the in-process TSF profile manager.
	""")]
	private const u32 ClassContextAll = 0x17;
	[Doc($$"""
	Creates the Windows TSF profile manager through the standard COM factory.
	""")]
	[DllImport("ole32.dll")]
	private static extern i32 CoCreateInstance(in Guid ClassId, nint Outer, u32 ClassContext, in Guid InterfaceId, [MarshalAs(UnmanagedType.Interface)] out ITfInputProcessorProfileMgr Profiles);
	[Doc($$"""
	以指定 TSF 介面直接啟用文字服務，供註冊前驗證使用。
	""")]
	[DllImport("ole32.dll", EntryPoint = "CoCreateInstance")]
	private static extern i32 CoCreateTextService(in Guid ClassId, nint Outer, u32 ClassContext, in Guid InterfaceId, out nint TextService);
	[Doc($$"""
	Creates the Windows TSF category manager through the standard COM factory.
	""")]
	[DllImport("ole32.dll", EntryPoint = "CoCreateInstance")]
	private static extern i32 CoCreateCategoryManager(in Guid ClassId, nint Outer, u32 ClassContext, in Guid InterfaceId, [MarshalAs(UnmanagedType.Interface)] out ITfCategoryMgr Categories);
	[Doc($$"""
	將一個已註冊 TSF profile 安裝到呼叫使用者的語言與輸入法清單。
	""")]
	[DllImport("input.dll", CharSet = CharSet.Unicode, ExactSpelling = true)]
	private static extern i32 InstallLayoutOrTip(str TipSpecifier, u32 Flags);

	[Doc($$"""
	Executes the explicit register or unregister command.
	""")]
	internal static partial i32 Run(str[] Args);

	[Doc($$"""
	Registers the machine-wide COM server, keyboard categories, and Chinese TSF profile.
	""")]
	private static partial i32 RegisterSystem(str ComHostPath, str? DisplayName);

	[Doc($$"""
	Installs the already-registered profile into the invoking user's input-method list.
	""")]
	private static partial i32 InstallUser();

	[Doc($$"""
	Removes the installed profile from the invoking user's input-method list.
	""")]
	private static partial i32 UninstallUser();

	[Doc($$"""
	Removes all machine-wide registration state owned by this POC.
	""")]
	private static partial i32 UnregisterSystem();

	[Doc($$"""
	Writes the current-user COM server mapping used by the .NET COM host.
	""")]
	private static partial void WriteComRegistration(str ComHostPath);

	[Doc($$"""
	Verifies that the registered COM class exposes the mandatory TSF text-service interface.
	""")]
	private static partial void VerifyTextServiceInterface();

	[Doc($$"""
	Creates the Windows-managed TSF input-processor profile service.
	""")]
	private static partial ITfInputProcessorProfileMgr CreateProfiles();

	[Doc($$"""
	Removes the obsolete English profile written by the first POC registration implementation.
	""")]
	private static partial void TryRemoveLegacyProfile();

	[Doc($$"""
	Creates the Windows-managed TSF category service.
	""")]
	private static partial ITfCategoryMgr CreateCategories();

	[Doc($$"""
	Registers the minimum categories required for Windows to expose this TIP as a keyboard.
	""")]
	private static partial void RegisterCategories();

	[Doc($$"""
	Removes the categories registered by {{nameof(RegisterCategories)}}.
	""")]
	private static partial void UnregisterCategories();

	[Doc($$"""
	Returns the `0804:{CLSID}{Profile}` identifier consumed by Windows input.dll.
	""")]
	private static partial str GetTipSpecifier();

	[Doc($$"""
	Installs or removes this profile from the current user's Windows input-method list.
	""")]
	private static partial void InstallTip(bool IsUninstall);

	[Doc($$"""
	從安裝目錄的 `identity.txt` 讀取本版本的 CLSID、profile GUID 與顯示名稱。
	版本化安裝＝每次安裝都是一個獨立輸入法：CLSID 與 profile 都由安裝器生成並寫在這裡，
	註冊、加入清單、卸載都讀同一份，故各版本互不相干，可單獨移除。
	檔案不存在時退回內建的那組 GUID，行為與舊版相同。
	""")]
	private static partial void LoadIdentity();

	[Doc($$"""
	Returns the exact current-user registry path owned by this POC.
	""")]
	private static partial str GetComKeyPath();

	[Doc($$"""
	Converts a failed native result into an exception containing the failed operation name.
	""")]
	private static partial void ThrowOnFailure(i32 HResult, str Operation);
}
