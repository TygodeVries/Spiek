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
    private static extern bool MoveWindow(IntPtr hWnd, int X, int Y, int nWidth, int nHeight, bool bRepaint);
    private const int GWL_STYLE = -16;
    private const int WS_CHILD = 0x40000000;
    private const int WS_VISIBLE = 0x10000000;
    private const int WS_POPUP = unchecked((int)0x80000000);
    private const int WS_CAPTION = 0x00C00000;
    private const int WS_THICKFRAME = 0x00040000;

    private Process? castingProcess = null;

    public VRDevice Device { get; }

    public DeviceWindow(VRDevice device)
    {
        Device = device;
        TopLevel = false;

        // Setup events
        Device.OnCastStarted += OnCastStart;
        Resize += (s, e) => SyncChilds();
        LocationChanged += (s, e) => SyncChilds();

        BackColor = Color.DarkRed;

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
        castingProcess = prc;

        IntPtr window = castingProcess.MainWindowHandle;

        // Setup window style (hide borders)
        int currentStyle = GetWindowLong(window, GWL_STYLE);
        int newStyle = (currentStyle | WS_CHILD | WS_VISIBLE) & ~WS_POPUP & ~WS_CAPTION & ~WS_THICKFRAME;
        SetWindowLong(castingProcess.MainWindowHandle, GWL_STYLE, newStyle);

        // Make window parent of this form
        SetParent(castingProcess.MainWindowHandle, Handle);

        // 
        MainForm.GetInstance().RecalculateLayout();

        // Move the casting window to the correct position
        SyncChilds();
    }

    /*
     *  Handle updates
     */
    private Timer? updateTimer;
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
        });
    }
    private void Update(object? sender, EventArgs e)
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
    }

    private void SyncChilds()
    {
        // If the casting program exists
        if (IsCasting)
        {
            // We fill the whole form, then another app will resize this form.
            MoveWindow(
                castingProcess!.Handle,
                0,
                0,
                ClientSize.Width,
                ClientSize.Height,
                true
            );
        }
    }

    public bool IsCasting => castingProcess != null && !castingProcess.HasExited && castingProcess.MainWindowHandle != IntPtr.Zero;

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
        if (IsCasting)
            castingProcess?.Close();

        base.Close();
    }
}