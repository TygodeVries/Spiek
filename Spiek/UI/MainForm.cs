using Spiek.Devices;
using System.Diagnostics;
using System.Net.NetworkInformation;
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
        private Form form;

        public void Invoke(Action action)
        {
            form.Invoke(action);
        }

        public MainForm()
        {
            instance = this;

            form = new Form()
            {
                WindowState = FormWindowState.Maximized
            };

            form.Icon = new Icon("App.ico");

            form.Focus();

            form.Resize += (_, _) => RecalculateLayout();
            form.Move += (_, _) => RecalculateLayout();
            form.Shown += (_, _) =>
            {
                Setup();
                ShowNoConnections();

                form.Invoke(() =>
                {
                    StartUpdate();
                    form.BackColor = Color.Black;
                });
            };


            Application.Run(form);
        }

        private static System.Windows.Forms.Timer? updateTimer;
        public static void StartUpdate()
        {
            updateTimer = new System.Windows.Forms.Timer();
            updateTimer.Tick += new EventHandler(Update);
            updateTimer.Interval = 5000;
            updateTimer.Start();
        }

        private static void Update(object? sender, EventArgs e)
        {
            Program.questDiscover?.Broadcast("SPIEK_PING");
        }

        private ToolStripDropDownButton AddDropDown(
            ToolStrip toolStrip,
            string text,
            params (string Name, Action Click)[] items)
        {
            var button = new ToolStripDropDownButton(text);

            foreach (var (name, click) in items)
            {
                var menuItem = new ToolStripMenuItem(name);
                menuItem.Click += (_, _) => click();

                button.DropDownItems.Add(menuItem);
            }

            toolStrip.Items.Add(button);
            return button;
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

            AddDropDown(toolStrip, "Connect",
                ("Meta Quest over USB", () => ManualUsbConnect()),
                ("Meta Quest over IP", () => ManualIpConnect()),
                ("Ping on Network", () =>
                {
                    Program.questDiscover.Broadcast("SPIEK_PING");
                }
            ));

            /*
             * AddDropDown(toolStrip, "Settings",
                ("Static Ip List", () => OpenStaticIpList()));
            */

            AddDropDown(toolStrip, "Advanced",
                ("Power-off All", () =>
                {
                    foreach (VRDevice device in devices)
                    {
                        if (device is QuestVRDevice quest)
                        {
                            quest.SendCommand("shell reboot -p");
                        }
                    }
                }
            ),
                ("Patch USB with auto startup", () => PatchUsb.Patch())
                );

            AddDropDown(toolStrip, "About",
                ("Version", () => Popup("Sπk is on version 1.0")),
                ("Credits", () => Popup("Developer: Tygo\nIcons and Logo: DJ\nCreated for the Digial Art Factory")));

            form.Invoke(() =>
            {
                form.Controls.Add(toolStrip);
            });
        }

        public void OpenStaticIpList()
        {
            if (!File.Exists("static-ips.txt"))
            {
                File.WriteAllText("static-ips.txt", "# Add a list of static ips for your Meta Quest headsets.");
                InfoAlert("Created static-ips.txt");
            }

            Process.Start("notepad.exe", "static-ips.txt");
        }

        public void PingStaticIps()
        {
            if (!File.Exists("static-ips.txt"))
            {
                File.WriteAllText(
                    "static-ips.txt",
                    "# Add a list of static ips for your Meta Quest headsets."
                );
            }

            string[] lines = File.ReadAllLines("static-ips.txt");

            List<string> ips = new List<string>();

            foreach (string line in lines)
            {
                string ip = line.Trim();

                if (string.IsNullOrWhiteSpace(ip) || ip.StartsWith("#"))
                    continue;

                ips.Add(ip);
            }

            form.Cursor = Cursors.WaitCursor;
            List<string> online = new List<string>();

            foreach (string ip in ips)
            {
                var result = CheckDeviceOnline(ip, 1000);

                if (result.IsOnline)
                    online.Add(result.IP);
            }

            form.Cursor = Cursors.Default;
            InfoAlert(
                $"Pinged {ips.Count} devices, {online.Count} of those are active."
            );

            foreach (string ip in online)
            {
                QuestVRDevice questVRDevice = new QuestVRDevice(ip.Trim(), 5555);
                AddDevice(questVRDevice);
            }
        }


        private static (string IP, bool IsOnline) CheckDeviceOnline(
    string ipAddress,
    int timeoutMs)
        {
            try
            {
                using Ping ping = new Ping();

                Console.WriteLine($"Pinging {ipAddress}");

                PingReply reply = ping.Send(ipAddress, timeoutMs);

                Console.WriteLine(
                    $"Finished {ipAddress}: {reply.Status} ({reply.RoundtripTime}ms)");

                return (ipAddress, reply.Status == IPStatus.Success);
            }
            catch (Exception ex)
            {
                Console.WriteLine(
                    $"Failed {ipAddress}: {ex.GetType().Name}: {ex.Message}");

                return (ipAddress, false);
            }
        }



        public List<DeviceWindow> deviceWindows = new List<DeviceWindow>();

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
                InfoAlert("No devices are attached.");
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


        private PictureBox? noConnectionsBox = null;
        public void ShowNoConnections()
        {
            // Already showing
            if (noConnectionsBox != null)
                return;

            form.Invoke(() =>
            {
                noConnectionsBox = new PictureBox()
                {
                    Image = new Bitmap("images/no-headsets.png"),
                    Dock = DockStyle.Fill,
                    SizeMode = PictureBoxSizeMode.Zoom
                };

                form.Controls.Add(noConnectionsBox);
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
            });
        }

        private List<VRDevice> devices = new List<VRDevice>();

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

        private void AddDeviceWindow(VRDevice vrDevice)
        {
            devices.Add(vrDevice);

            DeviceWindow deviceWindow = new DeviceWindow(vrDevice);
            deviceWindows.Add(deviceWindow);

            form.Controls.Add(deviceWindow);
            deviceWindow.Show();

            HideNoConnections();
            RecalculateLayout();
            deviceWindow.ApplyVisualFormat();

            vrDevice.OnDisconnect += () =>
            {
                form.Invoke(() =>
                {
                    deviceWindows.Remove(deviceWindow);
                    devices.Remove(vrDevice);

                    deviceWindow.Close();

                    if (deviceWindows.Count == 0)
                        ShowNoConnections();
                });
            };
        }



        public void RecalculateLayout()
        {
            form.Invoke(() =>
            {
                form.BackColor = Color.Black;
                int i = 0;
                foreach (DeviceWindow window in deviceWindows)
                {
                    Vector4 pos = GetWindowRect(i);
                    window.Move((int)pos.X, (int)pos.Y, (int)pos.Z, (int)pos.W);
                    i++;
                }

                form.Refresh();
            });
        }

        public void ManualIpConnect()
        {
            string ip = PopupInput("Please enter the IP of the Meta Quest");
            QuestVRDevice device = new QuestVRDevice(ip, 5555);
            AddDevice(device);
        }

        public string PopupInput(string text)
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

        public Vector4 GetWindowRect(int index)
        {
            int count = deviceWindows.Count;
            if (count == 0)
                return Vector4.Zero;

            int cols = (int)Math.Ceiling(Math.Sqrt(count));
            int rows = (int)Math.Ceiling((double)count / cols);

            float cellWidth = form.Width / cols;
            float cellHeight = form.Height / rows;

            int row = index / cols;
            int col = index % cols;

            float x = col * cellWidth;
            float y = row * cellHeight;

            return new Vector4(
                x,
                y,
                cellWidth,
                cellHeight
            );
        }
    }
}
