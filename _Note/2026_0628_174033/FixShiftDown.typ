看Ngan.Ime鍵盤

退格鍵左邊 →鍵右邊的`$`鍵
上滑後應該進入shift鎖定狀態

進入shift鎖定狀態之後、按下的所有按鍵都相當于按住shift再按這個按鍵的效果。

但經過嘗試、我開啓shift鎖定之後、再按 左 鍵 或 Home 鍵、他只是移動了光標 效果並不是相當於按住shift再按這個按鍵的效果。

我又用遠程控制軟件來試、我發現 開啓shift鎖定之後 再按方向鍵、他實際發送的按鍵事件是
shift按下 -> shift擡起 -> 方向鍵按下 -> 方向鍵擡起

你看看怎麼回事

2026_0628_180119[
看看這個
E:\_code\CsRime\Ngan.Ime\Ngan.Ime.Core\proj\Ngan.Ime.Core\Keys\IKeyEvent.cs

這裏面有一個
`public ISet<IKeyChar> AllDownKeys{get;set;}`
能不能讓shift的實現 和我的抽象匹配上?? 別搞這麼多套互相衝突的

]
