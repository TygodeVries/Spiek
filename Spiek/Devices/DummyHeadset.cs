
using System.Diagnostics;

namespace Spiek.Devices
{
    public class DummyHeadset : Headset
    {
        private string key = "DUMMY";
        private bool displayEnabled = false;
        private Process? castingProcess;

        private const Keys ToggleKey = Keys.F9;

        private IMessageFilter? keyboardFilter;

        public override void Connect()
        {
            key = "DUMMY";
            displayEnabled = false;

            keyboardFilter = new DummyKeyboardFilter(
                ToggleDisplay,
                ToggleKey
            );

            Application.AddMessageFilter(keyboardFilter);

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

        public override bool IsCasting()
        {
            return castingProcess != null &&
                   !castingProcess.HasExited;
        }

        public override bool IsScreenEnabled()
        {
            return displayEnabled;
        }

        public override void PowerDown()
        {
            displayEnabled = false;
            StopVideoStream();
        }

        public override void StartVideoStream()
        {
            if (IsCasting())
                return;

            castingProcess = Process.Start(
                Application.ExecutablePath,
                "--dummy"
            );

            if (castingProcess == null)
                return;

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

        public override void StopVideoStream()
        {
            if (castingProcess == null)
                return;

            try
            {
                if (!castingProcess.HasExited)
                    castingProcess.Kill();
            }
            catch (InvalidOperationException)
            {
                // Process already exited.
            }

            castingProcess.Dispose();
            castingProcess = null;
        }

        public override string GetUniqueAddress()
        {
            return key;
        }

        public void ToggleDisplay()
        {
            displayEnabled = !displayEnabled;

            Console.WriteLine(
                $"[DummyHeadset] Display: {(displayEnabled ? "ON" : "OFF")}"
            );
        }
    }

    internal class DummyKeyboardFilter : IMessageFilter
    {
        private readonly Action onToggle;
        private readonly Keys toggleKey;
        private bool keyDown;

        public DummyKeyboardFilter(Action onToggle, Keys toggleKey)
        {
            this.onToggle = onToggle;
            this.toggleKey = toggleKey;
        }

        public bool PreFilterMessage(ref Message m)
        {
            const int WM_KEYDOWN = 0x0100;
            const int WM_KEYUP = 0x0101;
            const int WM_SYSKEYDOWN = 0x0104;
            const int WM_SYSKEYUP = 0x0105;

            if ((m.Msg == WM_KEYDOWN || m.Msg == WM_SYSKEYDOWN) &&
                (Keys)(int)m.WParam == toggleKey)
            {
                if (!keyDown)
                {
                    keyDown = true;
                    onToggle();
                }
            }
            else if ((m.Msg == WM_KEYUP || m.Msg == WM_SYSKEYUP) &&
                     (Keys)(int)m.WParam == toggleKey)
            {
                keyDown = false;
            }

            return false;
        }
    }
}
