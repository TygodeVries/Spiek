using Spiek.Devices;
using Spiek.Utils;

namespace Spiek.UI.Update
{
    public class Bubble
    {
        public bool initialized = false;
        public float x;
        public float y;
        public int xOffset;
        public int yOffset;
        public float phase;
        public bool isFull;
        public bool isLogo;
        public float size;

        // Small bubble animation
        public float speed;
        public float wiggleSpeed;
        public float wiggleAmount;
        public Headset device;
        public DeviceInfo info;
    }
}