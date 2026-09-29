using Spiek.Devices;
using Spiek.UI;

public class Program
{
    /*
     * Problem: It seems that the quests are merging their ips and ports when multiple connected.
     * When a new headset is connecting, all other onces crash. 
     */



    // #TODO make sure to have permission to listen on UDP: netsh advfirewall firewall add rule name="Quest Spiek UDP Bridge" dir=in action=allow protocol=UDP localport=9999


    /* Todo, setup the link app:
     * 
     * Accessability, because we need to pretend to click allow
     * adb shell settings put secure enabled_accessibility_services dev.thesheep.spiek/.AdbAutoAllowService
     * adb shell settings put secure accessibility_enabled 1
     * 
     * Allow running adb
     * adb shell pm grant dev.thesheep.spiek android.permission.WRITE_SECURE_SETTINGS
     */
    public static string settings = "--stay-awake --keep-active --no-audio -b 6M --crop %width%:%height%:%x%:%y% --video-codec=h264 --max-size=800 --max-fps=20 --window-title \"Quest\" -s %addr%";
    public static QuestDiscover? questDiscover;
    public static void Main(string[] args)
    {
        questDiscover = new QuestDiscover();

        questDiscover.OnMessage += (result) =>
        {
            Console.WriteLine(result.message);

            // QUEST_DebugPort_12cfe1252388b1b5_45905
            string[] args = result.message.Split("_");

            if (args[0] != "QUEST")
                return;

            if (args[1] != "DebugPort")
                return;

            QuestVRDevice questVRDevice = new QuestVRDevice(args[2]);
            questVRDevice.ForceAddress(result.ip, int.Parse(args[3]));

            Console.WriteLine($"{questVRDevice.Ip}:{questVRDevice.Port} over network ping!");
            MainForm.GetInstance().AddDevice(questVRDevice);
        };

        questDiscover.Start();

        Application.SetHighDpiMode(HighDpiMode.PerMonitorV2);
        Application.SetDefaultFont(new Font("Segoe UI", 9f));
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);

        MainForm mainForm = new MainForm();
        mainForm.Setup();
        Console.ReadLine();
    }
}
