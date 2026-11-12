namespace Ngan.Ime.UI.Views.KeyBoard;

using Ngan.Ime.Core.Keys;
using Tsinswreng.CsCore;
using static Ngan.Ime.UI.Views.KeyBoard.KeyGesture;

[Doc($$"""
內建鍵盤布局（純資料）。

*為何把布局搬出 View*：先前 240 行 `KView(new(){Key=…, Label=…, SwipeUp=…})` 寫死在
{{nameof(Ngan.Ime.UI.Views.KeyBoard.ViewKeyBoard)}} 裡，導致①新增一種布局要改 View；
②數字層與字母層的第一排逐字重複；③改一顆鍵的提示要改編譯單元並重新發版。
現在布局是資料，View 只負責把 {{nameof(KeyRow)}} 畫出來。

*為何是 C# 常量而非配置檔*：布局是產品的一部分（跟鍵帽圖標一樣），不是使用者設定；
放配置會引入版本管理與「配置壞掉就沒有鍵盤」的風險。等要做「使用者自訂布局」時，
再把同一份 {{nameof(KeyDef)}} 模型序列化成配置即可——模型已經是可序列化的形狀。

*鍵名*：一律用 {{nameof(KeyChars)}} 的字段，不寫字符串字面量。

#Eg[
	```cs
	foreach(var Row in KeyLayout.MainRows){ /* 畫一排 */ }
	```
]
""")]
public static class KeyLayout{

	[Doc("鍵帽主文字的默認顯示：單字母鍵大寫，其餘用鍵名。")]
	static str DfltLabel(IKeyChar Key){
		if(Key.Name.Length == 1 && char.IsAsciiLetter(Key.Name[0])){
			return char.ToUpperInvariant(Key.Name[0]).ToString();
		}
		return Key.Name;
	}

	[Doc("數字排（`1`～`0`）：上檔符號在 Hint，長按與上滑都送符號。")]
	static KeyRow DigitRow(){
		KeyDef D(IKeyChar Key, str Hint, IKeyChar Shifted){
			return new KeyDef{
				Key = Key,
				Hint = Hint,
				Gestures = KeyDef.G(
					(EGestureSlot.LongClick, Send(Shifted)),
					(EGestureSlot.SwipeUp, Send(Shifted))
				),
			};
		}
		return new KeyRow{
			Cols = "*,*,*,*,*,*,*,*,*,*",
			Keys = [
				D(KeyChars.D1, "!", KeyChars.Exclamation),
				D(KeyChars.D2, "@", KeyChars.At),
				D(KeyChars.D3, "#", KeyChars.HashTag),
				D(KeyChars.D4, "$", KeyChars.Dollar),
				D(KeyChars.D5, "%", KeyChars.Percent),
				D(KeyChars.D6, "^", KeyChars.Caret),
				D(KeyChars.D7, "&", KeyChars.Ampersand),
				D(KeyChars.D8, "*", KeyChars.Asterisk),
				D(KeyChars.D9, "(", KeyChars.Paren_L),
				D(KeyChars.D0, ")", KeyChars.Paren_R),
			],
		};
	}

	[Doc("字母層第一排（與數字層第一排相同，故共用）。")]
	static KeyRow AlphaRow1(){
		return DigitRow();
	}

