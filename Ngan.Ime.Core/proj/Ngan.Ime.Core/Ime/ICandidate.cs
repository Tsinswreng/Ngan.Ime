
namespace Ngan.Ime.Core.Ime;
using Tsinswreng.CsCore;


//[Doc(@$"#See[{nameof(RimeCandidate)}]")]
public interface ICandidate{
	public str Text{get;set;}
	public str Comment{get;set;}
}

public class Candidate:ICandidate{
	public str Text{get;set;} = "";
	public str Comment{get;set;} = "";
}

