using Spiek.UI;
using Spiek.Utils;
using System.Diagnostics;
using System.Text.RegularExpressions;

namespace Spiek.Devices;

public class MetaQuest : Headset
{
    private string? Serial;
    private string? Ip;
    private int? Port;

    public MetaQuest(string serial)
    {
        this.Serial = serial;
    }
    public MetaQuest(string ip, int port)
    {
        this.Ip = ip;
        this.Port = port;
    }

    public override string GetUniqueAddress()
    {
        if (Ip != null && Port != null)
            return $"{Ip}:{Port}";

        return Serial!; // Serial as fallback. Prio is IP
    }

    internal void SetInternetAddress(string ip, int port)
    {
        this.Ip = ip;
        this.Port = port;
    }

    /// <summary>
    /// Turn this device from a USB-wired connection to a network connection.
    /// </summary>
    /// <exception cref="Exception"></exception>
    public bool SetupWirelessFromUsb()
    {
        Log("Attempting to connect over the internet to the USB-device...");
        if (Serial == null)
            throw new Exception("Both device serial and IP are unknown. The connection can not be made.");

        // Get the networking info
        (string msg, bool success) = Command.Run("adb", $"-s {Serial} shell ip route");

        if (!success)
        {
            if (msg.Contains("not found"))
            {
                Log($"adb could not find the USB connected device {Serial}. Are you sure its still plugged in?");
            }

            Log($"Something went wrong connecting to {Serial} device. msg: '{msg}'");
            return false;
        }

        string[] args = msg.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        Ip = args[8]; // Set the IP of the device

        const int DEBUG_PORT = 5555;

        // Open the android debugging over wifi
        (msg, success) = Command.Run("adb", $"-s {Serial} tcpip {DEBUG_PORT}");
        Port = DEBUG_PORT;

        // Check again incase someone removes the USB half-way
        if (!success)
        {
            if (msg.Contains("not found"))
            {
                Log($"adb could not find the USB connected device while setting DEBUG_PORT {Serial}. Are you sure its still plugged in?");
            }

            Log($"Something went wrong connecting to {Serial} device while setting DEBUG_PORT. msg: '{msg}'");
            return false;
        }

        // Wait for connection to finalize.
        Thread.Sleep(500);

        return true;
    }

    /// <summary>
    /// Connect over wifi to the device.
    /// </summary>
    public override void Connect()
    {
        SetDeviceState(DeviceState.Connecting);
        Log("Starting to connect over ADB...");
        string msg;
        bool success;

        // For USB connected devices, we need to optain an IP
        if (Ip == null)
        {
            if (!SetupWirelessFromUsb())
                OnConnectingFailed();
        }

        // Connect over net
        (msg, success) = Command.Run("adb", $"connect {Ip}:{Port}");

        bool failedToConnect = !success || msg.Contains("cannot connect to") || msg.Contains("failed to connect");

        if (failedToConnect)
        {
            OnConnectingFailed();
            return;
        }

        Log("Starting connection test...");
        if (TestConnectionState())
        {
            SetDeviceState(DeviceState.Connected);
        }
    }

    private bool TestConnectionState()
    {
        // Test the connection
        (string msg, bool success) = Command.Run("adb", $"-s {GetUniqueAddress()} shell dumpsys power");
        if (!success || msg.Contains("error"))
        {
            OnConnectingFailed();
            return false;
        }

        return true;
    }

    private void OnConnectingFailed()
    {
        Log("Connecting Failed, running automatic diagnosis...");
        SetDeviceState(DeviceState.Error);

        if (Port == null || Ip == null)
        {
            Log($"Error caused by: Missing internet info: {Ip}:{Port}");
            return;
        }

        Log("Error caused by: Adb refused by headset, required to re-pair");

        if (MainForm.GetInstance().AlertConfirm("The headset did not accept the connection, do you want to pair again?"))
        {
            string code = MainForm.GetInstance().AlertTextInput($"The headset at address {GetUniqueAddress()} requires to be paired. Please enter the pair code from the developer settings.");
            (string result, bool success) = Command.Run("adb", $"pair {Ip}:{Port} {code}");

            Log(result);
            MainForm.GetInstance().InfoAlert(result);
        }
        else
        {
            Log("Re-pair skipped.");
        }
    }

