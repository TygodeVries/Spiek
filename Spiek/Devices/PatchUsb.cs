using Spiek.UI;

namespace Spiek.Devices;

public class PatchUsb
{
    public static void Patch()
    {
        MainForm main = MainForm.GetInstance();

        List<string>? devices = QuestVRDevice.GetUsbDevices();
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
            PatchOnDevice(new QuestVRDevice(devices.First()));
            return;
        }

        // #TODO show pick dialog
        string? device = main.PickDialog(devices.ToArray());
        if (device == null)
            return;

        PatchOnDevice(new QuestVRDevice(device));
    }

    private static void PatchOnDevice(QuestVRDevice questVRDevice)
    {
        Console.WriteLine($"Patching quest device {questVRDevice.Serial}...");

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

        MainForm.GetInstance().InfoAlert("Patch complete. Restart the headset for changes to take effect.");
    }

}
