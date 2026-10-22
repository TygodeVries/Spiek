using Spiek.Devices;
using Spiek.Utils;
using System.Numerics;
namespace Spiek.UI
{
    public class MainForm
    {
        private static MainForm? instance;
        public static MainForm GetInstance()
        {
            return instance!;
        }

        public List<DeviceWindow> DeviceWindows { get; private set; } = new List<DeviceWindow>();
        public OverlayWindow Overlay { get; private set; }
        private List<VRDevice> devices = new List<VRDevice>();
        private Form form;

        public MainForm()
        {
            instance = this;

            form = new Form()
            {
                WindowState = FormWindowState.Maximized,
                Text = "Spiek",
                Icon = new Icon("App.ico"),
                BackColor = Color.Black
            };

            form.Focus();

            form.Resize += (_, _) => RecalculateLayout();
            form.Move += (_, _) => RecalculateLayout();
            form.Shown += (_, _) =>
            {
                Setup();
                ShowNoConnections();
                CreateOverlay();
                form.Invoke(() =>
                {
                    StartTick();
                });

                if (!Program.FirewallRuleExists("Quest Spiek UDP Bridge"))
                {
                    ErrorAlert("The firewall rules are not set correctly. \nAutomatic connect might not work correctly.");
                }
            };


            Application.Run(form);
        }

        public void Invoke(Action action)
        {
            form.Invoke(action);
        }

        public void CreateOverlay()
        {
            OverlayWindow overlayWindow = new OverlayWindow();
            Overlay = overlayWindow;

            IntPtr h = overlayWindow.Handle;

            int currentStyle = WindowsApi.GetWindowLong(h, WindowsApi.GWL_STYLE);
            int newStyle = (currentStyle | WindowsApi.WS_CHILD | WindowsApi.WS_VISIBLE)
                & ~WindowsApi.WS_POPUP
                & ~WindowsApi.WS_CAPTION
                & ~WindowsApi.WS_THICKFRAME;

            WindowsApi.SetWindowLong(h, WindowsApi.GWL_STYLE, newStyle);

            overlayWindow.Show();
        }

        private static System.Windows.Forms.Timer? updateTimer;
        public static void StartTick()
        {
            updateTimer = new System.Windows.Forms.Timer();
            updateTimer.Tick += new EventHandler(Tick);
            updateTimer.Interval = 5000;
            updateTimer.Start();

            Tick(null, EventArgs.Empty);
        }

        private static void Tick(object? sender, EventArgs e)
        {
            Program.questDiscover?.Broadcast("SPIEK_PING");
        }

        public VRDevice[] GetDevices()
        {
            return devices.ToArray();
        }

        public VRDevice? GetSelectedDevice()
        {
            if (SelectedWindow == null && devices.Count == 1)
                return devices.First();

            return SelectedWindow?.Device;
        }

        public void Setup()
        {
            var toolStrip = new ToolStrip
            {
                ShowItemToolTips = false,
                CanOverflow = false,
                GripStyle = ToolStripGripStyle.Hidden
            };

            toolStrip.OverflowButton.Visible = false;
            Tools.Populate(toolStrip);

            form.Invoke(() =>
            {
                form.Controls.Add(toolStrip);
            });
        }

        public void AddDummyDevice()
        {
            AddDevice(new DummyDevice());
        }
        public void ManualUsbConnect()
        {
            List<string>? devices = QuestVRDevice.GetUsbDevices();
            if (devices == null)
            {
                ErrorAlert("Could not get list of devices. adb might not have been found.");
                return;
            }

            if (devices.Count == 0)
            {
                InfoAlert("No devices are attached, or they could not be reached.\nTry plugging the device out and in.");
                return;
            }

            if (devices.Count == 1)
            {
                AddDevice(new QuestVRDevice(devices.First()));
                return;
            }

            // #TODO show pick dialog
            string? device = PickDialog(devices.ToArray());
            if (device == null)
                return;

            AddDevice(new QuestVRDevice(device));
        }

        private Panel? noConnectionsBox = null;