	[Doc("字母層第二排：q～y。")]
	static KeyRow AlphaRow2(){
		return new KeyRow{
			Cols = "*,*,*,*,*,*,*,*,*,*",
			Keys = [
				new(){ Key = KeyChars.LowerQ, Label = "Q", Gestures = KeyDef.G((EGestureSlot.SwipeUp, Send(KeyChars.Q))) },
				new(){ Key = KeyChars.LowerW, Label = "W", Gestures = KeyDef.G((EGestureSlot.SwipeUp, Send(KeyChars.W))) },
				new(){ Key = KeyChars.LowerE, Label = "E", Gestures = KeyDef.G((EGestureSlot.SwipeUp, Send(KeyChars.E))) },
				new(){ Key = KeyChars.LowerR, Label = "R", Gestures = KeyDef.G((EGestureSlot.SwipeUp, Send(KeyChars.R))) },
				new(){ Key = KeyChars.LowerT, Label = "T", Gestures = KeyDef.G((EGestureSlot.SwipeUp, Send(KeyChars.T))) },
				new(){
					Key = KeyChars.LowerU, Label = "U",
					Gestures = KeyDef.G(
						(EGestureSlot.SwipeUp, Send(KeyChars.U)),
						(EGestureSlot.SwipeLeft, Send(KeyChars.Paren_L)),
						(EGestureSlot.SwipeRight, Send(KeyChars.Paren_R))
					),
				},
				new(){
					Key = KeyChars.LowerI, Label = "I",
					Gestures = KeyDef.G(
						(EGestureSlot.SwipeUp, Send(KeyChars.I)),
						(EGestureSlot.SwipeLeft, Send(KeyChars.SquareBracket_L)),
						(EGestureSlot.SwipeRight, Send(KeyChars.SquareBracket_R))
					),
				},
				new(){
					Key = KeyChars.LowerO, Label = "O",
					Gestures = KeyDef.G(
						(EGestureSlot.SwipeUp, Send(KeyChars.O)),
						(EGestureSlot.SwipeLeft, Send(KeyChars.Braces_L)),
						(EGestureSlot.SwipeRight, Send(KeyChars.Braces_R))
					),
				},
				new(){
					Key = KeyChars.LowerP, Label = "Π",
					Gestures = KeyDef.G(
						(EGestureSlot.SwipeUp, Send(KeyChars.P)),
						(EGestureSlot.SwipeLeft, Send(KeyChars.Less)),
						(EGestureSlot.SwipeRight, Send(KeyChars.Greater))
					),
				},
				new(){
					Key = KeyChars.LowerY, Label = "Y", Hint = "⇆", HintBottom = "↷",
					Gestures = KeyDef.G(
						(EGestureSlot.SwipeUp, Send(KeyChars.Y)),
						(EGestureSlot.SwipeLeft, ToggleAscii()),
						(EGestureSlot.SwipeRight, CtrlChord(KeyChars.LowerY)),
						(EGestureSlot.LongClick, CtrlChord(KeyChars.LowerY))
					),
				},
			],
		};
	}

	[Doc("字母層第三排：a～;。")]
	static KeyRow AlphaRow3(){
		return new KeyRow{
			Cols = "*,*,*,*,*,*,*,*,*,*",
			Keys = [
				new(){
					Key = KeyChars.LowerA, Label = "A", HintBottom = "☑",
					Gestures = KeyDef.G(
						(EGestureSlot.SwipeUp, Send(KeyChars.A)),
						(EGestureSlot.SwipeRight, CtrlChord(KeyChars.LowerA)),
						(EGestureSlot.LongClick, CtrlChord(KeyChars.LowerA))
					),
				},
				new(){ Key = KeyChars.LowerS, Label = "Σ", Hint = "⇪", Gestures = KeyDef.G((EGestureSlot.SwipeUp, Send(KeyChars.S))) },
				new(){ Key = KeyChars.LowerD, Label = "Δ", Gestures = KeyDef.G((EGestureSlot.SwipeUp, Send(KeyChars.D))) },
				new(){ Key = KeyChars.LowerF, Label = "F", Gestures = KeyDef.G((EGestureSlot.SwipeUp, Send(KeyChars.F))) },
				new(){
					Key = KeyChars.LowerG, Label = "G",
					Gestures = KeyDef.G(
						(EGestureSlot.SwipeUp, Send(KeyChars.G)),
						(EGestureSlot.SwipeLeft, Send(KeyChars.Left)),
						(EGestureSlot.SwipeRight, Send(KeyChars.Right)),
						(EGestureSlot.SwipeDown, Send(KeyChars.Down))
					),
				},
				new(){ Key = KeyChars.LowerH, Label = "H", Gestures = KeyDef.G((EGestureSlot.SwipeUp, Send(KeyChars.H))) },
				new(){ Key = KeyChars.LowerJ, Label = "J", Gestures = KeyDef.G((EGestureSlot.SwipeUp, Send(KeyChars.J))) },
				new(){ Key = KeyChars.LowerK, Label = "K", Gestures = KeyDef.G((EGestureSlot.SwipeUp, Send(KeyChars.K))) },
				new(){ Key = KeyChars.LowerL, Label = "Λ", Gestures = KeyDef.G((EGestureSlot.SwipeUp, Send(KeyChars.L))) },
				new(){
					Key = KeyChars.Semicolon, Label = ";", Hint = ":",
					Gestures = KeyDef.G(
						(EGestureSlot.SwipeUp, Send(KeyChars.Colon)),
						(EGestureSlot.LongClick, Send(KeyChars.Colon))
					),
				},
			],
		};
	}

