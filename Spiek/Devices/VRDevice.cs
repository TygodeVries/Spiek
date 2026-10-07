using Spiek.Utils;
using System.Diagnostics;

namespace Spiek.Devices;

public abstract class VRDevice
{
    public abstract string UniqueAddressId();

    public DeviceState State { get; private set; }
    public void SetDeviceState(DeviceState newState)
    {
        State = newState;
        OnDeviceStateChange?.Invoke();
    }
    public event Action? OnDeviceStateChange;
    public event Action<Process>? OnCastStarted;
    public void RunCastStarted(Process process)
    {
        OnCastStarted?.Invoke(process);
    }

    public abstract string GetDeviceSerial();
    public abstract void Connect();
    public abstract void StartCast();
    public abstract bool IsScreenEnabled();
    public abstract void PowerDown();

    public void Fatal(string msg)
    {
        SetDeviceState(DeviceState.Error);
        OnFatal?.Invoke(msg);
    }

    public abstract string[] GetApps();

    public event Action<string>? OnFatal;
    public event Action? OnDisconnect;

    public abstract void StopCast();
    public abstract bool IsCasting();
    public void Disconnect()
    {
        Console.WriteLine("AHHHH I disconnected");
        SetDeviceState(DeviceState.Disconnected);
        OnDisconnect?.Invoke();
    }

    public abstract int GetBatteryLevel();

    public DeviceInfo GetDeviceInfo()
    {
        DeviceInfo? info = Settings.Instance.Devices.FirstOrDefault((o) =>
        {
            return o.DeviceId == GetDeviceSerial();
        });

        if (info == null)
        {
            info = new DeviceInfo()
            {
                DeviceId = GetDeviceSerial(),
                HexColor = "#FF0000",
                Name = "Unnamed"
            };
        }

        return info;
    }
}

public enum DeviceState
{
    Disconnected,
    Connecting,
    Error,
    Connected
}