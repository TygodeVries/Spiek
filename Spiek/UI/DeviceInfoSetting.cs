using Spiek.Utils;

namespace Spiek.UI
{
    public class DeviceInfoSetting : Form
    {
        private DeviceInfo info;

        private TextBox nameTextBox = null!;
        private TextBox colorTextBox = null!;

        public event Action? OnSave;

        public DeviceInfo GetInfo()
        {
            return info;
        }

        public DeviceInfoSetting(DeviceInfo info)
        {
            this.info = info;

            InitializeComponent();
            LoadValues();
        }

        private void InitializeComponent()
        {
            Text = "Device Settings";
            StartPosition = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            ClientSize = new Size(400, 180);

            var nameLabel = new Label
            {
                Text = "Name:",
                AutoSize = true,
                Location = new Point(20, 25)
            };

            nameTextBox = new TextBox
            {
                Location = new Point(120, 20),
                Width = 240
            };

            var colorLabel = new Label
            {
                Text = "Color:",
                AutoSize = true,
                Location = new Point(20, 70)
            };

            colorTextBox = new TextBox
            {
                Location = new Point(120, 65),
                Width = 240
            };

            var cancelButton = new Button
            {
                Text = "Cancel",
                DialogResult = DialogResult.Cancel,
                Location = new Point(200, 115),
                Width = 75
            };

            var saveButton = new Button
            {
                Text = "Save",
                Location = new Point(285, 115),
                Width = 75
            };

            saveButton.Click += SaveButton_Click;

            Controls.Add(nameLabel);
            Controls.Add(nameTextBox);
            Controls.Add(colorLabel);
            Controls.Add(colorTextBox);
            Controls.Add(cancelButton);
            Controls.Add(saveButton);

            AcceptButton = saveButton;
            CancelButton = cancelButton;
        }

        private void LoadValues()
        {
            nameTextBox.Text = info.Name;
            colorTextBox.Text = info.HexColor;
        }

        private void SaveButton_Click(object? sender, EventArgs e)
        {
            var name = nameTextBox.Text.Trim();
            var hexColor = colorTextBox.Text.Trim();

            if (string.IsNullOrWhiteSpace(name))
            {
                MessageBox.Show(
                    "Please enter a device name.",
                    "Invalid Name",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);

                nameTextBox.Focus();
                return;
            }

            if (!IsValidHexColor(hexColor))
            {
                MessageBox.Show(
                    "Please enter a valid hex color, for example #FF00FF.",
                    "Invalid Color",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);

                colorTextBox.Focus();
                return;
            }

            info.Name = name;
            info.HexColor = hexColor;

            OnSave?.Invoke();

            DialogResult = DialogResult.OK;
            Close();
        }

        private static bool IsValidHexColor(string value)
        {
            if (value.Length != 7 || value[0] != '#')
                return false;

            return value.Skip(1).All(Uri.IsHexDigit);
        }
    }
}