	[Doc("字母層第四排：z～'。")]
	static KeyRow AlphaRow4(){
		return new KeyRow{
			Cols = "*,*,*,*,*,*,*,*,*,*",
			Keys = [
				new(){
					Key = KeyChars.LowerZ, Label = "Z", HintBottom = "↶",
					Gestures = KeyDef.G(
						(EGestureSlot.SwipeUp, Send(KeyChars.Z)),
						(EGestureSlot.SwipeRight, CtrlChord(KeyChars.LowerZ)),
						(EGestureSlot.LongClick, CtrlChord(KeyChars.LowerZ))
					),
				},
				new(){
					Key = KeyChars.LowerX, Label = "X", HintBottom = "✁",
					Gestures = KeyDef.G(
						(EGestureSlot.SwipeUp, Send(KeyChars.X)),
						(EGestureSlot.SwipeRight, CtrlChord(KeyChars.LowerX)),
						(EGestureSlot.LongClick, CtrlChord(KeyChars.LowerX))
					),
				},
				new(){
					Key = KeyChars.LowerC, Label = "C", HintBottom = "❐",
					Gestures = KeyDef.G(
						(EGestureSlot.SwipeUp, Send(KeyChars.C)),
						(EGestureSlot.SwipeRight, CtrlChord(KeyChars.LowerC)),
						(EGestureSlot.LongClick, CtrlChord(KeyChars.LowerC))
					),
				},
				new(){
					Key = KeyChars.LowerV, Label = "V", HintBottom = "▣",
					Gestures = KeyDef.G(
						(EGestureSlot.SwipeUp, Send(KeyChars.V)),
						(EGestureSlot.SwipeRight, CtrlChord(KeyChars.LowerV)),
						(EGestureSlot.LongClick, CtrlChord(KeyChars.LowerV))
					),
				},
				new(){ Key = KeyChars.LowerB, Label = "B", Gestures = KeyDef.G((EGestureSlot.SwipeUp, Send(KeyChars.B))) },
				new(){ Key = KeyChars.LowerN, Label = "N", Gestures = KeyDef.G((EGestureSlot.SwipeUp, Send(KeyChars.N))) },
				new(){
					Key = KeyChars.LowerM, Label = "M", Hint = "$m,",
					Gestures = KeyDef.G(
						(EGestureSlot.SwipeUp, Send(KeyChars.M)),
						(EGestureSlot.SwipeDown, SendTextThenSpace(KeyChars.Dollar, KeyChars.LowerM, KeyChars.Comma)),
						(EGestureSlot.SwipeLeft, SendTextThenSpace(KeyChars.Dollar, KeyChars.LowerM, KeyChars.Comma, KeyChars.LowerJ)),
						(EGestureSlot.SwipeRight, SendTextThenSpace(KeyChars.Dollar, KeyChars.LowerM, KeyChars.Comma, KeyChars.LowerI))
					),
				},
				new(){
					Key = KeyChars.Comma, Label = ",", Hint = "<",
					Gestures = KeyDef.G(
						(EGestureSlot.SwipeUp, Send(KeyChars.Less)),
						(EGestureSlot.LongClick, Send(KeyChars.Less)),
						(EGestureSlot.SwipeLeft, Send(KeyChars.Less))
					),
				},
				new(){
					Key = KeyChars.Period, Label = ".", Hint = ">",
					Gestures = KeyDef.G(
						(EGestureSlot.SwipeUp, Send(KeyChars.Greater)),
						(EGestureSlot.LongClick, Send(KeyChars.Greater)),
						(EGestureSlot.SwipeRight, Send(KeyChars.Greater))
					),
				},
				new(){
					Key = KeyChars.Apostrophe, Label = "'", Hint = "\"",
					Gestures = KeyDef.G((EGestureSlot.SwipeUp, Send(KeyChars.Quote))),
				},
			],
		};
	}

