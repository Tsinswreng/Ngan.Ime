-Sesn[
	-T[2026_0725_141600][
		先看skill。
		然後看`E:\_code\CsNgaq\_Note\TryAndroidAot\Avln12NativeAot.typ`
	然後你試着給avalime也弄一個NativeAOT的包。
	注意AOT有很多種、我要的是NativeAOT。
	這種AOT編譯出來的apk體積一定是小得多的。
	wsl要在沙箱外纔能執行.


	][]
]


-Sesn[
	-T[2026_0725_224113][
		在windows上通過adb 給termux上的git
		的key設成指定值、這樣我操作私有倉庫的時候不用每次都填token。
		不是ssh的 是用户的token
		告訴我命令。
	][
````md
adb shell "echo 'https://YOUR_USERNAME:YOUR_TOKEN@github.com' > /data/data/com.termux/files/home/.git-credentials"
````
	]
]
