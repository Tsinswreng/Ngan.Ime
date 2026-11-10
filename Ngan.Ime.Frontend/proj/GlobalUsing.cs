// 前端各專案（Ui／Windows／Android／Desktop／Browser／iOS／Backend／Test）共用的全域 using。
//
// 為何放在 Frontend/proj 而不是各專案：這些命名空間出現頻率極高，
// 分散宣告只會各處不一致。Avalonia 的別名不放在這裡——Backend 與 Test 不引用 Avalonia，
// 那批別名留在 Ngan.Ime.UI/GlobalUsing.cs。
global using Tsinswreng.CsCore;
global using Tsinswreng.CsCfg;
global using Tsinswreng.CsErr;
global using Tsinswreng.CsKeyNode;

global using Ngan.Ime;
global using Ngan.Ime.Infra;
global using Ngan.Ime.Infra.Cfg;
global using Ngan.Ime.Infra.Json;
global using Ngan.Ime.Infra.Log;
global using Ngan.Ime.Infra.Storage;