	[Doc("字母層第五排：Enter／Tab／方向／空白／Shift 鎖定／退格。")]
	static KeyRow AlphaRow5(){
		return new KeyRow{
			Cols = "2*,*,*,*,*,*,*,2*",
			Keys = [
				new(){ Key = KeyChars.Enter, Label = "↵" },
				new(){ Key = KeyChars.Tab, Label = "␉" },
				new(){
					Key = KeyChars.Left, Label = "←", Hint = "⇤",
					Gestures = KeyDef.G(
						(EGestureSlot.LongClick, Send(KeyChars.Home)),
						(EGestureSlot.SwipeUp, Send(KeyChars.Home))
					),
				},
				new(){
					Key = KeyChars.Space, Label = "",
					Gestures = KeyDef.G(
						(EGestureSlot.SwipeLeft, Send(KeyChars.Left)),
						(EGestureSlot.SwipeRight, Send(KeyChars.Right))
					),
				},
				new(){
					Key = KeyChars.Space, Label = "",
					Gestures = KeyDef.G(
						(EGestureSlot.SwipeLeft, Send(KeyChars.Left)),
						(EGestureSlot.SwipeRight, Send(KeyChars.Right))
					),
				},
				new(){
					Key = KeyChars.Right, Label = "→", Hint = "⇥",
					Gestures = KeyDef.G(
						(EGestureSlot.LongClick, Send(KeyChars.End)),
						(EGestureSlot.SwipeUp, Send(KeyChars.End))
					),
				},
				new(){
					Key = KeyChars.Dollar, Label = "$", Hint = "⇪",
					Gestures = KeyDef.G((EGestureSlot.SwipeUp, ToggleShiftLock())),
				},
				new(){
					Key = KeyChars.Backspace, Label = "⌫", IsRepeat = true,
					Gestures = KeyDef.G(
						(EGestureSlot.LongClick, Send(KeyChars.Backspace))
					),
				},
			],
		};
	}

	[Doc("字母層第六排：符號與切換數字層。")]
	static KeyRow AlphaRow6(){
		return new KeyRow{
			Cols = "*,*,*,*,*,*,*,*,*,*",
			Keys = [
				new(){ Key = KeyChars.Minus, Label = "-", Hint = "_", Gestures = KeyDef.G((EGestureSlot.SwipeUp, Send(KeyChars.Underscore))) },
				new(){ Key = KeyChars.Equal, Label = "=", Hint = "+", Gestures = KeyDef.G((EGestureSlot.SwipeUp, Send(KeyChars.Plus))) },
				new(){
					Key = KeyChars.SquareBracket_L, Label = "[", Hint = "{",
					Gestures = KeyDef.G(
						(EGestureSlot.SwipeUp, Send(KeyChars.Braces_L)),
						(EGestureSlot.LongClick, Send(KeyChars.Braces_L))
					),
				},
				new(){
					Key = KeyChars.SquareBracket_R, Label = "]", Hint = "}",
					Gestures = KeyDef.G(
						(EGestureSlot.SwipeUp, Send(KeyChars.Braces_R)),
						(EGestureSlot.LongClick, Send(KeyChars.Braces_R))
					),
				},
				new(){ Key = KeyChars.Up, Label = "↑" },
				new(){ Key = KeyChars.Down, Label = "↓" },
				new(){ Key = KeyChars.Slash, Label = "/", Hint = "?", Gestures = KeyDef.G((EGestureSlot.SwipeUp, Send(KeyChars.Question))) },
				new(){ Key = KeyChars.BackSlash, Label = "\\", Hint = "|", Gestures = KeyDef.G((EGestureSlot.SwipeUp, Send(KeyChars.Pipe))) },
				new(){ Key = KeyChars.Grave, Label = "`", Hint = "~", Gestures = KeyDef.G((EGestureSlot.SwipeUp, Send(KeyChars.Tilde))) },
				new(){
					Key = KeyChars.D1,
					Label = "123",
					Gestures = KeyDef.G((EGestureSlot.LongClick, SetLayout(NumLayout: true))),
					Hint = null,
				},
			],
		};
	}

