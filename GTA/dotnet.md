# Build Instructions

## Building with dotnet CLI

```bash
# Build Debug configuration
dotnet build "C:\Coding Projects\gta11y-main\GTA\GTA11Y.csproj" -c Debug

# Build Release configuration
dotnet build "C:\Coding Projects\gta11y-main\GTA\GTA11Y.csproj" -c Release
```

## Output Location

- **Debug:** `GTA\bin\x64\Debug\GrandTheftAccessibility.dll`
- **Release:** `GTA\bin\x64\Release\GrandTheftAccessibility.dll`

## Prerequisites

- .NET Framework 4.8 SDK
- ScriptHookVDotNet3.dll (v3.4.0) in `bin\x64\Debug\`
- NAudio.dll in `bin\x64\Debug\`
- TolkDotNet.dll in `bin\x64\Debug\`