        public void ShowNoConnections()
        {
            // Already showing
            if (noConnectionsBox != null)
                return;

            form.Invoke(() =>
            {
                var panel = new Panel
                {
                    Dock = DockStyle.Fill,
                    BackColor = form.BackColor,
                    Padding = new Padding(100)
                };

                var logo = new PictureBox
                {
                    Image = new Bitmap("images/Logo-big.png"),
                    Dock = DockStyle.Top,
                    Height = 500,
                    SizeMode = PictureBoxSizeMode.Zoom
                };

                var text = new Label
                {
                    Text = "Scanning for devices...",
                    Dock = DockStyle.Bottom,
                    Height = 100,
                    TextAlign = ContentAlignment.MiddleCenter,
                    Font = new Font("Segoe UI", 32, FontStyle.Regular),
                    ForeColor = Color.White
                };

                panel.Controls.Add(text);
                panel.Controls.Add(logo);

                form.Controls.Add(panel);

                noConnectionsBox = panel;
            });
        }

        public void HideNoConnections()
        {
            form.Invoke(() =>
            {
                // Already hidden
                if (noConnectionsBox == null)
                    return;

                form.Controls.Remove(noConnectionsBox);
                noConnectionsBox.Dispose();
                noConnectionsBox = null;
            });
        }


        public void AddDevice(VRDevice vrDevice)
        {
            foreach (VRDevice other in devices)
            {
                if (other == null)
                    continue;

                if (other.UniqueAddressId() == vrDevice.UniqueAddressId())
                {
                    Console.WriteLine("[WARN] Device already connected");
                    return;
                }
            }

            // Device connection can happen outside UI thread
            if (vrDevice.State != DeviceState.Connected)
                vrDevice.Connect();

            form.Invoke(() =>
            {
                AddDeviceWindow(vrDevice);
            });
        }

        public Form GetForm()
        {
            return form;
        }

        private void AddDeviceWindow(VRDevice vrDevice)
        {
            devices.Add(vrDevice);

            DeviceWindow deviceWindow = new DeviceWindow(vrDevice);
            DeviceWindows.Add(deviceWindow);

            form.Controls.Add(deviceWindow);
            deviceWindow.Show();

            HideNoConnections();
            RecalculateLayout();
            deviceWindow.ApplyVisualFormat();

            vrDevice.OnDisconnect += () =>
            {
                form.Invoke(() =>
                {
                    DeviceWindows.Remove(deviceWindow);
                    devices.Remove(vrDevice);

                    deviceWindow.Close();

                    if (DeviceWindows.Count == 0)
                        ShowNoConnections();
                });
            };

            if (DeviceWindows.Count != devices.Count)
            {
                // Double cast detected
                Console.WriteLine("Detected Double cast!");
                ResetAndRecover();
            }
        }

        public void ResetAndRecover()
        {
            foreach (VRDevice device in devices)
            {
                device.Disconnect();
            }

            devices.Clear();

            Thread.Sleep(100);

            foreach (DeviceWindow window in DeviceWindows)
            {
                window.Close();
            }

            DeviceWindows.Clear();
        }

        public DeviceWindow? SelectedWindow { get; private set; }

        public void SetSelectedWindow(DeviceWindow? window)
        {
            if (SelectedWindow == window)
                SelectedWindow = null;
            else
                SelectedWindow = window;

            RecalculateLayout();
        }

        public DeviceWindow? GetDeviceWindowAt(Vector2 mouse)
        {
            for (int i = 0; i < DeviceWindows.Count; i++)
            {
                Vector4 rect = GetWindowRect(i);

                if (mouse.X >= rect.X &&
                    mouse.X < rect.X + rect.Z &&
                    mouse.Y >= rect.Y &&
                    mouse.Y < rect.Y + rect.W)
                {
                    return DeviceWindows[i];
                }
            }

            return null;
        }

