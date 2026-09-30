using Spiek.Devices;
using System.Diagnostics;
using System.Runtime.InteropServices;
namespace Spiek.UI;

using Timer = System.Windows.Forms.Timer;

public class DeviceWindow : Form
{
    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr SetParent(IntPtr hWndChild, IntPtr hWndNewParent);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern int GetWindowLong(IntPtr hWnd, int nIndex);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool MoveWindow(
        IntPtr hWnd,
        int X,
        int Y,
        int nWidth,
        int nHeight,
        bool bRepaint
    );

    private const int GWL_STYLE = -16;
    private const int WS_CHILD = 0x40000000;
    private const int WS_VISIBLE = 0x10000000;
    private const int WS_POPUP = unchecked((int)0x80000000);
    private const int WS_CAPTION = 0x00C00000;
    private const int WS_THICKFRAME = 0x00040000;

    private Process? castingProcess = null;
    private Timer? updateTimer;

    // Status controls
    private readonly Label castingDisabledLabel;
    private readonly Label batteryPercentageLabel;

    public VRDevice Device { get; }

    public DeviceWindow(VRDevice device)
    {
        Device = device;
        TopLevel = false;
        BackColor = Color.Black;

        // Container for the status information
        var statusPanel = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = Color.Black
        };

        // Display disabled text
        castingDisabledLabel = new Label
        {
            Text = "Display is not enabled.",
            AutoSize = true,
            ForeColor = Color.White,
            BackColor = Color.Black,
            Font = new Font("Segoe UI", 20f, FontStyle.Regular)
        };

        // Battery percentage
        batteryPercentageLabel = new Label
        {
            Text = "Battery: ??%",
            AutoSize = true,
            ForeColor = Color.White,
            BackColor = Color.Black,
            Font = new Font("Segoe UI", 14f, FontStyle.Regular)
        };

        statusPanel.Controls.Add(castingDisabledLabel);
        statusPanel.Controls.Add(batteryPercentageLabel);

        Controls.Add(statusPanel);

        // Center the status text and battery percentage
        statusPanel.Resize += (s, e) =>
        {
            int centerX = statusPanel.ClientSize.Width / 2;
            int centerY = statusPanel.ClientSize.Height / 2;

            castingDisabledLabel.Location = new Point(
                centerX - (castingDisabledLabel.Width / 2),
                centerY - 30
            );

            batteryPercentageLabel.Location = new Point(
                centerX - (batteryPercentageLabel.Width / 2),
                centerY + 20
            );
        };

        // Setup events
        Device.OnCastStarted += OnCastStart;
        Resize += (s, e) => SyncChilds();
        LocationChanged += (s, e) => SyncChilds();

        UpdateCastingLabel();
        StartUpdateTimer();
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
        int style = GetWindowLong(Handle, GWL_STYLE);

        style |= WS_CHILD | WS_VISIBLE;
        style &= ~WS_POPUP;
        style &= ~WS_CAPTION;
        style &= ~WS_THICKFRAME;

        SetWindowLong(Handle, GWL_STYLE, style);
    }

    public void OnCastStart(Process prc)
    {
        Invoke(() =>
        {
            castingProcess = prc;

            IntPtr window = castingProcess.MainWindowHandle;

            // Setup window style (hide borders)
            int currentStyle = GetWindowLong(window, GWL_STYLE);
            int newStyle = (currentStyle | WS_CHILD | WS_VISIBLE)
                & ~WS_POPUP
                & ~WS_CAPTION
                & ~WS_THICKFRAME;

            SetWindowLong(window, GWL_STYLE, newStyle);

            // Make window parent of this form
            SetParent(window, Handle);

            UpdateCastingLabel();

            MainForm.GetInstance().RecalculateLayout();

            SyncChilds();
        });
    }

    private void UpdateCastingLabel()
    {
        bool casting = IsCasting();

        castingDisabledLabel.Visible = !casting;
        batteryPercentageLabel.Visible = !casting;
    }

    public void StartUpdateTimer()
    {
        MainForm.GetInstance().Invoke(() =>
        {
            updateTimer = new Timer
            {
                Interval = 10_000
            };

            updateTimer.Tick += Update;
            updateTimer.Start();

            Device.OnDisconnect += () =>
            {
                updateTimer.Stop();
            };
        });


    }

    private void Update(object? sender, EventArgs e)
    {
        Console.WriteLine("UPDATE!!!");
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

        int batteryPercentage = Device.GetBatteryLevel();
        batteryPercentageLabel.Text = $"{batteryPercentage}%";

        UpdateCastingLabel();
    }

    private void SyncChilds()
    {
        if (IsCasting())
        {
            castingDisabledLabel.Visible = false;
            batteryPercentageLabel.Visible = false;

            MoveWindow(
                castingProcess!.MainWindowHandle,
                0,
                0,
                ClientSize.Width,
                ClientSize.Height,
                true
            );
        }
        else
        {
            castingDisabledLabel.Visible = true;
            batteryPercentageLabel.Visible = true;
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
            // The Process object isn't associated with a running process.
            return false;
        }
    }

    public void Move(int x, int y, int width, int height)
    {
        Invoke(() =>
        {
            Location = new Point(x, y);
            Size = new Size(width, height);
            Refresh();
            SyncChilds();
        });
    }

    public new void Close()
    {
        if (IsCasting())
            castingProcess?.Close();

        base.Close();
    }

}