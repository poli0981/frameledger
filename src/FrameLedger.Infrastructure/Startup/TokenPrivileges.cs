using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.Security;

namespace FrameLedger.Infrastructure.Startup;

/// <summary>
/// The privileges this process's token has ENABLED, by name — read, never changed (beta.10, the Agent's admin mode, D34).
/// </summary>
/// <remarks>
/// An elevated token HOLDS powerful privileges (debug, load-driver) disabled; enabling one takes an explicit
/// <c>AdjustTokenPrivileges</c>, which nothing in <c>src/</c> may call (<c>tools/chokepoint-check.ps1</c>). This is the
/// other half: the Agent logs what is enabled when it starts elevated, and a test on an elevated runner asserts that
/// opening a target and the CPU sensors enabled nothing — third-party code included.
/// </remarks>
public static class TokenPrivileges
{
    /// <summary>The enabled privileges' names (<c>SeChangeNotifyPrivilege</c>, …), or empty when the token could not be read.</summary>
    public static unsafe IReadOnlyList<string> Enabled()
    {
        HANDLE token;
        if (!PInvoke.OpenProcessToken(PInvoke.GetCurrentProcess(), TOKEN_ACCESS_MASK.TOKEN_QUERY, &token))
        {
            return [];
        }

        try
        {
            // Written through a pointer the analyzers cannot follow (CA1508 would call the size check dead).
            uint* length = stackalloc uint[1];
            _ = PInvoke.GetTokenInformation(token, TOKEN_INFORMATION_CLASS.TokenPrivileges, null, 0, length);
            if (*length == 0)
            {
                return [];
            }

            byte[] buffer = new byte[*length];
            fixed (byte* p = buffer)
            {
                if (!PInvoke.GetTokenInformation(token, TOKEN_INFORMATION_CLASS.TokenPrivileges, p, (uint)buffer.Length, length))
                {
                    return [];
                }

                var privileges = (TOKEN_PRIVILEGES*)p;
                LUID_AND_ATTRIBUTES* each = (LUID_AND_ATTRIBUTES*)&privileges->Privileges;
                var names = new List<string>((int)privileges->PrivilegeCount);
                for (uint i = 0; i < privileges->PrivilegeCount; i++)
                {
                    if (each[i].Attributes.HasFlag(TOKEN_PRIVILEGES_ATTRIBUTES.SE_PRIVILEGE_ENABLED) && NameOf(each[i].Luid) is { } name)
                    {
                        names.Add(name);
                    }
                }

                return names;
            }
        }
        finally
        {
            _ = PInvoke.CloseHandle(token);
        }
    }

    /// <summary>
    /// Whether process <paramref name="pid"/> runs elevated (<c>TokenElevation</c>), or null when its token cannot be read —
    /// how the Agent says in its log that a game it started runs with a standard user's rights, and how a test checks it.
    /// </summary>
    public static unsafe bool? IsElevated(int pid)
    {
        HANDLE process = PInvoke.OpenProcess(Windows.Win32.System.Threading.PROCESS_ACCESS_RIGHTS.PROCESS_QUERY_LIMITED_INFORMATION, false, (uint)pid);
        if (process.IsNull)
        {
            return null;
        }

        HANDLE token = default;
        try
        {
            if (!PInvoke.OpenProcessToken(process, TOKEN_ACCESS_MASK.TOKEN_QUERY, &token))
            {
                return null;
            }

            TOKEN_ELEVATION elevation;
            uint* length = stackalloc uint[1];
            return PInvoke.GetTokenInformation(token, TOKEN_INFORMATION_CLASS.TokenElevation, &elevation, (uint)sizeof(TOKEN_ELEVATION), length)
                ? elevation.TokenIsElevated != 0
                : null;
        }
        finally
        {
            if (!token.IsNull)
            {
                _ = PInvoke.CloseHandle(token);
            }

            _ = PInvoke.CloseHandle(process);
        }
    }

    private static unsafe string? NameOf(LUID luid)
    {
        char* name = stackalloc char[64];
        uint length = 64;
        return PInvoke.LookupPrivilegeName(default, &luid, new PWSTR(name), &length) ? new string(name, 0, (int)length) : null;
    }
}
