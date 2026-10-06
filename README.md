# IP Family Switcher

IP Family Switcher is a Windows 10 22H2+/Windows 11 x64 WPF utility for applying per-executable IP-family restrictions through Windows Defender Firewall rules.

## Safety model

- IPv4 Only blocks outbound IPv6 (`::/0`) for the selected executable.
- IPv6 Only blocks outbound IPv4 (`0.0.0.0/0`) for the selected executable.
- Default removes the application-managed family rule.
- Rules are persistent and owned only when they use the `IPFamilySwitcher.ManagedRules` group and the application's stable GUID.
- Unrelated Windows, launcher, antivirus, and system rules are not modified.

Configuration is stored under `%ProgramData%\IPFamilySwitcher\config.json`; rolling logs are stored under `%ProgramData%\IPFamilySwitcher\logs\`. The executable requests administrator elevation because Windows Firewall rule management requires it.

## Build and test

```powershell
dotnet restore IPFamilySwitcher.slnx
dotnet test IPFamilySwitcher.Tests\IPFamilySwitcher.Tests.csproj --configuration Release
dotnet publish IPFamilySwitcher\IPFamilySwitcher.csproj --configuration Release --runtime win-x64 --self-contained true --output Release
```

To create the portable archive:

```powershell
.\scripts\publish.ps1
```

The native firewall integration requires Windows and administrator privileges. Unit tests do not modify the live firewall.

## Manual safety checklist

1. Add an `.exe` and confirm it starts in Default with no managed rule.
2. Select IPv4 Only and confirm the owned rule targets `::/0` and the executable path.
3. Close and reopen the app; confirm the rule remains and is reported Active.
4. Delete the owned rule externally, refresh, and use Repair.
5. Switch back to Default and confirm only the owned rule is removed.
6. Create an unrelated firewall rule and confirm Remove All Rules leaves it unchanged.