	[Doc("數字層第二排。")]
	static KeyRow NumRow2(){
		return new KeyRow{
			Cols = "*,*,2*,2*,2*,*,*",
			Keys = [
				new(){ Key = KeyChars.LowerQ, Label = "Q", Gestures = KeyDef.G((EGestureSlot.SwipeUp, Send(KeyChars.Q))) },
				new(){ Key = KeyChars.LowerW, Label = "W", Gestures = KeyDef.G((EGestureSlot.SwipeUp, Send(KeyChars.W))) },
				new(){ Key = KeyChars.D7 },
				new(){ Key = KeyChars.D8 },
				new(){
					Key = KeyChars.D9,
					Gestures = KeyDef.G(
						(EGestureSlot.SwipeUp, Send(KeyChars.I)),
						(EGestureSlot.LongClick, Send(KeyChars.SquareBracket_L)),
						(EGestureSlot.SwipeLeft, Send(KeyChars.SquareBracket_L)),
						(EGestureSlot.SwipeRight, Send(KeyChars.SquareBracket_R))
					),
				},
				new(){
					Key = KeyChars.LowerP, Label = "Π",
					Gestures = KeyDef.G(
						(EGestureSlot.SwipeUp, Send(KeyChars.P)),
						(EGestureSlot.SwipeLeft, Send(KeyChars.Less)),
						(EGestureSlot.SwipeRight, Send(KeyChars.Greater))
					),
				},
				new(){
					Key = KeyChars.LowerY, Label = "Y", Hint = "⇆", HintBottom = "↷",
					Gestures = KeyDef.G(
						(EGestureSlot.SwipeUp, Send(KeyChars.Y)),
						(EGestureSlot.SwipeRight, CtrlChord(KeyChars.LowerY)),
						(EGestureSlot.LongClick, CtrlChord(KeyChars.LowerY))
					),
				},
			],
		};
	}

	[Doc("數字層第三排。")]
	static KeyRow NumRow3(){
		return new KeyRow{
			Cols = "*,*,2*,2*,2*,*,*",
			Keys = [
				new(){ Key = KeyChars.LowerA, Label = "A", Gestures = KeyDef.G((EGestureSlot.SwipeUp, Send(KeyChars.A))) },
				new(){ Key = KeyChars.LowerS, Label = "Σ", Hint = "⇪", Gestures = KeyDef.G((EGestureSlot.SwipeUp, Send(KeyChars.S))) },
				new(){ Key = KeyChars.D4, Gestures = KeyDef.G((EGestureSlot.SwipeUp, Send(KeyChars.D))) },
				new(){
					Key = KeyChars.D5,
					Gestures = KeyDef.G(
						(EGestureSlot.SwipeUp, Send(KeyChars.G)),
						(EGestureSlot.SwipeLeft, Send(KeyChars.Left)),
						(EGestureSlot.SwipeRight, Send(KeyChars.Right)),
						(EGestureSlot.SwipeDown, Send(KeyChars.Down))
					),
				},
				new(){ Key = KeyChars.D6, Gestures = KeyDef.G((EGestureSlot.SwipeUp, Send(KeyChars.J))) },
				new(){ Key = KeyChars.LowerL, Label = "Λ", Gestures = KeyDef.G((EGestureSlot.SwipeUp, Send(KeyChars.L))) },
				new(){
					Key = KeyChars.Semicolon, Label = ";", Hint = ":",
					Gestures = KeyDef.G(
						(EGestureSlot.SwipeUp, Send(KeyChars.Colon)),
						(EGestureSlot.LongClick, Send(KeyChars.Colon))
					),
				},
			],
		};
	}

	[Doc("數字層第四排。")]
	static KeyRow NumRow4(){
		return new KeyRow{
			Cols = "*,*,2*,2*,2*,*,*",
			Keys = [
				new(){ Key = KeyChars.LowerZ, Label = "Z", Gestures = KeyDef.G((EGestureSlot.SwipeUp, Send(KeyChars.Z))) },
				new(){ Key = KeyChars.LowerX, Label = "X", Gestures = KeyDef.G((EGestureSlot.SwipeUp, Send(KeyChars.X))) },
				new(){ Key = KeyChars.D1, Gestures = KeyDef.G((EGestureSlot.SwipeUp, Send(KeyChars.C))) },
				new(){ Key = KeyChars.D2, Gestures = KeyDef.G((EGestureSlot.SwipeUp, Send(KeyChars.B))) },
				new(){ Key = KeyChars.D3, Gestures = KeyDef.G((EGestureSlot.SwipeUp, Send(KeyChars.M))) },
				new(){
					Key = KeyChars.Period, Label = ".", Hint = ">",
					Gestures = KeyDef.G(
						(EGestureSlot.SwipeUp, Send(KeyChars.Greater)),
						(EGestureSlot.LongClick, Send(KeyChars.Greater)),
						(EGestureSlot.SwipeRight, Send(KeyChars.Greater))
					),
				},
				new(){
					Key = KeyChars.Apostrophe, Label = "'", Hint = "\"",
					Gestures = KeyDef.G((EGestureSlot.SwipeUp, Send(KeyChars.Quote))),
				},
			],
		};
	}

