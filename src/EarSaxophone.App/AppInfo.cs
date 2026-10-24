using System.Reflection;

namespace EarSaxophone.App;

internal static class AppInfo
{
    /// <summary>Program version without the build suffix, for example "1.0.0" or "1.0.0-rc1".</summary>
    public static string Version
    {
        get
        {
            string? version = typeof(AppInfo).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
            if (string.IsNullOrEmpty(version))
            {
                return "?";
            }

            int plus = version.IndexOf('+');
            return plus > 0 ? version[..plus] : version;
        }
    }
}
