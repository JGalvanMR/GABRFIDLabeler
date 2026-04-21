using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Drawing;

namespace GABRFIDLabeler
{
    public class RfidSafeLayout
    {
        public static Rectangle ChipZone = new Rectangle(328, 66, 80, 60);

        public static bool Intersects(Rectangle element)
        {
            return element.IntersectsWith(ChipZone);
        }

        public static Rectangle MoveOutside(Rectangle element)
        {
            if (!Intersects(element)) return element;

            element.Y = ChipZone.Bottom + 10;
            return element;
        }
    }

}

