using System.Security.Principal;

namespace IPFamilySwitcher.Services;

public sealed class PrivilegeService
{
    public bool IsAdministrator =>
        OperatingSystem.IsWindows() &&
        new WindowsPrincipal(WindowsIdentity.GetCurrent())
            .IsInRole(WindowsBuiltInRole.Administrator);
}
