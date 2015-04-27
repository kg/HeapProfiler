/*
The contents of this file are subject to the Mozilla Public License
Version 1.1 (the "License"); you may not use this file except in
compliance with the License. You may obtain a copy of the License at
http://www.mozilla.org/MPL/

Software distributed under the License is distributed on an "AS IS"
basis, WITHOUT WARRANTY OF ANY KIND, either express or implied. See the
License for the specific language governing rights and limitations
under the License.

The Original Code is Fury².

The Initial Developer of the Original Code is K. Gadd.

Original Author: K. Gadd (kg@luminance.org)
*/

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Drawing;

namespace HeapProfiler {
    public static class HSV {
        public const UInt16 HueUnit = 600;
        public const UInt16 HueMin = 0, HueMax = (HueUnit * 6) - 1;
        public const UInt16 SaturationMin = 0, SaturationMax = 2550;
        public const UInt16 ValueMin = 0, ValueMax = 2550;

        static byte ClampByte (int value) {
            if (value < 0)
                return 0;
            else if (value > 255)
                return 255;
            else
                return (byte)value;
        }

        public static Color ColorFromHSV (UInt16 hue, UInt16 saturation, UInt16 value) {
            return ColorFromHSVA(hue, saturation, value, 255);
        }

        public static Color ColorFromHSVA (UInt16 hue, UInt16 saturation, UInt16 value, byte alpha) {
            if (value <= ValueMin)
                return Color.FromArgb(alpha, 0, 0, 0);
            if ((value >= ValueMax) && (saturation <= SaturationMin))
                return Color.FromArgb(alpha, 255, 255, 255);
            if (value > ValueMax)
                value = ValueMax;
            if (saturation > SaturationMax)
                saturation = SaturationMax;

            int range = value * 255 / ValueMax;
            if (saturation <= SaturationMin)
                return Color.FromArgb(alpha, range, range, range);

            int segment = hue / HueUnit;
            int remainder = hue - (segment * HueUnit);
            int colorRange = (saturation) * range / SaturationMax;

            int c = (SaturationMax - saturation) * range / SaturationMax;
            int b = (remainder * colorRange) / HueUnit + c;
            int rb = colorRange - b + (c * 2);
            int a = colorRange + c;

            switch (segment) {
                case 0:
                    return Color.FromArgb(alpha, ClampByte(a), ClampByte(b), ClampByte(c));
                case 1:
                    return Color.FromArgb(alpha, ClampByte(rb), ClampByte(a), ClampByte(c));
                case 2:
                    return Color.FromArgb(alpha, ClampByte(c), ClampByte(a), ClampByte(b));
                case 3:
                    return Color.FromArgb(alpha, ClampByte(c), ClampByte(rb), ClampByte(a));
                case 4:
                    return Color.FromArgb(alpha, ClampByte(b), ClampByte(c), ClampByte(a));
                case 5:
                    return Color.FromArgb(alpha, ClampByte(a), ClampByte(c), ClampByte(rb));
                default:
                    throw new ArgumentException("Invalid color");
            }
        }

        public static void GetHSV (this Color color, out UInt16 hue, out UInt16 saturation, out UInt16 value) {
            byte alpha;
            GetHSVA(color, out hue, out saturation, out value, out alpha);
        }

        public static void GetHSVA (this Color color, out UInt16 hue, out UInt16 saturation, out UInt16 value, out byte alpha) {
            alpha = color.A;

            int min = Math.Min(color.R, Math.Min(color.G, color.B));
            int max = Math.Max(color.R, Math.Max(color.G, color.B));

            if (max == min) {
                hue = HueMin;
                saturation = SaturationMin;
                value = (UInt16)(min * ValueMax / 255);
                return;
            } else if (max == 0) {
                hue = HueMin;
                saturation = SaturationMin;
                value = ValueMin;
                return;
            }

            int b = max - min, d;
            value = (UInt16)(max * ValueMax / 255);
            saturation = (UInt16)(b * SaturationMax / max);

            if (color.R == max) {
                d = color.G - color.B;
                hue = (UInt16)(d * HueUnit / b);
            } else if (color.G == max) {
                d = color.B - color.R;
                hue = (UInt16)((d * HueUnit / b) + (2 * HueUnit));
            } else /* if (color.B == max) */ {
                d = color.R - color.G;
                hue = (UInt16)((d * HueUnit / b) + (4 * HueUnit));
            }
        }
    }
}
