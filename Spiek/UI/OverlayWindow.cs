namespace Spiek.UI
{
    public class OverlayWindow : Form
    {
        private const int WS_EX_NOACTIVATE = 0x08000000;

        public OverlayWindow()
        {
            BackColor = Color.Pink;
            TransparencyKey = Color.Pink;

            TopMost = true;
            FormBorderStyle = FormBorderStyle.None;
            ShowInTaskbar = false;

            SetStyle(ControlStyles.OptimizedDoubleBuffer, true);

            MainForm.GetInstance().GetForm().Activated += (sender, args) =>
            {
                this.Show();
                this.BringToFront();
                Refresh();
                MainForm.GetInstance().GetForm().Refresh();
                Invalidate();
                MainForm.GetInstance().GetForm().Invalidate();
            };

            MainForm.GetInstance().GetForm().Deactivate += (sender, args) =>
            {
                this.Hide();
            };
        }

        protected override CreateParams CreateParams
        {
            get
            {
                CreateParams cp = base.CreateParams;
                cp.ExStyle |= WS_EX_NOACTIVATE;
                return cp;
            }
        }

        public void AddChip(RoundedLabel chip, DeviceWindow deviceWindow)
        {
            chip.Click += (sender, args) =>
            {
                MainForm.GetInstance().SetSelectedWindow(deviceWindow);
            };
            Controls.Add(chip);
        }

        public void RemoveChip(RoundedLabel chip)
        {
            Controls.Remove(chip);
        }

        private const int WM_NCHITTEST = 0x0084;
        private const int HTTRANSPARENT = -1;
        private const int HTCLIENT = 1;

        protected override void WndProc(ref Message m)
        {
            base.WndProc(ref m);

            if (m.Msg == WM_NCHITTEST)
            {
                int x = m.LParam.ToInt32() & 0xFFFF;
                int y = (m.LParam.ToInt32() >> 16) & 0xFFFF;

                Point screenPoint = new Point(x, y);
                Point clientPoint = this.PointToClient(screenPoint);

                Control child = this.GetChildAtPoint(clientPoint);

                if (child == null)
                {
                    m.Result = HTTRANSPARENT;
                }
                else
                {
                    m.Result = HTCLIENT;
                }
            }
        }
    }
}