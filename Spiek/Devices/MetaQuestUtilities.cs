using Spiek.UI;
using Spiek.Utils;

namespace Spiek.Devices;

public class MetaQuestUtilities
{
    public static void PairMetaQuest()
    {
        if (MainForm.GetInstance().AlertConfirm("Do you need instructions on how to pair a headset?"))
        {
            MainForm.GetInstance().InfoAlert("Open the SPIEK app on the meta quest (In Unknown Sources).\nMake sure you are on the latest version.");
            MainForm.GetInstance().InfoAlert("Press 'Android Settings'");
            MainForm.GetInstance().InfoAlert("A dialog should appear, press 'Open'");
            MainForm.GetInstance().InfoAlert("Scroll down to 'System' and open it");
            MainForm.GetInstance().InfoAlert("Open 'Developer Options'");
            MainForm.GetInstance().InfoAlert("Scroll down to 'Wireless Debugging' and press the TEXT (not the button) (make sure the switch stays enabled)");
            MainForm.GetInstance().InfoAlert("Press 'Connect device with connection pin.");
        }

        string ipDebPort = MainForm.GetInstance().AlertTextInput("Enter the debugging IP and Port");
        string ipPairPort = MainForm.GetInstance().AlertTextInput("Enter the pairing IP and Port");
        string pin = MainForm.GetInstance().AlertTextInput("Enter the pin");

        (string result, bool success) = Command.Run("adb", $"pair {ipPairPort} {pin}");

        Console.WriteLine(result);
        MainForm.GetInstance().InfoAlert(result);

        (result, success) = Command.Run("adb", $"connect {ipDebPort}");

        Console.WriteLine(result);
        MainForm.GetInstance().InfoAlert(result);


    }

    public static List<string>? GetMetaQuestUsbDevices()
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
            string permission = args[1].Trim();

            if (permission.Contains("unauthorized"))
            {
                MainForm.GetInstance().ErrorAlert("Device requires permission, please accept the dialog in the headset and try again.");
            }


            devices.Add(serial);
        }

        return devices;
    }

    public static void PatchUsbMetaQuest()
    {
        MainForm main = MainForm.GetInstance();

        List<string>? devices = GetMetaQuestUsbDevices();
        if (devices == null)
        {
            main.ErrorAlert("Could not get list of devices. adb might not have been found.");
            return;
        }

        if (devices.Count == 0)
        {
            main.InfoAlert("No devices are attached.");
            return;
        }

        if (devices.Count == 1)
        {
            PatchOnDevice(new MetaQuest(devices.First()));
            return;
        }

        // #TODO show pick dialog
        string? device = main.PickDialog(devices.ToArray());
        if (device == null)
            return;

        PatchOnDevice(new MetaQuest(device));
    }

    private static void PatchOnDevice(MetaQuest questVRDevice)
    {
        Console.WriteLine($"Patching quest device {questVRDevice.GetUniqueAddress()}...");

        string apkPath = Path.Combine(
            AppContext.BaseDirectory,
            "patch",
            "Spiek.apk"
        );

        if (!File.Exists(apkPath))
        {
            MainForm.GetInstance().ErrorAlert($"Expected patch apk at {apkPath}, but it does not exist.");
            return;
        }

        string msg;
        bool result;
        // Install the app
        (msg, result) = questVRDevice.SendCommand($"install -r {apkPath}", 0);
        if (!result)
        {
            MainForm.GetInstance().ErrorAlert($"Failed to install apk: {msg}");
            return;
        }

        // Give permission
        (msg, result) = questVRDevice.SendCommand("shell pm grant dev.thesheep.spiekandroid android.permission.WRITE_SECURE_SETTINGS", 0);
        if (!result)
        {
            MainForm.GetInstance().ErrorAlert($"Failed to give permission for android.permission.WRITE_SECURE_SETTINGS: {msg}");
            return;
        }

        // Don't forget authorization after 7 days
        (msg, result) = questVRDevice.SendCommand("shell settings put global adb_allowed_connection_time -1", 0);
        if (!result)
        {
            MainForm.GetInstance().ErrorAlert($"Failed to bypass adb_allowed_connection_time: {msg}");
            return;
        }

        // Startup first time
        (msg, result) = questVRDevice.SendCommand("shell monkey -p dev.thesheep.spiekandroid 1", 0);
        if (!result)
        {
            MainForm.GetInstance().ErrorAlert($"Failed to start spiek on device: {msg}");
            return;
        }

        MainForm.GetInstance().InfoAlert("PatchUsbMetaQuest complete. Restart the headset for changes to take effect.");
    }

}
