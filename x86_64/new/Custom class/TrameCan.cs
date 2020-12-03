using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace PCANBasicExample.Custom_class
{
    class TrameCan
    {
        public int mode;
        public int color;
        public int position;
        public int unit;
        public int weight;

        public TrameCan(String receivedData)
        {
            int integerizedTrame = Convert.ToInt32(receivedData);

            mode = integerizedTrame >> 13;
            color = (integerizedTrame >> 11) & 0x03;
            position = (integerizedTrame >> 9) & 0x03;
            unit = (integerizedTrame >> 8) & 0x01;
            weight = (integerizedTrame & 0x00ff);
        }

        override
        public String ToString()
        {
            String returnValue = "";

            returnValue = ((mode << 13) + (color << 11) + (position << 9) + (unit << 8) + (weight)).ToString();

            return returnValue;
        }

       // public TrameCanOut(String OutDa)
    }
}