        public void RecalculateLayout()
        {
            if (Overlay != null)
            {
                Overlay.Size = form.Size;
                Overlay.Location = form.Location;
            }

            form.Invoke(() =>
            {
                form.BackColor = Color.Black;
                int i = 0;
                foreach (DeviceWindow window in DeviceWindows)
                {
                    Vector4 pos = GetWindowRect(i);
                    window.Move((int)pos.X, (int)pos.Y, (int)pos.Z, (int)pos.W);
                    i++;
                }

                form.Refresh();
            });
        }

        public Vector4 GetWindowRect(int index)
        {
            int count = DeviceWindows.Count;

            if (count == 0)
                return Vector4.Zero;

            if (SelectedWindow == null)
            {
                int cols = (int)Math.Ceiling(Math.Sqrt(count));
                int rows = (int)Math.Ceiling((double)count / cols);

                float cellWidth = (float)form.ClientSize.Width / cols;
                float cellHeight = (float)form.ClientSize.Height / rows;

                int row = index / cols;
                int col = index % cols;

                return new Vector4(
                    col * cellWidth,
                    row * cellHeight,
                    cellWidth,
                    cellHeight
                );
            }

            DeviceWindow selected = SelectedWindow;

            float selectedWidth = form.ClientSize.Width * 0.8f;
            float sideWidth = form.ClientSize.Width - selectedWidth;

            if (DeviceWindows[index] == selected)
            {
                return new Vector4(
                    0,
                    0,
                    selectedWidth,
                    form.ClientSize.Height
                );
            }

            var others = DeviceWindows
                .Where(x => x != selected)
                .ToList();

            int otherIndex = others.IndexOf(DeviceWindows[index]);

            int otherCount = others.Count;

            if (otherCount == 0)
                return Vector4.Zero;

            float height = (float)form.ClientSize.Height / otherCount;

            return new Vector4(
                selectedWidth,
                otherIndex * height,
                sideWidth,
                height
            );
        }

        public void ManualIpConnect()
        {
            string ip = AlertTextInput("Please enter the IP of the Meta Quest");
            QuestVRDevice device = new QuestVRDevice(ip, 5555);
            AddDevice(device);
        }

        public bool AlertConfirm(string text)
        {
            bool result = false;

            form.Invoke(() =>
            {
                Form confirmForm = new Form
                {
                    Text = "Sπk",
                    Size = new Size(350, 160),
                    StartPosition = FormStartPosition.CenterParent,
                    FormBorderStyle = FormBorderStyle.FixedDialog,
                    MaximizeBox = false,
                    MinimizeBox = false
                };

                Label label = new Label
                {
                    Text = text,
                    Dock = DockStyle.Top,
                    Height = 60,
                    TextAlign = ContentAlignment.MiddleCenter
                };

                Button yesButton = new Button
                {
                    Text = "Yes",
                    DialogResult = DialogResult.Yes,
                    Width = 80,
                    Top = 75,
                    Left = 85
                };

                Button noButton = new Button
                {
                    Text = "No",
                    DialogResult = DialogResult.No,
                    Width = 80,
                    Top = 75,
                    Left = 175
                };

                confirmForm.AcceptButton = yesButton;
                confirmForm.CancelButton = noButton;

                confirmForm.Controls.Add(label);
                confirmForm.Controls.Add(yesButton);
                confirmForm.Controls.Add(noButton);

                result = confirmForm.ShowDialog(form) == DialogResult.Yes;
            });

            return result;
        }

        private Form? loadingForm;

        public void ShowLoading(string text = "Loading...")
        {
            form.Invoke(() =>
            {
                // Already showing
                if (loadingForm != null)
                    return;

                loadingForm = new Form
                {
                    Text = "Sπk",
                    Size = new Size(350, 140),
                    StartPosition = FormStartPosition.CenterParent,
                    FormBorderStyle = FormBorderStyle.FixedDialog,
                    MaximizeBox = false,
                    MinimizeBox = false,
                    ControlBox = false,
                    ShowInTaskbar = false
                };

                Label label = new Label
                {
                    Text = text,
                    Dock = DockStyle.Top,
                    Height = 55,
                    TextAlign = ContentAlignment.MiddleCenter
                };

                ProgressBar progressBar = new ProgressBar
                {
                    Style = ProgressBarStyle.Marquee,
                    MarqueeAnimationSpeed = 30,
                    Width = 280,
                    Height = 25,
                    Left = 27,
                    Top = 65
                };

                loadingForm.Controls.Add(label);
                loadingForm.Controls.Add(progressBar);

                loadingForm.FormClosed += (_, _) =>
                {
                    loadingForm = null;
                };

                loadingForm.Show(form);
                loadingForm.BringToFront();
            });
        }

