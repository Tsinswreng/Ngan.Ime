namespace Ngan.Ime.Core.Ime;

using Tsinswreng.CsCore;

[Doc($$"""
一個候選詞。

*為何是介面*：候選可能來自本地引擎、聯想、剪貼板等多個來源，
界面只消費「文字＋註釋」，不該知道它是誰產生的。

#Eg[
	```cs
	ICandidate One = new Candidate{ Text = "你", Comment = "ni3" };
	```
]
""")]
public interface ICandidate{
	[Doc("候選詞本體，即上屏時要顯示／提交的文字。")]
	public str Text{get;set;}

	[Doc("候選詞的註釋（編碼、詞性、頻率等），可為空串。")]
	public str Comment{get;set;}
}

[Doc($$"""
{{nameof(ICandidate)}} 的默認實現。
""")]
public class Candidate : ICandidate{
	[Doc("候選詞本體；默認空串。")]
	public str Text{get;set;} = "";

	[Doc("候選詞註釋；默認空串。")]
	public str Comment{get;set;} = "";
}
