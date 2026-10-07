using Spiek.Devices;
using Spiek.UI;

namespace Spiek.Utils
{
    public class AppUtils
    {
        public static async Task MoveFileToDropbox(
     QuestVRDevice quest,
     string devicePath)
        {
            if (Settings.Instance.DropboxPath == "none")
            {
                MainForm.GetInstance().ErrorAlert(
                    "Dropbox is not setup.\nPlease setup DropboxPath in settings.json");

                return;
            }

            var localTempDirectory = Path.Combine(
                Directory.GetCurrentDirectory(),
                "downloads",
                quest.UniqueAddressId());

            Directory.CreateDirectory(localTempDirectory);

            var folderName = Path.GetFileName(
                devicePath.TrimEnd('/', '\\'));

            var localPath = Path.Combine(
                localTempDirectory,
                folderName);

            if (Directory.Exists(localPath))
            {
                Directory.Delete(localPath, recursive: true);
            }

            if (File.Exists(localPath))
            {
                File.Delete(localPath);
            }

            string? msg = await quest.SendCommandAsync(
                $"pull \"{devicePath}\" \"{localPath}\"",
                dontDisconnectIfIncorrect: false);

            if (!quest.LastCommandSuccess())
            {
                MainForm.GetInstance().ErrorAlert(
                    $"Failed to download directory: {msg}");

                return;
            }

            if (!Directory.Exists(localPath))
            {
                MainForm.GetInstance().ErrorAlert(
                    $"ADB pull completed, but directory was not found:\n{localPath}");

                return;
            }

            var targetPath = Path.Combine(
                Settings.Instance.DropboxPath,
                quest.GetDeviceInfo().Name,
                folderName);

            CopyDirectoryRecursively(localPath, targetPath);
        }


        private static void CopyDirectoryRecursively(string sourcePath, string targetPath)
        {
            Directory.CreateDirectory(targetPath);

            foreach (var sourceDirectory in Directory.GetDirectories(sourcePath, "*", SearchOption.AllDirectories))
            {
                var relativePath = Path.GetRelativePath(sourcePath, sourceDirectory);

                var targetDirectory = Path.Combine(targetPath, relativePath);

                Directory.CreateDirectory(targetDirectory);
            }

            foreach (var sourceFile in Directory.GetFiles(sourcePath, "*", SearchOption.AllDirectories))
            {
                var relativePath = Path.GetRelativePath(
                    sourcePath,
                    sourceFile);

                var targetFile = Path.Combine(
                    targetPath,
                    relativePath);

                Directory.CreateDirectory(
                    Path.GetDirectoryName(targetFile)!);

                File.Copy(
                    sourceFile,
                    targetFile,
                    overwrite: true);
            }
        }
    }
}
