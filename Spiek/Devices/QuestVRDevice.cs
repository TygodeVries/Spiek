using Spiek.UI;
using Spiek.Utils;
using System.Diagnostics;
using System.Text.RegularExpressions;

namespace Spiek.Devices;

public class QuestVRDevice : VRDevice
{
    public static List<string>? GetUsbDevices()
    {
        (string msg, bool success) = Command.Run("adb", $"devices");

        if (!success)
        {
            Console.WriteLine($"Could not get list of connected devices because {msg}");
            return null;
        }

        string[] lines = msg.Split("\n"); // Split on enters.

        int deviceCount = lines.Length - 3;

        if (deviceCount == 0)
        {
            return new List<string>();
        }

        List<string> devices = new List<string>();
        for (int i = 0; i < deviceCount; i++)
        {
            string line = lines[i + 1];

            // Its an IP, not correct!
            if (line.Contains('.'))
            {
                continue;
            }

            if (line == "not-found")
            {
                return new List<string>();
            }

            string[] args = line.Split('\t', StringSplitOptions.RemoveEmptyEntries);
            string serial = args[0].Trim();
            devices.Add(serial);
        }

        return devices;
    }

    public string? Serial { get; private set; }
    public string? Ip { get; private set; }
    public int? Port { get; private set; }


    /// <summary>
    /// Used for checking duplication.
    /// </summary>
    /// <returns></returns>
    public override string UniqueAddressId()
    {
        if (Serial != null)
            return Serial;

        return $"{Ip}:{Port}";
    }
    public QuestVRDevice(string serial)
    {
        this.Serial = serial;
    }

    public QuestVRDevice(string ip, int port)
    {
        this.Ip = ip;
        this.Port = port;
    }

    public void ForceAddress(string ip, int port)
    {
        this.Ip = ip;
        this.Port = port;
    }

    public override void Connect()
    {
        SetDeviceState(DeviceState.Connecting);
        string msg;
        bool success;
        // If the IP is null, we first need to optain that.
        if (Ip == null)
        {
            Console.WriteLine("IP is null...");
            if (Serial == null)
                throw new Exception("Both device serial and IP are unknown. The connection can not be made.");

            (msg, success) = Command.Run("adb", $"-s {Serial} shell ip route");

            if (!success)
            {
                if (msg.Contains("not found"))
                {
                    Fatal($"adb could not find the USB connected device {Serial}. Are you sure its still plugged in?");
                    return;
                }

                Fatal($"Something went wrong connecting to {Serial} device. msg: '{msg}'");
                return;
            }

            string[] args = msg.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            Ip = args[8];

            const int DEBUG_PORT = 5555;

            // Open the adb
            (msg, success) = Command.Run("adb", $"-s {Serial} tcpip {DEBUG_PORT}");
            Port = DEBUG_PORT;

            // Check again incase someone removes the USB half-way
            if (!success)
            {
                if (msg.Contains("not found"))
                {
                    Fatal($"adb could not find the USB connected device while setting DEBUG_PORT {Serial}. Are you sure its still plugged in?");
                    return;
                }

                Fatal($"Something went wrong connecting to {Serial} device while setting DEBUG_PORT. msg: '{msg}'");
                return;
            }

            // Wait for connection to finalize.
            Thread.Sleep(500);
        }


        // Connect
        (msg, success) = Command.Run("adb", $"connect {Ip}:{Port}");
        if (!success)
        {
            Fatal($"Failed to connect to device because: {msg}");
            return;
        }

        if (msg.Contains("cannot connect to"))
        {
            Fatal(msg);
            return;
        }

        if (msg.Contains("failed to connect"))
        {
            Fatal(msg);
            return;
        }

        Thread.Sleep(500);

        SetDeviceState(DeviceState.Connected);

        bool enabled = IsScreenEnabled();
        Console.WriteLine(enabled);
        if (!enabled)
        {
            Console.WriteLine($"[!] Connected with {GetAddress()} but the screen is not enabled.");
        }
    }

