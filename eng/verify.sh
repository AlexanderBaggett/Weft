#!/usr/bin/env bash
set -euo pipefail
weft_repo_root="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")/.." && pwd)"
cd "$weft_repo_root"
dotnet --version
javac -version
java -version
dotnet restore Weft.slnx
dotnet build Weft.slnx --configuration Release --no-restore
dotnet test Weft.slnx --configuration Release --no-build --logger 'trx;LogFileName=conformance.trx'
dotnet run --configuration Release --no-build --project src/Weft.Cli -- check --project examples/foundation
dotnet run --configuration Release --no-build --project src/Weft.Cli -- run --project examples/foundation --backend dotnet
dotnet run --configuration Release --no-build --project src/Weft.Cli -- run --project examples/foundation --backend jvm
