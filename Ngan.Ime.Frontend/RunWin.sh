OUT_DIR=./proj/Ngan.Ime.Windows/bin/Debug/net10.0/win-x86
mkdir -p "$OUT_DIR"
cp ./proj/Ngan.Ime.Windows/Ngan.Ime.Ro.jsonc "$OUT_DIR/Ngan.Ime.Ro.jsonc"
cp ./proj/Ngan.Ime.Windows/Ngan.Ime.Rw.jsonc "$OUT_DIR/Ngan.Ime.Rw.jsonc"
dotnet run --project ./proj/Ngan.Ime.Windows/Ngan.Ime.Windows.csproj -r win-x86
