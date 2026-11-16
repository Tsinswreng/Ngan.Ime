namespace Ngan.Ime.Core.Keys;


[Doc($$"""
{{nameof(IKeyEventRemapper)}} 的默認實現：按「來源鍵名」建索引，命中就交給規則改寫。

*總開關為何在外面*：開關屬於使用者偏好（存在配置裡），映射器只負責機制；
故構造時傳入 Func<bool> 而不是把 UI 狀態耦合進來。
關閉時直接原樣透傳，連列舉都不額外做一層。

#Eg[
	```cs
	var Remapper = new KeyEventRemapper(() => Cfg.Enabled, [new KeyRemapRule(KeyChars.Backspace, KeyChars.Alt_R)]);
	```
]
""")]
public class KeyEventRemapper : IKeyEventRemapper {

	[Doc("總開關；每次 Remap 都重新問，故使用者切換開關即時生效。")]
	readonly Func<bool> IsEnabled;

	[Doc("來源鍵名 → 規則；以名字為鍵，故同一個來源鍵只能有一條規則（重複會在構造時拋，屬配置錯誤）。")]
	readonly IReadOnlyDictionary<str, IKeyEventRemapRule> RulesBySourceKeyName;

	[Doc($$"""
	建立映射器。
	#Prm[{{nameof(IsEnabled)}}][總開關。]
	#Prm[{{nameof(Rules)}}][規則集；每條規則的 {{nameof(IKeyEventRemapRule.SourceKeyName)}} 必須唯一。]
	""")]
	public KeyEventRemapper(Func<bool> IsEnabled, IEnumerable<IKeyEventRemapRule> Rules){
		this.IsEnabled = IsEnabled;
		RulesBySourceKeyName = Rules.ToDictionary(
			Rule => Rule.SourceKeyName
			,Rule => Rule
			,StringComparer.Ordinal
		);
	}

	[Doc("開關關閉時原樣透傳；開啟時逐事件查表改寫。")]
	public IEnumerable<IKeyEvent> Remap(IEnumerable<IKeyEvent> KeyEvents){
		if(!IsEnabled()){
			return KeyEvents;
		}
		return RemapCore(KeyEvents);
	}

	[Doc("惰性逐事件改寫：命中一條規則就展開它的事件序列，未命中則原樣放行。")]
	IEnumerable<IKeyEvent> RemapCore(IEnumerable<IKeyEvent> KeyEvents){
		foreach(var Event in KeyEvents){
			// 名字相等即命中：KeyChar 的值語義保證跨宿主傳來的同名鍵也能查到。
			if(RulesBySourceKeyName.TryGetValue(Event.KeyChar.Name, out var Rule)
				&& Rule.TryRemap(Event, out var Remapped)){
				foreach(var One in Remapped){
					yield return One;
				}
				continue;
			}
			yield return Event;
		}
	}
}
