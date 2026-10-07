using System.ComponentModel;
using System.Diagnostics;

namespace Spiek.Utils;

public class Command
{
    public static (string, bool) Run(string cmd, string args)
    {
        Console.WriteLine($"-> {cmd} {args}");

        Process process = new Process();
        process.StartInfo.FileName = Solve(cmd);
        process.StartInfo.Arguments = args;

        process.StartInfo.UseShellExecute = false;
        process.StartInfo.CreateNoWindow = true;

        // Let us read the output
        process.StartInfo.RedirectStandardOutput = true;
        process.StartInfo.RedirectStandardError = true;

        try
        {
            process.Start();
        }
        catch (System.ComponentModel.Win32Exception ex)
        {
            Console.WriteLine($"Could not start '{cmd}': {ex.Message}");
            return ($"Could not start '{cmd}': {ex.Message}", false);
        }

        string output = process.StandardOutput.ReadToEnd();
        string error = process.StandardError.ReadToEnd();

        process.WaitForExit();

        if (process.ExitCode != 0)
        {
            Console.WriteLine($"Command '{cmd}' with arguments '{args}' failed (exit code {process.ExitCode}):\n{error}");
            return (error, false);
        }

        return (output, true);
    }

    public static async Task<(string Output, bool Success)> RunAsync(string cmd, string args)
    {
        Console.WriteLine($"-> {cmd} {args}");

        using var process = new Process();

        process.StartInfo.FileName = Solve(cmd);
        process.StartInfo.Arguments = args;

        process.StartInfo.UseShellExecute = false;
        process.StartInfo.CreateNoWindow = true;

        process.StartInfo.RedirectStandardOutput = true;
        process.StartInfo.RedirectStandardError = true;

        try
        {
            process.Start();
        }
        catch (System.ComponentModel.Win32Exception ex)
        {
            Console.WriteLine(
                $"Could not start '{cmd}': {ex.Message}");

            return (
                $"Could not start '{cmd}': {ex.Message}",
                false);
        }

        Task<string> outputTask =
            process.StandardOutput.ReadToEndAsync();

        Task<string> errorTask =
            process.StandardError.ReadToEndAsync();

        await process.WaitForExitAsync();

        string output = await outputTask;
        string error = await errorTask;

        if (process.ExitCode != 0)
        {
            Console.WriteLine(
                $"Command '{cmd}' with arguments '{args}' " +
                $"failed (exit code {process.ExitCode}):\n{error}");

            return (error, false);
        }

        return (output, true);
    }


    public static (Process?, string) RunGetPrc(string cmd, string args)
    {
        Process process = new Process();
        process.StartInfo.FileName = Solve(cmd);
        process.StartInfo.Arguments = args;

        process.StartInfo.UseShellExecute = false;
        process.StartInfo.CreateNoWindow = false;

        try
        {
            process.Start();
        }
        catch (Exception e)
        {
            Console.WriteLine($"Failed to RunGetPrc {e}");
            return (null, e.Message);
        }

        return (process, "yay");
    }


    private static string Solve(string command)
    {
        if (Path.IsPathFullyQualified(command))
            return command;

        string executable = command.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)
            ? command
            : command + ".exe";

        string localPath = Path.Combine(
            AppContext.BaseDirectory,
            executable
        );

        if (File.Exists(localPath))
            return localPath;

        string programsPath = Path.Combine(
            AppContext.BaseDirectory,
            "programs",
            executable
        );

        if (File.Exists(programsPath))
            return programsPath;

        return command;
    }


    public static bool Exists(string command, string args)
    {
        try
        {
            using var process = new Process();

            process.StartInfo.FileName = Solve(command);
            process.StartInfo.Arguments = args;
            process.StartInfo.UseShellExecute = false;
            process.StartInfo.CreateNoWindow = true;
            process.StartInfo.RedirectStandardOutput = true;
            process.StartInfo.RedirectStandardError = true;

            process.Start();
            process.WaitForExit();

            return process.ExitCode == 0;
        }
        catch (Win32Exception)
        {
            return false;
        }
    }

}