    private bool isStartingVideoStream = false;
    private readonly object videoStreamThreadLock = new object();
    public override void StartVideoStream()
    {
        Log("Starting Video Stream.");

        // Make sure we don't start streaming twice.
        lock (videoStreamThreadLock)
        {
            if (isStartingVideoStream || cast != null)
                return;

            isStartingVideoStream = true;
        }

        Thread thr = new Thread(() =>
        {
            Process? prc = null;

            try
            {
                if (State != DeviceState.Connected)
                {
                    Log("Cannot start stream if the device is not connected.");
                    return;
                }

                int battery = GetBatteryLevel();

                if (battery != -1 && battery <= 20)
                {
                    Log("Stream might not function since battery level is below 20%.");
                }


                var size = GetVideoCrop();

                if (size == null)
                {
                    MainForm.GetInstance().InfoAlert(
                        "Could not obtain display resolution required for casting.");

                    size = (0, 0, 1000, 500);
                }

                string args = Settings.Instance.SrcpyLaunchArguments
                    .Replace("%addr%", GetUniqueAddress())
                    .Replace("%width%", size.Value.width.ToString())
                    .Replace("%height%", size.Value.height.ToString())
                    .Replace("%x%", size.Value.x.ToString())
                    .Replace("%y%", size.Value.y.ToString());

                Log("Launching scrcpy...");

                (prc, string msg) =
                    Command.RunGetPrc("programs/scrcpy.exe", args);

                if (prc == null)
                {
                    Log("Failed to start cast because: " + msg);
                    Disconnect();
                    return;
                }

                IntPtr childHandle = IntPtr.Zero;

                for (int attempts = 0; attempts < 100 && childHandle == IntPtr.Zero; attempts++)
                {
                    if (prc.HasExited)
                        break;

                    prc.Refresh();
                    childHandle = prc.MainWindowHandle;

                    if (childHandle == IntPtr.Zero)
                        Thread.Sleep(200);
                }

                if (childHandle == IntPtr.Zero)
                {
                    Log("Casting window did not appear in time. Terminating scrcpy.");

                    try
                    {
                        if (!prc.HasExited)
                        {
                            prc.Kill();
                            prc.WaitForExit(2000);
                        }
                    }
                    catch (Exception e)
                    {
                        Console.WriteLine(
                            "Failed to terminate scrcpy: " + e.Message);
                    }

                    Log("Could not obtain casting window. scrcpy did not open a window.");
                    Disconnect();
                    return;
                }

                lock (videoStreamThreadLock)
                {
                    cast = prc;
                    prc = null;
                }

                Log("Streaming Window Found!");

                RunCastStarted(cast);
            }
            catch (Exception e)
            {
                Log("Failed to start casting: " + e);
            }
            finally
            {
                if (prc != null)
                {
                    try
                    {
                        if (!prc.HasExited)
                        {
                            prc.Kill();
                            prc.WaitForExit(2000);
                        }
                    }
                    catch (Exception e)
                    {
                        Console.WriteLine(
                            "Process cleanup failed: " + e.Message);
                    }
                    finally
                    {
                        prc.Dispose();
                    }
                }

                lock (videoStreamThreadLock)
                {
                    isStartingVideoStream = false;
                }
            }
        });

        thr.IsBackground = true;
        thr.Start();
    }
    private Process? cast = null;

    public override bool IsCasting()
    {
        try
        {
            return !(cast == null || cast.HasExited);
        }
        catch (Exception e)
        {
            return false;
        }
    }

    public override void StopVideoStream()
    {
        cast?.Close();
        cast = null;

        Log("Video Stream Stopped");
    }

