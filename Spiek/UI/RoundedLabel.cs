using System.Drawing.Drawing2D;

public class RoundedLabel : Label
{
    public Color ChipColor { get; set; } = Color.FromArgb(40, 44, 52);

    // Thickness of the outer border in pixels.
    public float BorderThickness { get; set; } = 4f;

    // How much the border is desaturated.
    public float BorderDesaturation { get; set; } = 0.35f;

    public RoundedLabel()
    {
        BackColor = Color.Transparent;
        ForeColor = Color.WhiteSmoke;
        TextAlign = ContentAlignment.MiddleCenter;

        AutoSize = false;
        DoubleBuffered = true;
        Padding = new Padding(10, 3, 10, 3);
        MinimumSize = new Size(48, 28);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        Color borderColor = ChipColor;

        // A muted, lighter version of the chip color.
        Color bodyColor = DesaturateColor(
            ControlPaint.Light(ChipColor, 0.18f),
            BorderDesaturation
        );

        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        e.Graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;

        float thickness = Math.Max(0f, BorderThickness);
        float inset = (thickness / 2f) + 0.5f;

        var rect = new RectangleF(
            inset,
            inset,
            Width - (2f * inset) - 1f,
            Height - (2f * inset) - 1f
        );

        if (rect.Width <= 0 || rect.Height <= 0)
            return;

        // A pill-shaped chip with fully rounded ends.
        using var path = CreatePillPath(rect);

        // Slightly darker interior keeps the thick border distinct.
        using (var fillBrush = new SolidBrush(bodyColor))
        {
            e.Graphics.FillPath(fillBrush, path);
        }

        if (thickness > 0)
        {
            using var borderPen = new Pen(borderColor, thickness)
            {
                LineJoin = LineJoin.Round,
                Alignment = PenAlignment.Center
            };

            e.Graphics.DrawPath(borderPen, path);
        }

        // Draw text last so it remains crisp and readable.
        TextRenderer.DrawText(
            e.Graphics,
            Text,
            Font,
            ClientRectangle,
            ForeColor,
            TextFormatFlags.HorizontalCenter |
            TextFormatFlags.VerticalCenter |
            TextFormatFlags.SingleLine |
            TextFormatFlags.EndEllipsis |
            TextFormatFlags.NoPadding
        );
    }

    private static GraphicsPath CreatePillPath(RectangleF rect)
    {
        var path = new GraphicsPath();

        float diameter = Math.Min(rect.Height, rect.Width);

        if (diameter <= 0)
            return path;

        float radius = rect.Height;

        if (rect.Width <= rect.Height)
        {
            path.AddEllipse(rect);
            return path;
        }

        path.AddArc(
            rect.X, rect.Y,
            radius, radius,
            90, 180
        );

        path.AddArc(
            rect.Right - radius, rect.Y,
            radius, radius,
            270, 180
        );

        path.CloseFigure();

        return path;
    }

    private static Color DesaturateColor(Color color, float amount)
    {
        amount = Math.Clamp(amount, 0f, 1f);

        int gray = (int)(
            (0.299 * color.R) +
            (0.587 * color.G) +
            (0.114 * color.B)
        );

        int r = (int)(color.R + ((gray - color.R) * amount));
        int g = (int)(color.G + ((gray - color.G) * amount));
        int b = (int)(color.B + ((gray - color.B) * amount));

        return Color.FromArgb(color.A, r, g, b);
    }
}