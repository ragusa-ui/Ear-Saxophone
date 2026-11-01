using System.Runtime.InteropServices;
using EarSaxophone.Core.Audio;
using EarSaxophone.Core.Localization;

namespace EarSaxophone.Windows.Audio;

/// <summary>
/// Sets the Windows default audio device. Windows offers no documented API for this; the control panel and tools such as
/// SoundSwitch use the internal IPolicyConfig interface. It exists on Windows 10 and 11 but is not guaranteed to stay unchanged
/// by Windows updates, so every failure is reported as "unsupported" instead of crashing.
/// </summary>
internal static class PolicyConfig
{
    private static readonly Guid ClsidPolicyConfigClient = new("870af99c-171d-4f9e-af0d-e63df40c2bc9");

    private static readonly int[] AllRoles = [0 /* eConsole */, 1 /* eMultimedia */, 2 /* eCommunications */];

    public static void SetDefaultForAllRoles(string deviceId)
    {
        object? instance = null;
        try
        {
            Type type = Type.GetTypeFromCLSID(ClsidPolicyConfigClient, throwOnError: true)!;
            instance = Activator.CreateInstance(type);
            var policy = (IPolicyConfig)instance!;
            foreach (int role in AllRoles)
            {
                int hresult = policy.SetDefaultEndpoint(deviceId, role);
                if (hresult < 0)
                {
                    throw new AudioUnsupportedException(Texts.WindowsReported(hresult), Marshal.GetExceptionForHR(hresult));
                }
            }
        }
        catch (COMException ex)
        {
            throw new AudioUnsupportedException(Texts.WindowsReported(ex.HResult), ex);
        }
        catch (InvalidCastException ex)
        {
            throw new AudioUnsupportedException(Texts.SwitchInterfaceMissing, ex);
        }
        finally
        {
            if (instance is not null && Marshal.IsComObject(instance))
            {
                Marshal.ReleaseComObject(instance);
            }
        }
    }

    // Vtable order must match the Windows interface exactly; only SetDefaultEndpoint is used.
    [ComImport]
    [Guid("F8679F50-850A-41CF-9C72-430F290290C8")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IPolicyConfig
    {
        [PreserveSig] int GetMixFormat([MarshalAs(UnmanagedType.LPWStr)] string deviceName, IntPtr format);

        [PreserveSig] int GetDeviceFormat([MarshalAs(UnmanagedType.LPWStr)] string deviceName, [MarshalAs(UnmanagedType.Bool)] bool defaultFormat, IntPtr format);

        [PreserveSig] int ResetDeviceFormat([MarshalAs(UnmanagedType.LPWStr)] string deviceName);

        [PreserveSig] int SetDeviceFormat([MarshalAs(UnmanagedType.LPWStr)] string deviceName, IntPtr endpointFormat, IntPtr mixFormat);

        [PreserveSig] int GetProcessingPeriod([MarshalAs(UnmanagedType.LPWStr)] string deviceName, [MarshalAs(UnmanagedType.Bool)] bool defaultPeriod, IntPtr defaultValue, IntPtr minimumValue);

        [PreserveSig] int SetProcessingPeriod([MarshalAs(UnmanagedType.LPWStr)] string deviceName, IntPtr period);

        [PreserveSig] int GetShareMode([MarshalAs(UnmanagedType.LPWStr)] string deviceName, IntPtr mode);

        [PreserveSig] int SetShareMode([MarshalAs(UnmanagedType.LPWStr)] string deviceName, IntPtr mode);

        [PreserveSig] int GetPropertyValue([MarshalAs(UnmanagedType.LPWStr)] string deviceName, [MarshalAs(UnmanagedType.Bool)] bool fxStore, IntPtr key, IntPtr value);

        [PreserveSig] int SetPropertyValue([MarshalAs(UnmanagedType.LPWStr)] string deviceName, [MarshalAs(UnmanagedType.Bool)] bool fxStore, IntPtr key, IntPtr value);

        [PreserveSig] int SetDefaultEndpoint([MarshalAs(UnmanagedType.LPWStr)] string deviceName, int role);

        [PreserveSig] int SetEndpointVisibility([MarshalAs(UnmanagedType.LPWStr)] string deviceName, [MarshalAs(UnmanagedType.Bool)] bool visible);
    }
}
