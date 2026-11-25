
using System.Drawing.Drawing2D;

public class RoundedLabel : Label
{
    public Color ChipColor { get; set; } = Color.FromArgb(75, 105, 155);

    public float CornerHighlight { get; set; } = 0.22f;

    public RoundedLabel()
    {
        BackColor = Color.Transparent;
        ForeColor = Color.White;
        TextAlign = ContentAlignment.MiddleCenter;

        AutoSize = false;
        DoubleBuffered = true;
        Padding = new Padding(12, 4, 12, 4);
        MinimumSize = new Size(48, 30);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        e.Graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;

        float inset = 2f;

        var rect = new RectangleF(
            inset,
            inset,
            Width - (inset * 2) - 1,
            Height - (inset * 2) - 1
        );

        if (rect.Width <= 0 || rect.Height <= 0)
            return;

        using var path = CreatePillPath(rect);

        // Soft shadow underneath the bubble.
        using (var shadowPath = CreatePillPath(
            new RectangleF(rect.X, rect.Y + 2f,
                rect.Width, rect.Height)))
        using (var shadowBrush = new SolidBrush(
            Color.FromArgb(45, 0, 0, 0)))
        {
            e.Graphics.FillPath(shadowBrush, shadowPath);
        }

        // Main bubble gradient.
        Color topColor = ControlPaint.Light(ChipColor, 0.28f);
        Color bottomColor = ControlPaint.Dark(ChipColor, 0.08f);

        using (var gradient = new LinearGradientBrush(
            rect,
            topColor,
            bottomColor,
            LinearGradientMode.Vertical))
        {
            e.Graphics.FillPath(gradient, path);
        }

        // Soft glossy highlight in the upper portion.
        float highlightHeight = rect.Height * 0.48f;

        var highlightRect = new RectangleF(
            rect.X + 2f,
            rect.Y + 1f,
            rect.Width - 4f,
            highlightHeight
        );

        using (var highlightPath = CreatePillPath(highlightRect))
        using (var highlightBrush = new LinearGradientBrush(
            highlightRect,
            Color.FromArgb(
                (int)(CornerHighlight * 255),
                Color.White),
            Color.FromArgb(0, Color.White),
            LinearGradientMode.Vertical))
        {
            e.Graphics.FillPath(highlightBrush, highlightPath);
        }

        // Text.
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

        float radius = rect.Height;

        if (rect.Width <= rect.Height)
        {
            path.AddEllipse(rect);
            return path;
        }

        path.AddArc(
            rect.X, rect.Y,
            radius, radius,
            90, 180);

        path.AddArc(
            rect.Right - radius, rect.Y,
            radius, radius,
            270, 180);

        path.CloseFigure();

        return path;
    }
}
