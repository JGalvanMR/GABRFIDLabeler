using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Drawing;

namespace GABRFIDLabeler
{
    public class ZplRfidBuilder
    {
        public static string Build(string text, string epc, string date, string name)
        {
            var nameBox = new Rectangle(40, 30, 300, 90);
            nameBox = RfidSafeLayout.MoveOutside(nameBox);

            return $@"
^XA
^PW736
^LL228
^LH0,0
^PON
^MNY
~SD30

^RS,10,500,1,E^FS
^RZ00000000,P^FS
^RFW,H,1,4,3^FDC3494D32^FS
^RFW,H,1,12,1^FD{epc}^FS
^RLE,P^FS

^FO30,20^BQN,2,7^FDLA,{text}^FS
^FO{nameBox.X},{nameBox.Y}^A0N,50,50^FD{name}^FS
^FO350,140^A0N,45,45^FD{date}^FS

^XZ";
        }
    }

}

