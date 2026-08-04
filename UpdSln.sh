#!/usr/bin/env bash
set -euo pipefail

ROOT_DIR="$(CDPATH= cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd)"
SOLUTION="$ROOT_DIR/Ngan.Ime.sln"

cd "$ROOT_DIR"
rm -f "$SOLUTION"
dotnet new sln -n Ngan.Ime

dotnet sln "$SOLUTION" add Ngan.Ime.Core/proj/**/*.csproj
dotnet sln "$SOLUTION" add Ngan.Ime.Doc/proj/**/*.csproj
dotnet sln "$SOLUTION" add Ngan.Ime.Frontend/proj/**/*.csproj
dotnet sln "$SOLUTION" add Ngan.Ime.Rime/proj/**/*.csproj
dotnet sln "$SOLUTION" add ../CsRimeApi/proj/**/*.csproj
dotnet sln "$SOLUTION" add ../Tsinswreng.CsInterop/proj/**/*.csproj
