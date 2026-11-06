# Python Analyzer

## Input and boundary

The analyzer accepts `.py` files, directories, and `pyproject.toml`. Directory discovery groups Python files by their nearest project boundary. A direct source-file input analyzes that file only. The analyzer parses discovered source without importing or executing it.

The bundled FastParse Python grammar parses source without requiring Python or another language toolchain on `PATH`. Parse errors become `ACI002`; file and parser failures become `ACI001`. A partial parse retains metadata from files that parsed successfully. The Python extension is currently marked preview by its publisher and provides native assets for Windows x64, Linux x64, and macOS x64/arm64; unsupported native targets are reported as partial analysis.

## Analysis data

`PythonModuleAnalysisData` retains each module's project-relative name, relative and absolute import edges to modules found in the same analysis scope, function/class scope summaries, pytest/unittest indicators, and observed assignment-kind changes. Function summaries include parameters, assigned names, and whether the syntax contains `None`/bare returns as well as value returns.

This first backend provides syntax-tree metadata for later rule families. Import resolution is limited to discovered source files. It does not resolve installed packages, execute tests, infer full Python types, or prove path-sensitive `None` behavior.

## Local checks

```powershell
dotnet test src/AllCodeChecker.Core.Tests/AllCodeChecker.Core.Tests.csproj -c Release
dotnet test src/AllCodeChecker.Cli.Tests/AllCodeChecker.Cli.Tests.csproj -c Release
dotnet run --project src/AllCodeChecker.Cli/AllCodeChecker.Cli.csproj -c Release -- <python-project> --format json
```
