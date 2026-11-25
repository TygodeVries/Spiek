
using Spiek.Devices;
using System.Diagnostics;

namespace Spiek.UI;

using Timer = System.Windows.Forms.Timer;

public class DeviceWindow : Control
{
    private Process? castingProcess = null;
    private Timer? updateTimer;

    public Headset Device { get; }

    public RoundedLabel BatteryLabel;

    public DeviceWindow(Headset device)
    {
        Device = device;

        Visible = false;

        Device.OnCastStarted += OnCastStart;

        Resize += (s, e) => SyncChilds();
        LocationChanged += (s, e) => SyncChilds();

        ControlAdded += (sender, arg) =>
        {
            if (BatteryLabel == null)
            {
                BatteryLabel = new RoundedLabel
                {
                    Text = "??%",
                    ChipColor = ColorTranslator.FromHtml(
                        device.GetDeviceInfo().HexColor),
                    ForeColor = Color.White,
                    Font = new Font("Segoe UI", 30f),
                    AutoSize = true,
                    Visible = false
                };

                MainForm.GetInstance().Overlay.AddChip(
                    BatteryLabel, this);
            }
        };

        ControlRemoved += (sender, args) =>
        {
            if (BatteryLabel != null)
                MainForm.GetInstance().Overlay.RemoveChip(BatteryLabel);
        };

        UpdateUIElements();
        StartUpdateTimer();

        Tick(null, EventArgs.Empty);
        Tick(null, EventArgs.Empty);
    }

    public Color GetColor(string name)
    {
        Color color = Color.FromName(name);

        if (!color.IsKnownColor)
            return Color.Black;

        return color;
    }

    public void ApplyVisualFormat()
    {
        int style = WindowsApi.GetWindowLong(
            Handle, WindowsApi.GWL_STYLE);

        style |= WindowsApi.WS_CHILD | WindowsApi.WS_VISIBLE;
        style &= ~WindowsApi.WS_POPUP;
        style &= ~WindowsApi.WS_CAPTION;
        style &= ~WindowsApi.WS_THICKFRAME;

        WindowsApi.SetWindowLong(
            Handle, WindowsApi.GWL_STYLE, style);
    }

    public void OnCastStart(Process prc)
    {
        WaitForMainWindow(prc);

        Invoke(() =>
        {
            prc.Exited += (sender, args) =>
            {
                if (!IsDisposed && IsHandleCreated)
                {
                    BeginInvoke(() =>
                    {
                        if (castingProcess == prc)
                        {
                            UpdateUIElements();
                            MainForm.GetInstance().RecalculateLayout();
                        }
                    });
                }
            };

            castingProcess = prc;

            IntPtr window = prc.MainWindowHandle;

            int currentStyle = WindowsApi.GetWindowLong(
                window, WindowsApi.GWL_STYLE);

            int newStyle = (currentStyle | WindowsApi.WS_CHILD |
                WindowsApi.WS_VISIBLE)
                & ~WindowsApi.WS_POPUP
                & ~WindowsApi.WS_CAPTION
                & ~WindowsApi.WS_THICKFRAME;

            WindowsApi.SetWindowLong(
                window, WindowsApi.GWL_STYLE, newStyle);

            WindowsApi.SetParent(window, Handle);

            UpdateUIElements();

            MainForm.GetInstance().RecalculateLayout();

            SyncChilds();
        });
    }

    private void WaitForMainWindow(Process process)
    {
        for (int i = 0; i < 100; i++)
        {
            if (process.HasExited)
                throw new InvalidOperationException(
                    "Casting process exited.");

            process.Refresh();

            if (process.MainWindowHandle != IntPtr.Zero)
                return;

            Thread.Sleep(100);
        }

        throw new TimeoutException(
            "Timed out waiting for casting process main window.");
    }

    public void StartUpdateTimer()
    {
        MainForm.GetInstance().Invoke(() =>
        {
            updateTimer = new Timer
            {
                Interval = 10_000
            };

            updateTimer.Tick += Tick;
            updateTimer.Start();

            Device.OnDisconnect += () =>
            {
                updateTimer.Stop();

                if (!IsDisposed)
                {
                    MainForm.GetInstance().Invoke(() =>
                    {
                        castingProcess = null;
                        UpdateUIElements();
                        MainForm.GetInstance().RecalculateLayout();
                    });
                }
            };
        });
    }

    private void Tick(object? sender, EventArgs e)
    {
        if (Device.IsScreenEnabled())
        {
            if (!Device.IsCasting())
                Device.StartVideoStream();
        }
        else
        {
            if (Device.IsCasting())
                Device.StopVideoStream();
        }

        UpdateUIElements();
    }

    private void UpdateUIElements()
    {
        bool casting = IsCasting();

        Visible = casting;

        if (BatteryLabel != null)
        {
            BatteryLabel.Text = $"{Device.GetBatteryLevel()}%";
            BatteryLabel.Visible = casting;
        }
    }

    private void SyncChilds()
    {
        if (IsCasting())
        {
            IntPtr castHandle = castingProcess!.MainWindowHandle;

            WindowsApi.SetWindowPos(
                castHandle,
                WindowsApi.HWND_BOTTOM,
                0, 0,
                ClientSize.Width,
                ClientSize.Height,
                WindowsApi.SWP_NOACTIVATE |
                WindowsApi.SWP_SHOWWINDOW
            );
        }
    }

    public bool IsCasting()
    {
        if (castingProcess == null)
            return false;

        try
        {
            if (castingProcess.HasExited)
                return false;

            return castingProcess.MainWindowHandle != IntPtr.Zero;
        }
        catch (InvalidOperationException)
        {
            return false;
        }
    }

    public void Move(int x, int y, int width, int height)
    {
        Invoke(() =>
        {
            if (BatteryLabel != null)
            {
                BatteryLabel.Location = new Point(
                    x + 50, y + height - 100);
            }

            Location = new Point(x, y);
            Size = new Size(width, height);

            Refresh();
            SyncChilds();
        });
    }

    public new void Close()
    {
        updateTimer?.Stop();
        updateTimer?.Dispose();

        if (IsCasting())
            castingProcess?.Close();

        Visible = false;
    }
}
