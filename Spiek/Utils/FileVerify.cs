namespace Spiek.Utils;

public class FileVerify
{
    public static void Verify()
    {
        Console.WriteLine("- File Installation Checker -");
        if (Command.Exists("adb", "version"))
        {
            Console.WriteLine("Adb has been found!");
        }
        else
        {
            Console.WriteLine("Adb could not be found, are you sure your programs folder is setup correctly?");
            Console.ReadLine();
        }

        if (Command.Exists("scrcpy", "--version"))
        {
            Console.WriteLine("Scrcpy has been found!");
        }
        else
        {
            Console.WriteLine("Scrcpy.exe could not be found, are you sure your programs folder is setup correctly?");
            if (File.Exists("scrcpy.exe"))
            {
                Console.WriteLine("Its found");
            }
            Console.ReadLine();
        }

        Console.Clear();
    }
}
