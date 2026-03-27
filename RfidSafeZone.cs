using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Drawing;

namespace ZebraRFIDApp
{
    public static class RfidSafeZone
    {
        // Zona del IC: centrada, 8x8 mm = 64x64 dots (203 dpi)
        private static readonly Rectangle IcZone = new Rectangle(336, 48, 64, 64);

        public static Rectangle AjustarSiInvadeIC(Rectangle original)
        {
            if (!original.IntersectsWith(IcZone))
                return original;

            int nuevoX = IcZone.Right + 10; // margen extra de 10 dots
            return new Rectangle(nuevoX, original.Y, original.Width, original.Height);
        }
    }


}
