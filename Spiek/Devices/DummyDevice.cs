using System.Diagnostics;

namespace Spiek.Devices
{
    public class DummyDevice : VRDevice
    {
        private string key;
        public override void Connect()
        {
            key = $"DUMMY";
            SetDeviceState(DeviceState.Connected);
        }

        public override int GetBatteryLevel()
        {
            return 69;
        }

        public override string GetDeviceSerial()
        {
            return "DUMMY";
        }


        private bool isCasting = false;
        public override bool IsCasting()
        {
            return castingProcess != null && !castingProcess.HasExited;
        }

        public override bool IsScreenEnabled()
        {
            return false;
        }

        public override void PowerDown()
        {

        }

        public override string[] GetApps()
        {
            throw new NotImplementedException();
        }

        private Process castingProcess;

        public override void StartCast()
        {
            isCasting = true;

            castingProcess = Process.Start(new ProcessStartInfo
            {
                FileName = "mspaint.exe",
                UseShellExecute = true
            });

            castingProcess.WaitForInputIdle();

            for (int i = 0; i < 20; i++)
            {
                castingProcess.Refresh();

                if (castingProcess.MainWindowHandle != IntPtr.Zero)
                    break;

                Thread.Sleep(100);
            }

            RunCastStarted(castingProcess);
        }



        public override void StopCast()
        {
            castingProcess.Kill();
        }

        public override string UniqueAddressId()
        {
            return key;
        }
    }
}
