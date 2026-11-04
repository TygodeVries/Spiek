using Spiek.Devices;
using Spiek.UI;
using System.Diagnostics;

public class Program
{
    public static bool FirewallRuleExists(string ruleName)
    {
        var psi = new ProcessStartInfo
        {
            FileName = "netsh",
            Arguments = $"advfirewall firewall show rule name=\"{ruleName}\"",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using var process = Process.Start(psi);
        process.WaitForExit();

        return process.ExitCode == 0;
    }

    // #TODO make sure to have permission to listen on UDP: netsh advfirewall firewall add rule name="Quest Spiek UDP Bridge" dir=in action=allow protocol=UDP localport=

    public static QuestDiscover? questDiscover;
    public static void Main(string[] args)
    {
        questDiscover = new QuestDiscover();

        questDiscover.OnMessage += (result) =>
        {
            Console.WriteLine(result.message);

            string[] args = result.message.Split("_");

            if (args[0] != "QUEST")
                return;

            if (args[1] != "DebugPort")
                return;

            QuestVRDevice questVRDevice = new QuestVRDevice(args[2]);
            questVRDevice.ForceAddress(result.ip, int.Parse(args[3]));

            Console.WriteLine($"A meta quest responded ({questVRDevice.Ip}:{questVRDevice.Port}) over network ping!");
            MainForm.GetInstance().AddDevice(questVRDevice);
        };

        questDiscover.Start();

        Application.SetHighDpiMode(HighDpiMode.PerMonitorV2);
        Application.SetDefaultFont(new Font("Segoe UI", 9f));
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);

        MainForm mainForm = new MainForm();
        Console.ReadLine();
    }
}
