using Spiek.Devices;
using Spiek.UI.Update;
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
        private List<Headset> devices = new List<Headset>();
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
                ShowSearching();
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

            bool fullscreen = false;

            form.KeyPreview = true;

            form.KeyDown += (_, e) =>
            {
                if (e.KeyCode == Keys.F11)
                {
                    fullscreen = !fullscreen;

                    if (fullscreen)
                    {
                        form.FormBorderStyle = FormBorderStyle.None;
                        form.WindowState = FormWindowState.Maximized;
                    }
                    else
                    {
                        form.FormBorderStyle = FormBorderStyle.Sizable;
                        form.WindowState = FormWindowState.Normal;
                    }
                }
            };

            searchingBox = new Searching();

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

            Console.WriteLine($"Started to send out ping messages on network with an interval of {updateTimer.Interval}ms");
            Tick(null, EventArgs.Empty);
        }

        private static void Tick(object? sender, EventArgs e)
        {
            Program.questDiscover?.Broadcast("SPIEK_PING");
        }

        public Headset[] GetDevices()
        {
            return devices.ToArray();
        }

        public Headset? GetSelectedDevice()
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
            AddDevice(new DummyHeadset());
        }
        public void ManualUsbConnect()
        {
            List<string>? devices = MetaQuestUtilities.GetMetaQuestUsbDevices();
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
                AddDevice(new MetaQuest(devices.First()));
                return;
            }

            // #TODO show pick dialog
            string? device = PickDialog(devices.ToArray());
            if (device == null)
                return;

            AddDevice(new MetaQuest(device));
        }


        private Searching searchingBox;
        public void ShowSearching()
        {
            form.Invoke(() =>
            {
                form.Controls.Add(searchingBox);
            });
        }

        public static event Action<Headset>? OnDeviceAdded;
        public void AddDevice(Headset vrDevice)
        {
            foreach (Headset other in devices)
            {
                if (other == null)
                    continue;

                if (other is MetaQuest quest1 && vrDevice is MetaQuest quest2)
                {
                    if (quest1.Serial != null && quest2.Serial != null && quest1.Serial == quest2.Serial)
                    {
                        Console.WriteLine("[WARN] Device already connected (Same Serial)");
                        return;
                    }
                }

                if (other.GetUniqueAddress() == vrDevice.GetUniqueAddress())
                {
                    Console.WriteLine("[WARN] Device already connected (Same Address)");
                    return;
                }
            }

            // Device connection can happen outside UI thread
            if (vrDevice.State != DeviceState.Connected)
                vrDevice.Connect();

            OnDeviceAdded?.Invoke(vrDevice);

            form.Invoke(() =>
            {
                AddDeviceWindow(vrDevice);
            });
        }

        public Form GetForm()
        {
            return form;
        }

        private void AddDeviceWindow(Headset vrDevice)
        {
            devices.Add(vrDevice);

            DeviceWindow deviceWindow = new DeviceWindow(vrDevice);
            DeviceWindows.Add(deviceWindow);

            form.Controls.Add(deviceWindow);

            deviceWindow.BringToFront();
            deviceWindow.Show();

            RecalculateLayout();
            deviceWindow.ApplyVisualFormat();

            vrDevice.OnDisconnect += () =>
            {
                form.Invoke(() =>
                {
                    ErrorAlert("A device has disconnected.");
                    DeviceWindows.Remove(deviceWindow);
                    devices.Remove(vrDevice);

                    deviceWindow.Close();
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
            foreach (Headset device in devices)
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
            if (form.IsDisposed || !form.IsHandleCreated)
                return;

            form.Invoke(() =>
            {
                if (Overlay != null)
                {
                    Overlay.Size = form.Size;
                    Overlay.Location = form.Location;
                }

                int offset = 50;

                Size size = form.ClientSize - new Size(0, offset);

                form.BackColor = Color.Black;

                // Only casting devices participate in the layout.
                var activeWindows = DeviceWindows
                    .Where(window => window.IsCasting())
                    .ToList();

                // Keep all parent controls alive and visible.
                foreach (DeviceWindow window in DeviceWindows)
                {
                    window.Visible = true;

                    if (!activeWindows.Contains(window))
                    {
                        window.Move(0, 0, 0, 0);
                    }
                }

                if (activeWindows.Count == 0)
                {
                    form.Refresh();
                    return;
                }

                int count = activeWindows.Count;

                int cols = SelectedWindow != null &&
                           activeWindows.Contains(SelectedWindow)
                    ? 1
                    : (int)Math.Ceiling(Math.Sqrt(count));

                int rows = SelectedWindow != null &&
                           activeWindows.Contains(SelectedWindow)
                    ? 1
                    : (int)Math.Ceiling((double)count / cols);

                if (SelectedWindow != null &&
                    activeWindows.Contains(SelectedWindow))
                {
                    float selectedWidth = size.Width * 0.8f;
                    float sideWidth = size.Width - selectedWidth;

                    SelectedWindow.Move(
                        0, offset,
                        (int)selectedWidth,
                        size.Height
                    );

                    var others = activeWindows
                        .Where(x => x != SelectedWindow)
                        .ToList();

                    for (int i = 0; i < others.Count; i++)
                    {
                        float height = (float)size.Height /
                                       others.Count;

                        others[i].Move(
                            (int)selectedWidth,
                            (int)(i * height) + offset,
                            (int)sideWidth,
                            (int)height
                        );
                    }
                }
                else
                {
                    float cellWidth = (float)size.Width / cols;
                    float cellHeight = (float)size.Height / rows;

                    for (int i = 0; i < count; i++)
                    {
                        int row = i / cols;
                        int col = i % cols;

                        activeWindows[i].Move(
                            (int)(col * cellWidth),
                            (int)(row * cellHeight) + offset,
                            (int)cellWidth,
                            (int)cellHeight
                        );
                    }
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
            MetaQuest device = new MetaQuest(ip, 5555);
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
                using Form inputForm = new Form
                {
                    Text = "Sπk",
                    Size = new Size(420, 230),
                    StartPosition = FormStartPosition.CenterParent,
                    FormBorderStyle = FormBorderStyle.FixedDialog,
                    MaximizeBox = false,
                    MinimizeBox = false,
                    ShowInTaskbar = false,
                    BackColor = Color.FromArgb(28, 30, 38),
                    ForeColor = Color.White,
                    Font = new Font("Segoe UI", 10F),
                };

                Label header = new Label
                {
                    Text = "Enter a value",
                    Font = new Font("Segoe UI Semibold", 15F),
                    ForeColor = Color.White,
                    Location = new Point(24, 18),
                    AutoSize = true
                };

                Label label = new Label
                {
                    Text = text,
                    Font = new Font("Segoe UI", 9.5F),
                    ForeColor = Color.FromArgb(180, 185, 200),
                    Location = new Point(26, 62),
                    Size = new Size(350, 35),
                    AutoEllipsis = true
                };

                TextBox textBox = new TextBox
                {
                    Location = new Point(26, 105),
                    Size = new Size(350, 30),
                    Font = new Font("Segoe UI", 11F),
                    BackColor = Color.FromArgb(43, 46, 57),
                    ForeColor = Color.White,
                    BorderStyle = BorderStyle.FixedSingle
                };

                Button okButton = new Button
                {
                    Text = "Continue",
                    DialogResult = DialogResult.OK,
                    Size = new Size(110, 36),
                    Location = new Point(266, 155),
                    FlatStyle = FlatStyle.Flat,
                    BackColor = Color.FromArgb(105, 95, 230),
                    ForeColor = Color.White,
                    Font = new Font("Segoe UI Semibold", 9.5F),
                    Cursor = Cursors.Hand
                };

                okButton.FlatAppearance.BorderSize = 0;
                okButton.FlatAppearance.MouseOverBackColor =
                    Color.FromArgb(125, 115, 245);

                Button cancelButton = new Button
                {
                    Text = "Cancel",
                    DialogResult = DialogResult.Cancel,
                    Size = new Size(100, 36),
                    Location = new Point(155, 155),
                    FlatStyle = FlatStyle.Flat,
                    BackColor = Color.FromArgb(43, 46, 57),
                    ForeColor = Color.FromArgb(190, 195, 210),
                    Cursor = Cursors.Hand
                };

                cancelButton.FlatAppearance.BorderSize = 0;

                inputForm.Controls.Add(header);
                inputForm.Controls.Add(label);
                inputForm.Controls.Add(textBox);
                inputForm.Controls.Add(cancelButton);
                inputForm.Controls.Add(okButton);

                inputForm.AcceptButton = okButton;
                inputForm.CancelButton = cancelButton;

                inputForm.Shown += (_, _) => textBox.Focus();

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