	[Doc("數字層第五排。")]
	static KeyRow NumRow5(){
		return new KeyRow{
			Cols = "2*,2*,2*,2*,2*",
			Keys = [
				new(){ Key = KeyChars.Enter, Label = "↵" },
				new(){ Key = KeyChars.D0 },
				new(){
					Key = KeyChars.Space, Label = "",
					Gestures = KeyDef.G(
						(EGestureSlot.SwipeLeft, Send(KeyChars.Left)),
						(EGestureSlot.SwipeRight, Send(KeyChars.Right))
					),
				},
				new(){
					Key = KeyChars.Period, Label = ".", Hint = ">",
					Gestures = KeyDef.G((EGestureSlot.SwipeUp, Send(KeyChars.Greater))),
				},
				new(){
					Key = KeyChars.Backspace, Label = "⌫", IsRepeat = true,
					Gestures = KeyDef.G((EGestureSlot.LongClick, Send(KeyChars.Backspace))),
				},
			],
		};
	}

	[Doc("數字層第六排。")]
	static KeyRow NumRow6(){
		return new KeyRow{
			Cols = "*,*,*,*,*,*,*,*,*,*",
			Keys = [
				new(){ Key = KeyChars.Minus, Label = "-", Hint = "_", Gestures = KeyDef.G((EGestureSlot.SwipeUp, Send(KeyChars.Underscore))) },
				new(){ Key = KeyChars.Equal, Label = "=", Hint = "+", Gestures = KeyDef.G((EGestureSlot.SwipeUp, Send(KeyChars.Plus))) },
				new(){ Key = KeyChars.Slash, Label = "/", Hint = "?", Gestures = KeyDef.G((EGestureSlot.SwipeUp, Send(KeyChars.Question))) },
				new(){ Key = KeyChars.BackSlash, Label = "\\", Hint = "|", Gestures = KeyDef.G((EGestureSlot.SwipeUp, Send(KeyChars.Pipe))) },
				new(){
					Key = KeyChars.Left, Label = "←", Hint = "⇤",
					Gestures = KeyDef.G(
						(EGestureSlot.LongClick, Send(KeyChars.Home)),
						(EGestureSlot.SwipeUp, Send(KeyChars.Home))
					),
				},
				new(){
					Key = KeyChars.Right, Label = "→", Hint = "⇥",
					Gestures = KeyDef.G(
						(EGestureSlot.LongClick, Send(KeyChars.End)),
						(EGestureSlot.SwipeUp, Send(KeyChars.End))
					),
				},
				new(){ Key = KeyChars.Up, Label = "↑" },
				new(){ Key = KeyChars.Down, Label = "↓" },
				new(){ Key = KeyChars.Grave, Label = "`", Hint = "~", Gestures = KeyDef.G((EGestureSlot.SwipeUp, Send(KeyChars.Tilde))) },
				new(){ Key = KeyChars.LowerQ, Label = "qwe", Gestures = KeyDef.G((EGestureSlot.LongClick, SetLayout(NumLayout: false))) },
			],
		};
	}

	[Doc("字母層六排。")]
	public static readonly IReadOnlyList<KeyRow> MainRows = [
		AlphaRow1(), AlphaRow2(), AlphaRow3(), AlphaRow4(), AlphaRow5(), AlphaRow6(),
	];

	[Doc("數字層六排。")]
	public static readonly IReadOnlyList<KeyRow> NumRows = [
		DigitRow(), NumRow2(), NumRow3(), NumRow4(), NumRow5(), NumRow6(),
	];

	[Doc("每一排的高度比例（第一排與最後一排略矮，與舊版一致）。")]
	public const str RowHeights = "0.8*,*,*,*,*,0.8*";

	[Doc("鍵帽主文字的默認顯示規則；供 ViewModel 使用。")]
	public static str LabelOf(KeyDef Def){
		return Def.Label ?? DfltLabel(Def.Key);
	}
}
