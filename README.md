Proof of concept for Epic [#9672](https://github.com/dotnet/arcade/issues/9672). Issue [#9843 - Investigate and write PoC for using Roslyn as a backend for GenAPI ](https://github.com/dotnet/arcade/issues/9843)

Takes `dll` as an argument, reconstruct reference package out of it and print to a console.

Influenced by [Azure/APIView](https://github.com/Azure/azure-sdk-tools/tree/main/src/dotnet/APIView/APIView); reused, removed redundant (not needed) files/classes/etc, ...

Usage:
```
.\GenAPI-Roslyn.exe Microsoft.CodeAnalysis.CSharp.dll >> Microsoft.CodeAnalysis.cs
```