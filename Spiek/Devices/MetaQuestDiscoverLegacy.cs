using System.Management;

namespace Spiek.Devices
{
    public class MetaQuestDiscoverLegacy
    {
        private readonly ManagementEventWatcher _arrivalWatcher;
        private readonly ManagementEventWatcher _removalWatcher;

        public event Action? QuestConnected;
        public event Action? QuestDisconnected;

        public MetaQuestDiscoverLegacy()
        {
            // Query specifically for Meta / Oculus Vendor ID (VID_2833)
            var arrivalQuery = new WqlEventQuery(
                "SELECT * FROM __InstanceCreationEvent WITHIN 1 " +
                "WHERE TargetInstance ISA 'Win32_PnPEntity' AND TargetInstance.DeviceID LIKE '%VID_2833%'");

            var removalQuery = new WqlEventQuery(
                "SELECT * FROM __InstanceDeletionEvent WITHIN 1 " +
                "WHERE TargetInstance ISA 'Win32_PnPEntity' AND TargetInstance.DeviceID LIKE '%VID_2833%'");

            _arrivalWatcher = new ManagementEventWatcher(arrivalQuery);
            _arrivalWatcher.EventArrived += (_, _) => QuestConnected?.Invoke();

            _removalWatcher = new ManagementEventWatcher(removalQuery);
            _removalWatcher.EventArrived += (_, _) => QuestDisconnected?.Invoke();
        }

        public void Start()
        {
            _arrivalWatcher.Start();
            _removalWatcher.Start();
        }

        public static bool IsAlreadyConnected()
        {
            using var searcher = new ManagementObjectSearcher(
                "SELECT DeviceID FROM Win32_PnPEntity WHERE DeviceID LIKE '%VID_2833%'");
            return searcher.Get().Cast<ManagementBaseObject>().Any();
        }

        public void Dispose()
        {
            _arrivalWatcher.Stop();
            _arrivalWatcher.Dispose();
            _removalWatcher.Stop();
            _removalWatcher.Dispose();
        }
    }
}
