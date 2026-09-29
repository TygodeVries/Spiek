using Spiek.Devices;
using System.Diagnostics;
using System.Runtime.InteropServices;
namespace Spiek.UI;

using Timer = System.Windows.Forms.Timer;

public class DeviceWindow
{
    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr SetParent(IntPtr hWndChild, IntPtr hWndNewParent);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern int GetWindowLong(IntPtr hWnd, int nIndex);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool MoveWindow(IntPtr hWnd, int X, int Y, int nWidth, int nHeight, bool bRepaint);

    public Overlay? Overlay;
    private const int GWL_STYLE = -16;
    private const int WS_CHILD = 0x40000000;
    private const int WS_VISIBLE = 0x10000000;
    private const int WS_POPUP = unchecked((int)0x80000000);
    private const int WS_CAPTION = 0x00C00000;
    private const int WS_THICKFRAME = 0x00040000;

    private readonly Form form;
    private IntPtr childHandle = IntPtr.Zero;

    public VRDevice Device { get; }

    public DeviceWindow(VRDevice device)
    {
        Device = device;
        Device.OnCastStarted += OnCastStart;

        form = new Form
        {
            TopLevel = false,
            FormBorderStyle = FormBorderStyle.None,
            BackColor = Color.Black
        };

        int currentParentStyle = GetWindowLong(form.Handle, GWL_STYLE);
        SetWindowLong(form.Handle, GWL_STYLE, currentParentStyle);

        form.Show();
        form.Resize += (s, e) => SyncChilds();
        form.LocationChanged += (s, e) => SyncChilds();

        form.Resize += (_, _) =>
        {
            if (form.WindowState == FormWindowState.Minimized)
            {
                Overlay?.Hide();
            }
            else
            {
                Overlay?.Show();
            }
        };
    }

    public Form GetForm() => form;

    public Color RandomColor()
    {
        Color[] colors = new Color[]
        {
            Color.Red,
            Color.Green,
            Color.Blue,
            Color.Yellow,
            Color.Pink
        };

        return colors[Random.Shared.Next(0, colors.Length)];
    }

    private Process? castingPrc;
    public void OnCastStart(Process prc)
    {
        childHandle = prc.MainWindowHandle; ;
        castingPrc = prc;

        int currentStyle = GetWindowLong(childHandle, GWL_STYLE);
        int newStyle = (currentStyle | WS_CHILD | WS_VISIBLE) & ~WS_POPUP & ~WS_CAPTION & ~WS_THICKFRAME;


        SetWindowLong(childHandle, GWL_STYLE, newStyle);
        SetParent(childHandle, form.Handle);

        const bool ENABLE_OVERLAY = true;

        if (ENABLE_OVERLAY)
        {
            Overlay = new Overlay();

            Overlay.Show();
            Overlay.BringToFront();
        }

        SyncChilds();


        MainForm.GetInstance().RecalculateLayout();

        StartUpdate();

        form.Invoke(() =>
        {
            form.BackColor = RandomColor();
        });
    }

    private Timer? updateTimer;
    public void StartUpdate()
    {
        updateTimer = new Timer();
        updateTimer.Tick += new EventHandler(Update);
        updateTimer.Interval = 5000;
        updateTimer.Start();
    }

    private void Update(object? sender, EventArgs e)
    {
        Console.WriteLine(Device.GetBatteryLevel());

        if (castingPrc == null || castingPrc.HasExited)
        {
            Device.StartCast();
        }

        if (Device.IsScreenEnabled())
        {
            SetAwake();
        }
        else
        {
            SetSleeping();
        }
    }

    private PictureBox? statusImage;

    public void SetSleeping()
    {
        castingPrc?.Kill();
        Overlay?.Close();
        if (statusImage != null)
            return;

        Console.WriteLine("Enabeling Sleep mode...");

        if (!form.IsDisposed)
            form.Invoke(() =>
            {
                statusImage = new PictureBox()
                {
                    Image = new Bitmap("images/sleep.gif"),
                    Dock = DockStyle.Fill,
                    SizeMode = PictureBoxSizeMode.Zoom
                };

                statusImage.BringToFront();

                if (!form.IsDisposed)
                    form.Controls.Add(statusImage);
            });
    }

    public void SetAwake()
    {
        if (statusImage == null)
            return;

        Console.WriteLine("Waking up...");

        form.Invoke(() =>
        {
            form.Controls.Remove(statusImage);
            statusImage = null;

            SyncChilds();
        });

        Device.StartCast();
    }

    private void SyncChilds()
    {
        if (childHandle != IntPtr.Zero)
        {
            MoveWindow(
                childHandle,
                0,
                0,
                form.ClientSize.Width,
                form.ClientSize.Height,
                true
            );
        }

        if (Overlay != null)
        {
            const int Offset = 30;


            Size size = form.ClientSize;
            size.Height -= Offset;

            Point point = form.PointToScreen(Point.Empty);
            point.Y += Offset;

            Overlay.Size = size;
            Overlay.Location = point;
            Overlay.Refresh();
        }
    }


    public void Move(int x, int y, int width, int height)
    {
        form.Invoke(() =>
        {
            form.Location = new Point(x, y);
            form.Size = new Size(width, height);
            form.Refresh();
            SyncChilds();
        });
    }

    public void Close()
    {
        if (!form.IsDisposed)
            form.Invoke(() =>
            {
                form.Close();
                Overlay?.Close();
            });



        if (castingPrc != null)
        {
            castingPrc.Close();
            castingPrc = null;
        }
    }
}