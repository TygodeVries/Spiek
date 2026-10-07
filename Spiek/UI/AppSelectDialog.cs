using Spiek.Devices;

namespace Spiek.UI
{
    internal class AppSelectDialog : Form
    {
        public AppSelectDialog()
        {
            Shown += (sender, args) =>
            {

            };
        }

        private List<VRDevice> devices = new List<VRDevice>();
        private List<App> apps = new List<App>();

        public static AppSelectDialog Create(VRDevice device)
        {
            return Create(new[] { device });
        }

        public static AppSelectDialog Create(VRDevice[] devices)
        {
            AppSelectDialog dialog = new AppSelectDialog();

            Dictionary<string, App> appMap = new Dictionary<string, App>();

            foreach (VRDevice device in devices)
            {
                foreach (string package in device.GetApps())
                {
                    if (!appMap.TryGetValue(package, out App? app))
                    {
                        app = new App(package);
                        appMap.Add(package, app);
                    }

                    app.DeviceCount++;
                }
            }

            dialog.apps.AddRange(appMap.Values);

            return dialog;
        }

        private class App
        {
            public string Name;
            public int DeviceCount;
            public string Package;

            public App(string package)
            {
                Package = package;
                Name = package.Split('.').Last();
            }
        }
    }
}
