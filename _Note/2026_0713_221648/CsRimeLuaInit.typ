-T[2026_0713_221705][
	先看skill和記憶、
	然後看CsRimeLua這個項目。

	在CsRime這個項目中，我想弄一個初始化機制。
	就是在AOT編譯出的Dll中導出一個或幾個函數、讓庫的調用方先調這些函數來初始化、
	初始化之後纔能調其他的函數。

	在lua中、我能輕易地知道我的C\#的Dll在甚麼地方、然後輕鬆地用
	loadlib調用他。

	現在有個問題、就是、
	如果要讓librime-lua調用C\# AOT編譯出的dll中的函數的話、
	C\#代碼中必須調用lua的C API纔能和LUA交互。
	就是參數聲明lua_State L、然後最後清理lua棧。



	這裏有 C\#對 LUA C API的依賴了:

	Lua 調用-> C\# DLL  調用 Lua C Api

	這樣就有幾個問題、
	C\#代碼中去哪裏找 LUA的C API?

	如果 不使用LuaCApi、則lua側雖能調用dll中的函數、但不能傳參 也不能接收返回值。
	如
	```cs
	[UnmanagedCallersOnly(...)]
	public aaa MyInitFunc(xxx){
		yyy;
		return zzz;
	}
	```
	在xxx處: 頂多能寫`lua_State`、因爲他就是IntPtr、只依賴LuaCApi的聲明而不依賴實現。
	在yyy處的話、由于當前沒拿到LuaCApi的實現、就無法使用。則無法拿到Lua側傳給C\#的參數、
	C\#側也不能給lua返回值、因爲給lua返回值要動棧、不動棧的話我記得是不行的。

	根據我的記憶(未必準確)、這樣一個普通的、沒實際用的LuaCApi的函數、要在Lua中調用的話、就只能
	```lua
	local MyInitFunc = loadlib(...)
	MyInitFunc()
	-- do other...
	```
	這樣不會炸、

	絕對不能
	````lua
	local a = MyInitFunc()
	````
	這樣就會炸。

	這樣就有辦法做一個全局的dll入口函數了。但是這個函數是不能接收參數也不能返回的。

	但是我們接下來始終是要和Lua交互的、就算在入口函數拿不到Lua Api實現、那後面還是一定要再拿到的。

	有幾種方案、
	-H[用Rime.dll][
		用Rime.dll。Rime.dll中包含了完整的Lua C api。
		只要 NativeLibrary.Load(Rime.dll) 就能拿到lua的C api。
		這種方案有個問題、
		就是 我如何在C\#中知道Rime.dll在甚麼路徑下?
		根據發行版的不同、甚至安裝位置的不同、rime.dll路徑是完全不一樣的。
		比如在weasel上、我把rime裝在`D:/ENV/Rime/weasel-0.15.0/`、 rime.dll就在`D:/ENV/Rime/weasel-0.15.0/rime.dll`。
		如果是Avalime.Android、rime.dll(librime.so)就在`/data/.../Tsinswreng.Avalime/...`裏面。


		-H[如果用條件編譯、根據不同編譯目標硬編碼不同dll路徑][
			那就太不靈活了。
		]
		-H[強制要求C\#的Dll和Rime.dll在同一目錄下][
			我當前在C\#中加載其他dll全是用`NativeLibrary`類來做的、沒用`[DllImport]`。
			我試了很多辦法、確認 在程序運行時、程序本身是拿不到自己所在路徑的、
			命令中的`.`則是pwd的路徑。
			「連接同目錄下的其他dll」可能做不到。也可能不同系統有不同的dll解析機制 他就剛好能解析到。也可能是其實也有辦法能連上。這個我不確定。我覺得有風險。
		]
		-H[用環境變量][
			Lua在調用C\#的Dll之前、先把rime.dll的路徑設在環境變量裏、然後再調。

			之前我也試過、Lua無法更改當前進程的環境變量。如果調用系統的shell來用命令改環境變量的話、其實改的是子進程的環境變量、自己的環境變量是沒有變化的。
		]
		-H[用配置文件。指定從某目錄的配置文件讀dll路徑][
			在啓動時、先調用「不依賴LuaCApi」的初始化函數、該初始化函數中讀配置文件、拿到路徑之後再鏈接dll。

			還是一樣的問題、配置文件路徑沒辦法拿到。
		]

	]
	-H[C\# Lua自己獨立準備一份同版本Lua的dll][
		還是可能有拿不到路徑的問題。
		我怎麼知道我自己準備的lua.dll在甚麼路徑下呢?
		我也不能靜態鏈接進來。
	]

	-H[參考KeraLua的做法][
		KeraLua用的是類似 `[DllImport("lua")]`這樣的。
		AOT編譯後、輸出目錄會選出KeraLua自帶的、預先編譯好的Lua.dll。
		然後能連上。大概是`[DllImport]`會有他自己的解析機制、就是會看同目錄的dll。
		我覺得他可能做了多平臺(包含Android)的適配。
	]

	你有甚麼想法
][

]


-T[2026_0713_232018][
	我想實現的目標就是用C\#給Rime引擎寫擴展。
	既然Lua這麼麻煩 那能不能不走Lua、直接讓Rime調C\#?
	這樣會不會方便點
][

]



-NewSesn[]

-T[2026_0713_230558][
	介紹C\# NativeLibrary 都有哪些方式來查找Dll?
	特別、當我的C\#程序本身以Native AOT編譯成了一個dll、然後我這個dll還要導入第三方dll、但第三方Dll不是絕對路徑時 或我不知道Dll的絕對路徑時、我要怎麼辦
	考慮 Windows, Android, Linux系統
][

]


-T[][][

]
