using System.Drawing.Text;

namespace Spiek.UI.Update
{
    public class Theme
    {
        public static Color MainColor { get; private set; } = Color.FromArgb(55, 37, 88);
        public static Color AccentColor { get; private set; } = Color.FromArgb(100, 90, 133);
        public static Color MainColorDark { get; private set; } = Color.FromArgb(55, 37, 88);
        public static Color Text { get; private set; } = Color.White;
        public static Image SpiekLogo { get; private set; } = Image.FromFile("images/Logo-big.png");
        public static Image DafLogo { get; private set; } = Image.FromFile("images/Logo-Daf.png");
        public static Font Font { get; private set; }

        private static PrivateFontCollection privateFonts = new PrivateFontCollection();
        public static void LoadFont()
        {
            privateFonts.AddFontFile("images/font.ttf");
            FontFamily family = privateFonts.Families[0];
            Font = new Font(family, 100, FontStyle.Regular);
        }
    }
}