        public void HideLoading()
        {
            if (form.InvokeRequired)
            {
                form.Invoke(HideLoading);
                return;
            }

            if (loadingForm == null)
                return;

            loadingForm?.Close();
            loadingForm?.Dispose();
            loadingForm = null;
        }


        public string AlertTextInput(string text)
        {
            string inputResult = string.Empty;

            form.Invoke(() =>
            {
                Form inputForm = new Form
                {
                    Text = "Sπk",
                    Size = new Size(350, 180),
                    StartPosition = FormStartPosition.CenterParent,
                    FormBorderStyle = FormBorderStyle.FixedDialog,
                    MaximizeBox = false,
                    MinimizeBox = false
                };

                Label label = new Label
                {
                    Text = text,
                    Dock = DockStyle.Top,
                    Height = 40,
                    TextAlign = ContentAlignment.MiddleCenter
                };

                TextBox textBox = new TextBox
                {
                    Width = 280,
                    Top = 45,
                    Left = 28
                };

                Button okButton = new Button
                {
                    Text = "OK",
                    DialogResult = DialogResult.OK,
                    Width = 80,
                    Top = 80,
                    Left = 128
                };

                inputForm.AcceptButton = okButton;

                inputForm.Controls.Add(label);
                inputForm.Controls.Add(textBox);
                inputForm.Controls.Add(okButton);

                if (inputForm.ShowDialog(form) == DialogResult.OK)
                {
                    inputResult = textBox.Text;
                }
            });

            return inputResult;
        }

        public void Popup(string text)
        {
            form.Invoke(() =>
            {
                Form popupForm = new Form
                {
                    Text = "Sπk",
                    Size = new Size(300, 150),
                    StartPosition = FormStartPosition.CenterParent
                };

                Label label = new Label
                {
                    Text = text,
                    AutoSize = false,
                    Dock = DockStyle.Fill,
                    TextAlign = ContentAlignment.MiddleCenter
                };

                popupForm.Controls.Add(label);
                popupForm.ShowDialog(form);
            });
        }

        public void ErrorNoSelected()
        {
            ErrorAlert("You have no device selected. Press the power label to select the device.");
        }

        public void ErrorAlert(string text)
        {
            if (form.IsDisposed)
                return;
            form.Invoke(() =>
            {
                MessageBox.Show(
                    form,
                    text,
                    "Error",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error
                );
            });
        }

        public void InfoAlert(string text)
        {
            form.Invoke(() =>
            {
                MessageBox.Show(
                    form,
                    text,
                    "Info",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information
                );
            });
        }
        public string? PickDialog(string[] options)
        {
            string? result = null;

            form.Invoke(() =>
            {
                using Form dialog = new Form
                {
                    Text = "Select an option",
                    Size = new Size(300, 300),
                    StartPosition = FormStartPosition.CenterParent,
                    FormBorderStyle = FormBorderStyle.FixedDialog,
                    MaximizeBox = false,
                    MinimizeBox = false
                };

                ListBox listBox = new ListBox
                {
                    Dock = DockStyle.Fill
                };

                listBox.Items.AddRange(options);

                Button okButton = new Button
                {
                    Text = "OK",
                    Dock = DockStyle.Bottom,
                    Height = 35,
                    DialogResult = DialogResult.OK
                };

                dialog.Controls.Add(listBox);
                dialog.Controls.Add(okButton);

                dialog.AcceptButton = okButton;

                if (dialog.ShowDialog(form) == DialogResult.OK &&
                    listBox.SelectedItem != null)
                {
                    result = listBox.SelectedItem.ToString();
                }
            });

            return result;
        }
    }
}
