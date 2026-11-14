' Start the Ngan IME server with no visible window.
' Launched by double click, or by a copy in the Startup folder.
' If the server is already running, this script does nothing: two servers
' would share the same Rime user data directory.
Option Explicit

Dim Fso, Root, DirName, ExePath, Wmi, Procs, Shell

Set Fso = CreateObject("Scripting.FileSystemObject")

' The 2026-11-14 rename changed the folder to ngan-server-x86.
' Accept the old name too, so this launcher works before and after redeploy.
Root = "C:\NganImeVm\apps\"
ExePath = ""
For Each DirName In Array("ngan-server-x86", "ngan-host-x86")
	If Fso.FileExists(Root & DirName & "\Ngan.Ime.Windows.exe") Then
		ExePath = Root & DirName & "\Ngan.Ime.Windows.exe"
		Exit For
	End If
Next

If ExePath = "" Then
	MsgBox "Ngan.Ime.Windows.exe was not found under " & Root & ". Deploy first.", 16, "Ngan IME"
	WScript.Quit 1
End If

Set Wmi = GetObject("winmgmts:\\.\root\cimv2")
Set Procs = Wmi.ExecQuery("select ProcessId from Win32_Process where Name='Ngan.Ime.Windows.exe'")
If Procs.Count > 0 Then
	WScript.Quit 0
End If

' 0 means hidden window, False means do not wait for it to exit.
' The working directory must be the server folder: the server uses its own
' executable directory as the storage root and reads its config from there.
Set Shell = CreateObject("WScript.Shell")
Shell.CurrentDirectory = Fso.GetParentFolderName(ExePath)
Shell.Run """" & ExePath & """ --tsf", 0, False
