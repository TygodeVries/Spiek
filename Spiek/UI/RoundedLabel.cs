using System.Drawing.Drawing2D;

public class RoundedLabel : Label
{
    public Color ChipColor { get; set; } = Color.FromArgb(40, 44, 52);
    public int BorderThickness { get; set; } = 0;
    public RoundedLabel()
    {
        BackColor = Color.Transparent;
        TextAlign = ContentAlignment.MiddleCenter;
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        e.Graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;

        int diameter = Height - 1;

        float strokeOffset = BorderThickness / 2f;
        var rect = new RectangleF(
            strokeOffset,
            strokeOffset,
            Width - BorderThickness,
            Height - BorderThickness
        );

        using var path = GetPillPath(rect, diameter - BorderThickness);

        using (var bgBrush = new SolidBrush(ChipColor))
        {
            e.Graphics.FillPath(bgBrush, path);
        }

        Color borderColor = BrightenColor(ChipColor);
        using (var borderPen = new Pen(borderColor, BorderThickness))
        {
            borderPen.Alignment = PenAlignment.Center;
            e.Graphics.DrawPath(borderPen, path);
        }

        TextRenderer.DrawText(
            e.Graphics,
            Text,
            Font,
            ClientRectangle,
            ForeColor,
            TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter
        );
    }

    private static GraphicsPath GetPillPath(RectangleF rect, float radius)
    {
        var path = new GraphicsPath();
        if (radius <= 0)
        {
            path.AddRectangle(rect);
            return path;
        }

        path.AddArc(rect.X, rect.Y, radius, radius, 180, 90);
        path.AddArc(rect.Right - radius, rect.Y, radius, radius, 270, 90);
        path.AddArc(rect.Right - radius, rect.Bottom - radius, radius, radius, 0, 90);
        path.AddArc(rect.X, rect.Bottom - radius, radius, radius, 90, 90);
        path.CloseFigure();

        return path;
    }

    private static Color BrightenColor(Color color)
    {
        int r = Math.Min(255, color.R + 20);
        int g = Math.Min(255, color.G + 20);
        int b = Math.Min(255, color.B + 20);
        return Color.FromArgb(color.A, r, g, b);
    }
}