using Spiek.UI;

public class Overlay : Form
{
    private Font font;
    public Overlay()
    {
        font = new Font("Segoe UI", 40f);
        FormBorderStyle = FormBorderStyle.None;
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.Manual;
        TopMost = true;

        SetStyle(
            ControlStyles.UserPaint |
            ControlStyles.AllPaintingInWmPaint |
            ControlStyles.OptimizedDoubleBuffer,
            true
        );

        TransparencyKey = Color.Magenta;
        BackColor = Color.Magenta;

        Button button = new Button()
        {
            Text = "Testing, Testing!",
            AutoSize = true,
            BackColor = Color.White
        };

        button.Click += (_, _) =>
        {
            MainForm.GetInstance().InfoAlert("You pressed the button!");
        };

        Controls.Add(button);
    }
}
