namespace Ngan.Ime.Core.Keys;


[Doc($$"""
把「引擎未處理的按鍵」轉發給作業系統／宿主應用的處理器。
各平臺各一個實現（Android 走 `InputConnection`、Windows 走 `keybd_event`、TSF 走回包給原生外殼）。

#Eg[
	```cs
	await OsKeyProcessor.OnKeyEvents(UnhandledKeys, Ct);
	```
]
""")]
public interface IOsKeyProcessor
	:IKeyProcessor
{
}