    private readonly object castLock = new object();
    private bool startingCast = false;
    public override void StartCast()
    {
        lock (castLock)
        {
            if (startingCast || cast != null)
                return;

            startingCast = true;
        }

        Thread thr = new Thread(() =>
        {
            Process? prc = null;

            try
            {
                Console.WriteLine("Starting Cast...");

                if (State != DeviceState.Connected)
                {
                    Console.WriteLine(
                        "Cannot start stream if the device is not connected.");
                    return;
                }

                int battery = GetBatteryLevel();

                if (battery != -1 && battery <= 20)
                {
                    MainForm.GetInstance().InfoAlert(
                        "Stream might not function since battery level is below 20%.");
                }

                Console.WriteLine($"Battery is {battery}.");

                var size = GetCrop();

                if (size == null)
                {
                    MainForm.GetInstance().InfoAlert(
                        "Could not obtain display resolution required for casting.");

                    size = (0, 0, 1000, 500);
                }

                string args = Settings.Instance.SrcpyLaunchArguments
                    .Replace("%addr%", GetAddress())
                    .Replace("%width%", size.Value.width.ToString())
                    .Replace("%height%", size.Value.height.ToString())
                    .Replace("%x%", size.Value.x.ToString())
                    .Replace("%y%", size.Value.y.ToString());

                Console.WriteLine("Starting Casting Application...");

                (prc, string msg) =
                    Command.RunGetPrc("programs/scrcpy.exe", args);

                if (prc == null)
                {
                    Fatal("Failed to start cast because: " + msg);
                    return;
                }

                Console.WriteLine("Waiting for window...");

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
                    Console.WriteLine("Casting window did not appear in time. Terminating scrcpy.");

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

                    Fatal("Could not obtain casting window. scrcpy did not open a window.");
                    return;
                }

                lock (castLock)
                {
                    cast = prc;
                    prc = null;
                }

                Console.WriteLine("Window found!");

                RunCastStarted(cast);
            }
            catch (Exception e)
            {
                MainForm.GetInstance().ErrorAlert(
                    "Failed to start casting: " + e);
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

                lock (castLock)
                {
                    startingCast = false;
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

    public override void StopCast()
    {
        cast?.Close();

        if (IsCasting())
        {
            cast = null;
        }
    }

    public bool ErrorIfNotConnected(string msg)
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

    public string GetAddress()
    {
        if (Ip == null && Serial == null)
            throw new NullReferenceException("Both IP and Serial of device is unknown.");

        if (Ip == null)
            return Serial!;
        return $"{Ip}:{Port}";
    }

    public string? SendCommand(string command, bool bypassConnection = false, bool dontDisconnectIfIncorrect = false)
    {
        if (!bypassConnection)
            ErrorIfNotConnected("Can not send commands to this device if its not connected.");

        (string msg, bool success) = Command.Run("adb", $"-s {GetAddress()} {command}");
        last = success;

        if (!success && !dontDisconnectIfIncorrect)
        {
            Disconnect();
            return null;
        }

        return msg;
    }

    public (string msg, bool success) SendCommand(string command, int i = 0)
    {
        (string msg, bool success) = Command.Run("adb", $"-s {GetAddress()} {command}");
        last = success;

        return (msg, success);
    }

    public async Task<string?> SendCommandAsync(string command, bool bypassConnection = false, bool dontDisconnectIfIncorrect = false)
    {
        if (!bypassConnection)
            ErrorIfNotConnected("Can not send commands to this device if its not connected.");

        (string msg, bool success) = await Command.RunAsync("adb", $"-s {GetAddress()} {command}");
        last = success;

        if (!success && !dontDisconnectIfIncorrect)
        {
            Disconnect();
            return null;
        }

        return msg;
    }

    private bool last = false;
    public bool LastCommandSuccess()
    {
        return last;
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

    public (int x, int y, int width, int height)? GetCrop()
    {
        if (ErrorIfNotConnected("The device has to be connected to get its display resolution."))
        {
            return null;
        }

        (string msg, bool success) = Command.Run("scrcpy", $"--list-displays -s {GetAddress()}");
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


    public string? GetMacAddress()
    {
        if (ErrorIfNotConnected("The device has to be connected to get its MAC address."))
        {
            return null;
        }

        string? result = SendCommand("shell ip addr show wlan0", false);
        if (string.IsNullOrEmpty(result))
            return null;

        Match match = Regex.Match(
            result,
            @"([0-9A-Fa-f]{2}:){5}[0-9A-Fa-f]{2}",
            RegexOptions.IgnoreCase
        );

        return match.Success ? match.Value : null;
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

    public override string[] GetApps()
    {
        throw new NotImplementedException();
    }
}
