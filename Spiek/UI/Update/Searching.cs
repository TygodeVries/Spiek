using System.Diagnostics;
using System.Drawing.Drawing2D;

namespace Spiek.UI.Update;

public class Searching : Control
{
    public Searching()
    {
        SetStyle(
            ControlStyles.AllPaintingInWmPaint |
            ControlStyles.UserPaint |
            ControlStyles.OptimizedDoubleBuffer |
            ControlStyles.ResizeRedraw,
            true);

        BackColor = Theme.MainColor;
        Dock = DockStyle.Fill;

        var timer = new System.Windows.Forms.Timer { Interval = 33 };
        timer.Tick += (_, _) => Invalidate();
        timer.Start();

        time.Start();

        bubbles = new Bubble[50];

    }

    public Bubble[] bubbles;

    private Stopwatch time = new Stopwatch();
    protected override void OnPaint(PaintEventArgs e)
    {
        Graphics g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.InterpolationMode = InterpolationMode.HighQualityBicubic;

        Rectangle bounds = this.ClientRectangle;

        float scale = 1.42f;
        float ellipseWidth = bounds.Width * scale;
        float ellipseHeight = bounds.Height * scale;
        float offsetX = (bounds.Width - ellipseWidth) / 2f;
        float offsetY = (bounds.Height - ellipseHeight) / 2f;

        RectangleF ellipseBounds = new RectangleF(offsetX, offsetY, ellipseWidth, ellipseHeight);

        using (GraphicsPath ellipsePath = new GraphicsPath())
        {
            ellipsePath.AddEllipse(ellipseBounds);
            using (PathGradientBrush radial = new PathGradientBrush(ellipsePath))
            {
                radial.CenterPoint = new PointF(bounds.Width / 2f, bounds.Height / 2f);
                radial.CenterColor = Theme.MainColor;
                radial.SurroundColors = new[] { Theme.MainColorDark };

                g.FillRectangle(radial, bounds);
            }
        }

        using (Pen pen = new Pen(Theme.AccentColor, 7))
        using (SolidBrush brush = new SolidBrush(Theme.AccentColor))
        {
            for (int i = 0; i < bubbles.Length; i++)
            {
                Bubble bubble = bubbles[i];

                float elapsed = (float)time.Elapsed.TotalSeconds;

                Rectangle bubbleRect = new Rectangle(
                    bubble.x,
                    bubble.y,
                    (int)bubble.size,
                    (int)bubble.size
                );

                if (bubble.isFull)
                    g.FillEllipse(brush, bubbleRect);
                else
                {
                    if (bubble.isLogo)
                        g.DrawImage(Theme.DafLogo, bubbleRect);

                    g.DrawEllipse(pen, bubbleRect);
                }

                // Floating movement
                float speed = 15f + (bubble.phase * 3f);

                float floatY = MathF.Sin((elapsed * 1.2f) + bubble.phase) * 12f;
                float floatX = MathF.Sin((elapsed * 0.8f) + bubble.phase) * 25f;

                // Continuous upward movement
                float baseY = Height -
                    (((elapsed * speed) + bubble.yOffset) % (Height + bubble.size));

                bubble.y = (int)(baseY + floatY);

                // Gentle horizontal drifting
                bubble.x = bubble.xOffset +
                    (int)((MathF.Cos((elapsed * 0.6f) + bubble.phase) * 60f) + floatX);
            }
        }


        string text = "Scanning for Devices...";

        using (StringFormat format = new StringFormat(StringFormat.GenericTypographic))
        {
            format.FormatFlags |= StringFormatFlags.MeasureTrailingSpaces;

            // Measure the complete text
            SizeF textSize = g.MeasureString(text, Theme.Font, int.MaxValue, format);

            // Calculate centered starting position
            float startX = (this.ClientSize.Width - textSize.Width) / 2f;
            float startY = (this.ClientSize.Height - textSize.Height) / 2f;

            CharacterRange[] ranges = new CharacterRange[text.Length];

            for (int i = 0; i < text.Length; i++)
                ranges[i] = new CharacterRange(i, 1);

            format.SetMeasurableCharacterRanges(ranges);

            Region[] regions = g.MeasureCharacterRanges(
                text,
                Theme.Font,
                new RectangleF(startX, startY, 5000, 1000),
                format
            );

            using (SolidBrush brush = new SolidBrush(Theme.Text))
            using (SolidBrush brush2 = new SolidBrush(Theme.MainColorDark))
            {
                for (int i = 0; i < text.Length; i++)
                {
                    RectangleF charBounds = regions[i].GetBounds(g);

                    float yOffset = (float)Math.Sin(
                        (i * 0.4) + (Environment.TickCount / 500.0)
                    ) * 10f;

                    g.DrawString(
                        text[i].ToString(),
                        Theme.Font,
                        brush2,
                        new PointF(charBounds.Left + 5, startY + yOffset + 8),
                        format
                    );

                    g.DrawString(
                        text[i].ToString(),
                        Theme.Font,
                        brush,
                        new PointF(charBounds.Left, startY + yOffset),
                        format
                    );

                    regions[i].Dispose();
                }
            }
        }
    }
}
