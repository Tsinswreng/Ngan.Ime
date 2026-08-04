namespace Ngan.Ime.Core.Keys;

[Doc(@$"按鍵字符。區分了大小寫與shift狀態下纔能輸出的符號 保留備用")]
public class KeyChar:IKeyChar{
	public str Name{get;set;}="";
	public KeyChar(){}
	public KeyChar(str name){
		this.Name = name;
	}
	public static IKeyChar K(str name){
		var key = new KeyChar(name);
		return key;
	}
}
