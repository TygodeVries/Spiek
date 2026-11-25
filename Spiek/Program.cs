using Spiek.Devices;
using Spiek.UI;
using Spiek.UI.Update;
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
    public static MetaQuestDiscover? questDiscover;

    [STAThread]
    public static void Main(string[] args)
    {
        Application.SetHighDpiMode(HighDpiMode.PerMonitorV2);
        Application.SetDefaultFont(new Font("Segoe UI", 9f));
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);

        if (args.Length > 0 && args[0] == "--dummy")
        {
            Application.Run(new Form());

            return;
        }

        Theme.LoadFont();
        questDiscover = new MetaQuestDiscover();

        questDiscover.OnMessage += (result) =>
        {
            Console.WriteLine($"[Network] {result}");

            string[] args = result.message.Split("_");

            if (args.Length < 4 ||
                args[0] != "QUEST" ||
                args[1] != "DebugPort")
                return;

            MetaQuest questVRDevice = new MetaQuest(args[2]);
            questVRDevice.SetInternetAddress(result.ip, int.Parse(args[3]));

            Console.WriteLine(
                $"[Network] Discovered a Meta Quest device at {result.ip}");

            MainForm.GetInstance().AddDevice(questVRDevice);
        };

        questDiscover.Start();

        try
        {
            Application.Run(new MainForm().GetForm());
        }
        catch
        {

        }
    }
}
