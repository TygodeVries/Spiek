using Spiek.Devices;
using Spiek.UI;

namespace Spiek.Utils
{
    public class Tools
    {
        public static void Populate(ToolStrip toolStrip)
        {
            AddOption(toolStrip, "Connect/Meta Quest/Connect via USB", () => MainForm.GetInstance().ManualUsbConnect());
            AddOption(toolStrip, "Connect/Meta Quest/Connect via IP", () => MainForm.GetInstance().ManualIpConnect());
            AddOption(toolStrip, "Connect/Meta Quest/Ping on Network", () => ManualBroadcast());
            AddOption(toolStrip, "Connect/Testing/Add Dummy Device", () => MainForm.GetInstance().AddDummyDevice());

            AddOption(toolStrip, "Actions/Power/Power-off All Devices", () => PoweroffAll());
            AddOption(toolStrip, "Actions/Power/Power-off Selected Device", () => PoweroffSelected());
            AddOption(toolStrip, "Actions/Patch/Install Spiek on USB Connected Device", () => PatchUsb.Patch());
            AddOption(toolStrip, "Actions/Settings/Change Selected Device Settings", () => ChangeSettings());

            AddOption(toolStrip, "Apps/SculptVR/Move Exports to Dropbox on Selected Device", () => _ = SculptVrMoveToDropbox());

            AddOption(toolStrip, "About/Version", () => MainForm.GetInstance().Popup("Sπk is on version: 'Beta 1'"));
            AddOption(toolStrip, "About/Credits", () => MainForm.GetInstance().Popup("Developer: Tygo\nIcons and Logo: DJ\nCreated for the Digial Art Factory"));
        }

        private static void ChangeSettings()
        {
            VRDevice? device = MainForm.GetInstance().GetSelectedDevice();

            if (device == null)
            {
                MainForm.GetInstance().ErrorNoSelected();
                return;
            }

            DeviceInfoSetting deviceInfoSetting = new DeviceInfoSetting(device.GetDeviceInfo());

            deviceInfoSetting.OnSave += () =>
            {
                Settings.Instance.Devices.RemoveAll((o) =>
                {
                    return o.DeviceId == device.GetDeviceSerial();
                });

                Settings.Instance.Devices.Add(deviceInfoSetting.GetInfo());
                Settings.Instance.Save();

                device.Disconnect();
            };

            deviceInfoSetting.Show();

        }

        private async static Task SculptVrMoveToDropbox()
        {
            VRDevice? device = MainForm.GetInstance().GetSelectedDevice();

            if (device == null)
            {
                MainForm.GetInstance().ErrorNoSelected();
                return;
            }

            if (device is not QuestVRDevice quest)
            {
                MainForm.GetInstance().ErrorAlert(
                    "This function is only available for the Meta Quest!");

                return;
            }

            var mainForm = MainForm.GetInstance();

            try
            {
                mainForm.ShowLoading("Moving SculptVR models to Dropbox...\nThis might take some time.");

                await AppUtils.MoveFileToDropbox(
                    quest,
                    "sdcard/Android/Data/com.sculptrvrinc.sculptrvrapp/files/OBJExports");

                await AppUtils.MoveFileToDropbox(
                    quest,
                    "sdcard/Android/Data/com.sculptrvrinc.sculptrvrapp/files/FBXExports");
                mainForm.HideLoading();
                mainForm.InfoAlert("Files have moved to Dropbox!");
            }
            catch (Exception ex)
            {
                mainForm.ErrorAlert(
                    $"Failed to move files to Dropbox:\n{ex.Message}");
            }

            mainForm.HideLoading();
        }


        private static void PoweroffAll()
        {
            if (MainForm.GetInstance().AlertConfirm("Are you sure you want to disable all devices?"))
            {
                foreach (VRDevice device in MainForm.GetInstance().GetDevices())
                {
                    device.PowerDown();
                }
            }
        }

        private static void PoweroffSelected()
        {
            VRDevice? device = MainForm.GetInstance().GetSelectedDevice();

            if (device == null)
            {
                MainForm.GetInstance().ErrorNoSelected();
                return;
            }

            device.PowerDown();
        }

        private static void ManualBroadcast()
        {
            Program.questDiscover.Broadcast("SPIEK_PING");
            MainForm.GetInstance().InfoAlert("A ping was send manually. It might take some time (~10 seconds) before devices are found.\nSpiek automatically sends out pings, so you do not have to send them manually unless something is wrong.");
        }

        private static void AddOption(ToolStrip toolStrip, string path, Action action)
        {
            var parts = path.Split('/');

            if (parts.Length == 0)
                throw new ArgumentException("Path cannot be empty.", nameof(path));

            var button = toolStrip.Items
                .OfType<ToolStripDropDownButton>()
                .FirstOrDefault(x => x.Text == parts[0]);

            if (button == null)
            {
                button = new ToolStripDropDownButton(parts[0]);
                toolStrip.Items.Add(button);
            }

            ToolStripDropDownItem parent = button;

            for (int i = 1; i < parts.Length - 1; i++)
            {
                var submenu = parent.DropDownItems
                    .OfType<ToolStripMenuItem>()
                    .FirstOrDefault(x => x.Text == parts[i]);

                if (submenu == null)
                {
                    submenu = new ToolStripMenuItem(parts[i]);
                    parent.DropDownItems.Add(submenu);
                }

                parent = submenu;
            }

            var menuItem = new ToolStripMenuItem(parts[^1]);
            menuItem.Click += (_, _) => action();

            parent.DropDownItems.Add(menuItem);
        }
    }
}