    private bool ErrorIfNotConnected(string msg)
    {
        if (State != DeviceState.Connected)
        {
            Console.WriteLine("The device has to be connected.");
            Console.WriteLine(msg);

            Disconnect();
            return true;
        }

        return false;
    }

    public string? SendCommand(string command, bool bypassConnection = false, bool dontDisconnectIfIncorrect = false)
    {
        if (!bypassConnection)
            ErrorIfNotConnected("Can not send commands to this device if its not connected.");

        (string msg, bool success) = Command.Run("adb", $"-s {GetUniqueAddress()} {command}");
        lastCommandSuccess = success;

        if (!success && !dontDisconnectIfIncorrect)
        {
            Disconnect();
            return null;
        }

        return msg;
    }

    public (string msg, bool success) SendCommand(string command, int i = 0)
    {
        (string msg, bool success) = Command.Run("adb", $"-s {GetUniqueAddress()} {command}");
        lastCommandSuccess = success;

        return (msg, success);
    }

    public async Task<string?> SendCommandAsync(string command, bool bypassConnection = false, bool dontDisconnectIfIncorrect = false)
    {
        if (!bypassConnection)
            ErrorIfNotConnected("Can not send commands to this device if its not connected.");

        (string msg, bool success) = await Command.RunAsync("adb", $"-s {GetUniqueAddress()} {command}");
        lastCommandSuccess = success;

        if (!success && !dontDisconnectIfIncorrect)
        {
            Disconnect();
            return null;
        }

        return msg;
    }

    private bool lastCommandSuccess = false;
    internal bool IsLastCommandSuccess()
    {
        return lastCommandSuccess;
    }

    public override int GetBatteryLevel()
    {
        string? result = SendCommand("shell dumpsys battery", false);
        if (result == null)
            return -1;

        int level = int.Parse(
            Regex.Match(result, @"^\s*level:\s*(\d+)", RegexOptions.Multiline).Groups[1].Value
        );

        return level;
    }

    public (int x, int y, int width, int height)? GetVideoCrop()
    {
        if (ErrorIfNotConnected("The device has to be connected to get its display resolution."))
        {
            return null;
        }

        (string msg, bool success) = Command.Run("scrcpy", $"--list-displays -s {GetUniqueAddress()}");
        if (!success)
        {
            return null;
        }

        foreach (string line in msg.Split("\n"))
        {
            if (line.Trim().StartsWith("--display-id=0"))
            {
                Match match = Regex.Match(line.Trim(), @"\((\d+)x(\d+)\)");

                if (!match.Success)
                {
                    return null;
                }

                int displayWidth = int.Parse(match.Groups[1].Value);
                int displayHeight = int.Parse(match.Groups[2].Value);

                // One eye = half the display width.
                int eyeWidth = displayWidth / 2;

                // Calculate the height required to maintain 16:9.
                int cropHeight = (int)Math.Round(eyeWidth * 9.0 / 16.0);

                // Center vertically.
                int cropY = (displayHeight - cropHeight) / 2;

                // Assuming the left eye.
                int cropX = 0;

                return (cropX, cropY, eyeWidth, cropHeight);
            }
        }

        return null;
    }

    public override bool IsScreenEnabled()
    {
        string? result = SendCommand("shell dumpsys power", false);
        if (result == null)
            return false;

        return Regex.IsMatch(
            result,
            @"^\s*mWakefulness\s*=\s*Awake\s*$",
            RegexOptions.IgnoreCase | RegexOptions.Multiline
        );

    }

    public override string GetDeviceSerial()
    {
        string? result = SendCommand("shell getprop ro.serialno", false);

        if (!string.IsNullOrWhiteSpace(result))
            return result.Trim();

        result = SendCommand("shell getprop ro.boot.serialno", false);

        return result?.Trim() ?? string.Empty;
    }

    public override void PowerDown()
    {
        SendCommand("shell reboot -p", false);
    }
}
