using Spiek.Devices;
using System.Diagnostics;
namespace Spiek.UI;

using Timer = System.Windows.Forms.Timer;

public class DeviceWindow : Form
{
    private Process? castingProcess = null;
    private Timer? updateTimer;


    public VRDevice Device { get; }

    public DeviceWindow(VRDevice device)
    {
        Device = device;
        TopLevel = false;
        BackColor = Color.Black;

        Device.OnCastStarted += OnCastStart;
        Resize += (s, e) => SyncChilds();
        LocationChanged += (s, e) => SyncChilds();

        Shown += (sender, args) =>
        {
            if (BatteryLabel == null)
            {
                BatteryLabel = new RoundedLabel();
                BatteryLabel.Text = "??%";
                BatteryLabel.ChipColor = ColorTranslator.FromHtml(device.GetDeviceInfo().HexColor);
                BatteryLabel.ForeColor = Color.White;
                BatteryLabel.Font = new Font("Segoe UI", 30f);
                BatteryLabel.AutoSize = true;

                MainForm.GetInstance().Overlay.AddChip(BatteryLabel, this);
            }

            UpdateUIElements();
        };

        FormClosed += (sender, args) =>
        {
            MainForm.GetInstance().Overlay.RemoveChip(BatteryLabel);
        };

        UpdateUIElements();
        StartUpdateTimer();

        Tick(null, EventArgs.Empty);
        Tick(null, EventArgs.Empty);

        ResizeEnd += (sender, args) =>
        {
            UpdateUIElements();
        };
    }

    public Color GetColor(string name)
    {
        Color color = Color.FromName(name);

        if (!color.IsKnownColor)
        {
            return Color.Black;
        }

        return color;
    }

    public void ApplyVisualFormat()
    {
        int style = WindowsApi.GetWindowLong(Handle, WindowsApi.GWL_STYLE);

        style |= WindowsApi.WS_CHILD | WindowsApi.WS_VISIBLE;
        style &= ~WindowsApi.WS_POPUP;
        style &= ~WindowsApi.WS_CAPTION;
        style &= ~WindowsApi.WS_THICKFRAME;

        WindowsApi.SetWindowLong(Handle, WindowsApi.GWL_STYLE, style);
    }

    public void OnCastStart(Process prc)
    {
        WaitForMainWindow(prc);

        Invoke(() =>
        {

            while (Handle == IntPtr.Zero)
            {

            }

            prc.Exited += (sender, args) =>
            {
                UpdateUIElements();
            };

            Invoke(() =>
            {
                castingProcess = prc;

                IntPtr window = castingProcess.MainWindowHandle;

                // Setup window style (hide borders)
                int currentStyle = WindowsApi.GetWindowLong(window, WindowsApi.GWL_STYLE);
                int newStyle = (currentStyle | WindowsApi.WS_CHILD | WindowsApi.WS_VISIBLE)
                    & ~WindowsApi.WS_POPUP
                    & ~WindowsApi.WS_CAPTION
                    & ~WindowsApi.WS_THICKFRAME;

                WindowsApi.SetWindowLong(window, WindowsApi.GWL_STYLE, newStyle);

                // Make window parent of this form
                WindowsApi.SetParent(window, Handle);

                UpdateUIElements();

                MainForm.GetInstance().RecalculateLayout();

                SyncChilds();
            });
        });
    }

    private void WaitForMainWindow(Process process)
    {

        for (int i = 0; i < 100; i++)
        {
            if (process.HasExited)
                throw new InvalidOperationException("Casting process exited.");

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
            };
        });


    }

    private void Tick(object? sender, EventArgs e)
    {
        if (Device.IsScreenEnabled())
        {
            if (!Device.IsCasting())
                Device.StartCast();
        }
        else
        {
            if (Device.IsCasting())
                Device.StopCast();
        }

        UpdateUIElements();
    }


    private Label infoLabel;
    private void UpdateUIElements()
    {

        int battery = Device.GetBatteryLevel();
        if (BatteryLabel != null)
            BatteryLabel.Text = $"{battery}%";

        if (infoLabel == null)
        {
            infoLabel = new Label();
            infoLabel.Font = new Font("Segoe UI", 30f);
            infoLabel.ForeColor = Color.White;
            Controls.Add(infoLabel);
        }
        bool displayEnabled = Device.IsScreenEnabled();
        bool casting = Device.IsCasting();

        if (!displayEnabled || !casting)
        {
            infoLabel.Show();
            infoLabel.Text = "Display not enabled";
            infoLabel.AutoSize = true;

            infoLabel.Location = new Point(
                (ClientSize.Width - infoLabel.Width) / 2,
                (ClientSize.Height - infoLabel.Height) / 2
            );

            infoLabel.BringToFront();
            return;
        }

        infoLabel.Hide();
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
                ClientSize.Width, ClientSize.Height,
                WindowsApi.SWP_NOACTIVATE | WindowsApi.SWP_SHOWWINDOW
            );
        }
    }

    public bool IsCasting()
    {
        bool processExists = castingProcess != null;
        if (!processExists)
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

    public RoundedLabel BatteryLabel;
    public void Move(int x, int y, int width, int height)
    {
        Invoke(() =>
        {
            BatteryLabel.Location = new Point(x + 50, y + height - 100);
            Location = new Point(x, y);
            Size = new Size(width, height);
            Refresh();
            SyncChilds();

            infoLabel.Location = new Point(
                (ClientSize.Width - infoLabel.Width) / 2,
                (ClientSize.Height - infoLabel.Height) / 2
            );
        });
    }

    public new void Close()
    {
        if (IsCasting())
            castingProcess?.Close();

        base.Close();
    }

}