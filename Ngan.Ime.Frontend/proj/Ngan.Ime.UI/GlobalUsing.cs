// Ngan.Ime.UI 的全域 using（只放界面層需要的東西；Backend／Test 不引用 Avalonia）。
//
// *為何不再有 Tsinswreng.Avln.Dsl*：使用者已明令撤除該庫；界面一律用
// Avalonia.Markup.Declarative 的純 C# 聲明式寫法，布局容器用本地 Tsinswreng.Avln.Grid。
global using Avalonia.Markup.Declarative;
global using Ngan.Ime.Core.Ime;
global using Ngan.Ime.Core.Keys;
global using Ngan.Ime.Core.Language;
global using Ngan.Ime.Core.Voice;
global using Ngan.Ime.UI.Infra;
global using Tsinswreng.Avln.Grid;

#region Grid 別名
global using ColDef = Avalonia.Controls.ColumnDefinition;
global using RowDef = Avalonia.Controls.RowDefinition;
global using GUT = Avalonia.Controls.GridUnitType;
#endregion

global using TxtAlign = Avalonia.Media.TextAlignment;
global using VAlign = Avalonia.Layout.VerticalAlignment;
global using HAlign = Avalonia.Layout.HorizontalAlignment;
