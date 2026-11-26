using System.Diagnostics;
using System.Runtime.InteropServices;

public static class ProcessOptimizer
{
    private const int ProcessPowerThrottling = 4;
    private const uint PROCESS_POWER_THROTTLING_CURRENT_VERSION = 1;
    private const uint PROCESS_POWER_THROTTLING_EXECUTION_SPEED = 0x1;

    [StructLayout(LayoutKind.Sequential)]
    private struct PROCESS_POWER_THROTTLING_STATE
    {
        public uint Version;
        public uint ControlMask;
        public uint StateMask;
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool SetProcessInformation(
        IntPtr hProcess,
        int ProcessInformationClass,
        ref PROCESS_POWER_THROTTLING_STATE ProcessInformation,
        int ProcessInformationSize);

    public static void DisableBackgroundThrottling()
    {
        var state = new PROCESS_POWER_THROTTLING_STATE
        {
            Version = PROCESS_POWER_THROTTLING_CURRENT_VERSION,
            ControlMask = PROCESS_POWER_THROTTLING_EXECUTION_SPEED,
            StateMask = 0 // 0 disables throttling
        };

        SetProcessInformation(
            Process.GetCurrentProcess().Handle,
            ProcessPowerThrottling,
            ref state,
            Marshal.SizeOf(state));

        // Optional: Elevate process priority so the OS scheduler favors your render thread
        Process.GetCurrentProcess().PriorityClass = ProcessPriorityClass.AboveNormal;
    }
}