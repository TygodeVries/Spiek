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


    public void Fatal(string msg)
    {
        SetDeviceState(DeviceState.Error);
        OnFatal?.Invoke(msg);
    }

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
}

public enum DeviceState
{
    Disconnected,
    Connecting,
    Error,
    Connected
}