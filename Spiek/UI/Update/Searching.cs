using Spiek.Devices;
using Spiek.Utils;
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
        MainForm.OnDeviceAdded += OnDeviceAdded;
        var timer = new System.Windows.Forms.Timer { Interval = 33 };
        timer.Tick += (_, _) => Invalidate();
        timer.Start();

        time.Start();



        deviceBubbles = new List<Bubble>();
        smallBubbles = new List<Bubble>();

        for (int i = 0; i < 10; i++)
            smallBubbles.Add(new Bubble()
            {
                size = 30
            });

        foreach (DeviceInfo deviceInfo in Settings.Instance.Devices)
        {
            deviceBubbles.Add(new Bubble()
            {
                size = 300,
                info = deviceInfo
            });
        }

    }

    public void OnDeviceAdded(Headset device)
    {
        DeviceInfo info = device.GetDeviceInfo();

        foreach (Bubble bubble in deviceBubbles)
        {
            if (bubble.info.DeviceId == info.DeviceId)
            {
                bubble.device = device;
                return;
            }
        }

        Bubble bubble1 = new Bubble()
        {
            size = 300,
            info = info,
            device = device
        };

        lock (deviceBubbles)
        {
            deviceBubbles.Add(bubble1);
        }
    }

    public List<Bubble> deviceBubbles;
    public List<Bubble> smallBubbles;

    private Stopwatch time = new Stopwatch();
    protected override void OnPaint(PaintEventArgs e)
    {

        UpdatePulse();
        Graphics g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.InterpolationMode = InterpolationMode.HighQualityBicubic;


        // Gradient Background
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

        // Small bubbles
        UpdateSmallBubbles(smallBubbles);

        foreach (Bubble bubble in smallBubbles)
        {
            Rectangle bubbleRect = new Rectangle((int)bubble.x, (int)bubble.y, (int)bubble.size, (int)bubble.size);
            SolidBrush solidBrush = new SolidBrush(Theme.AccentColorGreen);
            g.FillEllipse(solidBrush, bubbleRect);
        }

        // Headset Bubbles
        lock (deviceBubbles)
        {
            UpdateDeviceBubbles(deviceBubbles);

            foreach (Bubble bubble in deviceBubbles)
            {
                Rectangle bubbleRect = new Rectangle((int)bubble.x, (int)bubble.y, (int)bubble.size, (int)bubble.size);
                Rectangle bubbleImageRect = new Rectangle((int)bubble.x + 50, (int)bubble.y + 50, (int)bubble.size - 100, (int)bubble.size - 100);



                Color color = Theme.AccentColorPurple;

                if (bubble.device != null && bubble.device.State == Devices.DeviceState.Connected)
                {
                    color = Theme.AccentColorGreen;
                }
                else if (bubble.device != null && bubble.device.State == Devices.DeviceState.Disconnected)
                {
                    color = Color.Red;
                }

                SolidBrush solidBrush = new SolidBrush(color);
                SolidBrush backBrush = new SolidBrush(Theme.MainColor);
                Pen pen = new Pen(solidBrush);
                pen.Width = 3;

                DrawWigglyLine(g, new Point(bubbleRect.Location.X + (bubbleRect.Width / 2), bubbleRect.Location.Y + (bubbleRect.Height / 2)), new Point(Width / 2, Height / 2), 5, color);

                g.FillEllipse(backBrush, bubbleRect);
                g.DrawEllipse(pen, bubbleRect);

                g.DrawImage(Theme.Headset, bubbleImageRect);
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

    public void DrawWigglyLine(Graphics graphics, Point a, Point b, float amount, Color col)
    {
        float dx = b.X - a.X;
        float dy = b.Y - a.Y;

        float length = (float)Math.Sqrt((dx * dx) + (dy * dy));

        if (length == 0)
            return;

        float perpX = -dy / length;
        float perpY = dx / length;

        int segments = (int)(length / 3f);

        PointF[] points = new PointF[segments + 1];

        float time = Environment.TickCount / 300f;

        for (int i = 0; i <= segments; i++)
        {
            float t = (float)i / segments;

            float x = a.X + (dx * t);
            float y = a.Y + (dy * t);

            float envelope = (float)Math.Sin(t * Math.PI);

            float wave = (float)Math.Sin((t * 20f) + time) * amount * envelope;

            x += perpX * wave;
            y += perpY * wave;

            points[i] = new PointF(x, y);
        }

        float thickness = 2f + (pulseAmount * 4f);

        Color color = LerpColor(
            col,
            pulseColor,
            pulseAmount
        );

        using (Pen pen = new Pen(color, thickness))
        {
            pen.StartCap = LineCap.Round;
            pen.EndCap = LineCap.Round;
            pen.LineJoin = LineJoin.Round;

            graphics.DrawCurve(pen, points, 0.5f);
        }
    }

    private static float pulseAmount = 0f;
    private static Color pulseColor = Theme.AccentColorPurple;
    private static float pulseDecay = 2.5f;
    private static float lastPulseTime = 0f;

    public static void Pulse(Color color)
    {
        pulseColor = color;
        pulseAmount = 1f;
        lastPulseTime = Environment.TickCount / 1000f;
    }

    private static Color LerpColor(Color from, Color to, float amount)
    {
        return Color.FromArgb(
            (int)(from.A + ((to.A - from.A) * amount)),
            (int)(from.R + ((to.R - from.R) * amount)),
            (int)(from.G + ((to.G - from.G) * amount)),
            (int)(from.B + ((to.B - from.B) * amount))
        );
    }

    private static void UpdatePulse()
    {
        float now = Environment.TickCount / 1000f;
        float delta = now - lastPulseTime;

        pulseAmount *= (float)Math.Exp(-pulseDecay * delta);

        lastPulseTime = now;

        if (pulseAmount < 0.001f)
            pulseAmount = 0f;
    }

    private void UpdateSmallBubbles(List<Bubble> bubbles)
    {
        float elapsed = (float)time.Elapsed.TotalSeconds;

        foreach (Bubble bubble in bubbles)
        {
            // Initialize random movement properties if needed
            if (bubble.speed == 0)
            {
                bubble.speed = Random.Shared.Next(30, 80);
                bubble.phase = (float)(Random.Shared.NextDouble() * Math.PI * 2);
                bubble.wiggleSpeed = Random.Shared.Next(10, 25) / 10f;
                bubble.wiggleAmount = Random.Shared.Next(8, 25);
            }

            // Move upwards
            bubble.y -= bubble.speed / 10f;

            // Horizontal bubble movement
            bubble.x += (float)Math.Sin(
                (elapsed * bubble.wiggleSpeed) + bubble.phase
            ) * 0.8f;

            // Reset when leaving the screen
            if (bubble.y + bubble.size < 0)
            {
                bubble.y = Height + Random.Shared.Next(20, 150);
                bubble.x = Random.Shared.Next(0, Math.Max(1, Width - (int)bubble.size));

                bubble.speed = Random.Shared.Next(30, 80);
                bubble.phase = (float)(Random.Shared.NextDouble() * Math.PI * 2);
            }
        }
    }

    private void UpdateDeviceBubbles(List<Bubble> bubbles)
    {
        float centerX = Width / 2f;
        float centerY = Height / 2f;

        float radiusX = Width * 0.36f;
        float radiusY = Height * 0.36f;

        float elapsed = (float)time.Elapsed.TotalSeconds;

        // Adjust this to control movement smoothness
        float lerpSpeed = 5f;
        float deltaTime = 1f / 60f;
        float lerpFactor = 1f - (float)Math.Exp(-lerpSpeed * deltaTime);

        for (int i = 0; i < bubbles.Count; i++)
        {
            Bubble bubble = bubbles[i];

            float angle = (float)(i * (Math.PI * 2 / bubbles.Count));

            float wiggleX = (float)Math.Sin((elapsed * 1.1f) + (i * 1.7f)) * 22f;
            float wiggleY = (float)Math.Cos((elapsed * 0.9f) + (i * 2.3f)) * 22f;

            float radiusOffset = (float)Math.Sin((elapsed * 0.7f) + (i * 1.4f)) * 12f;

            float targetX = centerX + ((float)Math.Cos(angle) * (radiusX + radiusOffset));
            float targetY = centerY + ((float)Math.Sin(angle) * (radiusY + radiusOffset));

            targetX += wiggleX - (bubble.size / 2f);
            targetY += wiggleY - (bubble.size / 2f);

            // Initialize position for newly added bubbles
            if (!bubble.initialized)
            {
                bubble.x = targetX;
                bubble.y = targetY;
                bubble.initialized = true;
            }
            else
            {
                // Smoothly interpolate toward target
                bubble.x += (targetX - bubble.x) * lerpFactor;
                bubble.y += (targetY - bubble.y) * lerpFactor;
            }
        }
    }
}
