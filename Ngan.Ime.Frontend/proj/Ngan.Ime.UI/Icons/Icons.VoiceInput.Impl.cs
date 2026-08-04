namespace Ngan.Ime.UI.Icons;

/// 語音輸入專用圖標實現。
public partial class Icons{
	/// 建立 24×24 正方形視區內中心對稱的麥克風圖標。
	///
	/// 圖形主體與支架均以 x=12 為鏡像軸，整體幾何邊界在 y 軸也保持對稱留白；
	/// 因此放進任意正方形容器時，直接雙向居中即可，不需要用偏移量猜測視覺中心。
	public static partial Icon Microphone(){
		return "M12 3a3 3 0 0 0-3 3v6a3 3 0 0 0 6 0V6a3 3 0 0 0-3-3zm-5 8a1 1 0 0 0-2 0 7 7 0 0 0 6 6.92V19H8a1 1 0 0 0 0 2h8a1 1 0 0 0 0-2h-3v-2.08A7 7 0 0 0 19 11a1 1 0 0 0-2 0 5 5 0 0 1-10 0z";
	}
}
