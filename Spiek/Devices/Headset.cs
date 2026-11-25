using Spiek.Utils;
using System.Diagnostics;

namespace Spiek.Devices;

public abstract class Headset
{
    public abstract string GetUniqueAddress();

    public DeviceState State { get; private set; }
    public void SetDeviceState(DeviceState newState)
    {
        Console.ForegroundColor = ConsoleColor.Yellow;
        Log($"changed state {State} -> {newState}.");
        Console.ForegroundColor = ConsoleColor.White;

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
    public abstract void StartVideoStream();
    public abstract bool IsScreenEnabled();
    public abstract void PowerDown();

    public event Action? OnDisconnect;

    public abstract void StopVideoStream();
    public abstract bool IsCasting();
    public void Disconnect()
    {
        Log("Device Disconnected.");
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

    public void Log(string msg)
    {
        Console.WriteLine($"{GetUniqueAddress()} -> {msg}");
    }
}

public enum DeviceState
{
    Disconnected,
    Connecting,
    Error,
    Connected
}