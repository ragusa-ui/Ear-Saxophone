using System.Runtime.InteropServices;
using System.Text;

namespace EarSaxophone.Windows.Audio;

/// <summary>Resolves the executable path of a process id with the least privilege that Windows allows.</summary>
internal static class ProcessPath
{
    private const uint ProcessQueryLimitedInformation = 0x1000;

    public static string? TryGet(int processId)
    {
        if (processId <= 0)
        {
            return null;
        }

        IntPtr handle = OpenProcess(ProcessQueryLimitedInformation, false, (uint)processId);
        if (handle == IntPtr.Zero)
        {
            return null;
        }

        try
        {
            var builder = new StringBuilder(1024);
            int size = builder.Capacity;
            return QueryFullProcessImageName(handle, 0, builder, ref size) ? builder.ToString(0, size) : null;
        }
        finally
        {
            CloseHandle(handle);
        }
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr OpenProcess(uint desiredAccess, [MarshalAs(UnmanagedType.Bool)] bool inheritHandle, uint processId);

    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool QueryFullProcessImageName(IntPtr process, uint flags, StringBuilder exeName, ref int size);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CloseHandle(IntPtr handle);
}
