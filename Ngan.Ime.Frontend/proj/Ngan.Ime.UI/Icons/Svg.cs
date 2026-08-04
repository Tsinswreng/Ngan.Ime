namespace Ngan.Ime.UI.Icons;

using TStruct = Svg;

public record struct Svg(str V)
{
	public str Value => V;

	public static implicit operator str(TStruct e) => e.Value;
	public static implicit operator TStruct(str s) => new(s);

	public override string ToString() => Value;
}
