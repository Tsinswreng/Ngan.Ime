"""比對 C# 的生成式 COM 宣告與 msctf.h 的方法順序。

用途：手寫 vtable 時「少算一格」會把呼叫指到別的方法（2026-11-15 的 notepad 崩潰就是
`GetActiveView` 被寫成第 8 槽位、實際是第 9，於是呼叫到 `GetEnd`）。改用生成式宣告之後，
順序仍然由宣告順序決定，所以還需要用機器把「宣告順序」與頭文件對一次。

用法：
    python scripts/check-msctf-order.py [--header <msctf.h>] [--src <目錄>]

預設 header 是 D:/ENV/msys64/mingw32/include/msctf.h，原始碼目錄是本腳本上一層的
proj/Ngan.Ime.Frontend/Ngan.Ime.Tsf.Windows。找到不符就印出位置並以非零結束碼退出。
"""

import argparse
import pathlib
import re
import sys

# C# 介面名 -> 頭文件介面名
PAIRS = {
	"ITfContextGen": "ITfContext",
	"ITfContextViewGen": "ITfContextView",
	"ITfRangeGen": "ITfRange",
	"ITfKeystrokeMgrGen": "ITfKeystrokeMgr",
	"ITfTextInputProcessorGen": "ITfTextInputProcessor",
	"ITfKeyEventSinkGen": "ITfKeyEventSink",
	"ITfEditSessionGen": "ITfEditSession",
	"IClassFactoryGen": "IClassFactory",
}


def header_methods(text: str, name: str):
	"""取出頭文件裡某個介面的方法名序列（只看 C++ 的 virtual 宣告）。"""
	start = re.search(r"\b%s\s*:\s*public\s+IUnknown" % re.escape(name), text)
	if not start:
		return None
	# 到下一個介面宣告或檔尾為止
	tail = text[start.end():]
	end = re.search(r"\n[A-Za-z_]\w*\s*:\s*public\s+IUnknown", tail)
	block = tail[: end.start()] if end else tail
	return re.findall(r"STDMETHODCALLTYPE\s+(\w+)\s*\(", block)


def cs_methods(text: str, name: str):
	"""取出 C# 介面裡 [PreserveSig] 方法的序列。"""
	start = re.search(r"interface\s+%s\b" % re.escape(name), text)
	if not start:
		return None
	tail = text[start.end():]
	end = re.search(r"\n}", tail)
	block = tail[: end.start()] if end else tail
	return re.findall(r"\[PreserveSig\]\s+int\s+(\w+)\s*\(", block)


def main() -> int:
	parser = argparse.ArgumentParser()
	parser.add_argument("--header", default="D:/ENV/msys64/mingw32/include/msctf.h")
	parser.add_argument(
		"--src",
		default=str(pathlib.Path(__file__).resolve().parent.parent
			/ "proj/Ngan.Ime.Frontend/Ngan.Ime.Tsf.Windows"),
	)
	args = parser.parse_args()

	header = pathlib.Path(args.header).read_text(encoding="utf-8", errors="ignore")
	src_dir = pathlib.Path(args.src)
	cs = "".join(f.read_text(encoding="utf-8", errors="ignore") for f in src_dir.glob("*.cs"))

	failed = 0
	for cs_name, h_name in PAIRS.items():
		want = header_methods(header, h_name)
		got = cs_methods(cs, cs_name)
		if want is None:
			print(f"[skip] 頭文件找不到 {h_name}")
			continue
		if got is None:
			print(f"[skip] C# 沒有宣告 {cs_name}")
			continue
		if not got:
			print(f"[FAIL] {cs_name} 沒有宣告任何方法")
			failed += 1
			continue
		bad = None
		for index, name in enumerate(got):
			if index >= len(want):
				bad = (index, name, "(頭文件沒有這個槽位)")
				break
			if want[index] != name:
				bad = (index, name, want[index])
				break
		if bad:
			index, name, expected = bad
			print(f"[FAIL] {cs_name} 第 {index} 個方法宣告為 {name}，頭文件是 {expected}")
			failed += 1
		else:
			print(f"[ok]   {cs_name}: {len(got)} 個方法與 {h_name} 前綴一致")
	return 1 if failed else 0


if __name__ == "__main__":
	sys.exit(main())
