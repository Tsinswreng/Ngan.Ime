輸入法 Preedit顯示 沒有顯示caret (輸入遊標)

在weasel和trime上
我按下 kj 兩個鍵
preedit顯示的應該是




如果我再按一下 左 鍵
就變成
k‸j

了

在ngan.ime上 完全沒有 ‸ 顯示。
先不改代碼 看看怎麼回事。



2026_0626_231741[
看
```ts
File: d:\Program Files\Rime\User_Data\_tsToLua\src\Tsinswreng\types\librime-lua.d.ts
114: 	get_commit_text():string
115: 	/**
116: 	 * 你是甚麼‸ -> ni|shi|shen|me
117: 	 * 你是‸甚麼 -> ni|shi
118: 	 */
119: 	get_script_text():string //-- 按音节分割
120: 	/**
121: 	 * 你是‸甚麼 -> nkq|dsy‸djrm
122: 	 */

```
這是librime-lua的api

看一下rime api裏面有沒有 get_script_text
有的話直接用 get_script_text 代替preedit
如果沒有 就看librime-lua裏面 get_script_text 是怎麼實現的。

	E:\_code\_clone\rime
	這下面有完整的 librime和librime-lua 和 weasel的 c++ 源碼
]
