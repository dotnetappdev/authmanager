Property Setup & Build Notes

This project requires a .NET SDK compatible with the `global.json` at the repository root.

- Required SDK (as specified in `global.json`): 10.0.103

Quick checks

- Verify installed SDKs:

```powershell
dotnet --list-sdks
```

- Check dotnet runtime & SDK details:

```powershell
dotnet --info
```

If you don't have the required SDK

- Install the matching SDK from https://dotnet.microsoft.com (use the exact version `10.0.103` if possible), or update `global.json` to a version you have.

Common build troubleshooting

- Error: "The TargetFramework value '' was not recognized." — this means MSBuild evaluated `$(TargetFramework)` to an empty string for one of the projects during build. Steps to resolve:
  - Ensure all SDK-style projects (`<Project Sdk="...">`) define a `TargetFramework` (single TF) or `TargetFrameworks` (plural) property. Example:

    ```xml
    <PropertyGroup>
      <TargetFramework>net10.0</TargetFramework>
    </PropertyGroup>
    ```

  - Confirm `Directory.Build.props` or other imported `.props` files do NOT unset or override `TargetFramework` to an empty value.
  - If using an IDE (Visual Studio/VS Code), confirm it is using the same `dotnet` SDK as `dotnet --info` reports. Visual Studio may use its own MSBuild/SDK resolver — update Visual Studio or set the global.json to a supported SDK.
  - Run a verbose build to see which project triggers the error:

    ```powershell
    dotnet build -v:detailed
    ```

  - If a specific project shows `TargetFramework=''` in the logs, open that `.csproj` and ensure the `<TargetFramework>` line is present and not conditional on an unset property.

If you can't install the SDK right now

- You can temporarily change `global.json` to a lower SDK version you have (not recommended for publishing) or edit individual project `TargetFramework` values to match a supported TF moniker on your machine (for local testing).

Notes for this repository

- Projects currently target `net10.0` (and one project targets `netstandard2.0` for the source generator). Keep SDK >= the version in `global.json` to avoid SDK/TF mismatches.

If you want, I can:
- Search each `.csproj` and verify none have conditional/empty `TargetFramework` entries.
- Run a local `dotnet build` (if you want me to try from this environment) and paste the failing log so we can pinpoint the culprit.

---

Last updated: April 2026
