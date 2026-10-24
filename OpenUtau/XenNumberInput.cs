using System;
using System.Globalization;

namespace OpenUtau.App {
    /// <summary>Accepts the selected UI culture's decimals and invariant file-style decimals, never grouping.</summary>
    public static class XenNumberInput {
        public static double Parse(string text, CultureInfo culture) {
            if (double.TryParse(text.Trim(), NumberStyles.Float, culture, out double value)
                || double.TryParse(text.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out value)) {
                if (double.IsFinite(value)) return value;
            }
            throw new FormatException("Enter a finite decimal number without digit grouping.");
        }
    }
}
