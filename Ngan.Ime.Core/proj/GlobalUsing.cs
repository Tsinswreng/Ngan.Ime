// Core 程序集的全域 using。
// 基礎設施（Di／AppLog／Todo／KeysErr 在 Ngan.Ime 根命名空間）是全項目共用的，
// 故把子命名空間一併導入，免得每個檔案都要寫同一排 using。
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
