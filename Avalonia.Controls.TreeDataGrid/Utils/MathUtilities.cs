using System;

// Avalonia 12 made Avalonia.Utilities.MathUtilities internal (it was previously accessible to
// TreeDataGrid via InternalsVisibleTo when TreeDataGrid was a first-party Avalonia project).
// This is a same-namespace/same-name local replacement covering just what TreeDataGrid uses,
// copied verbatim from Avalonia's own (still public on GitHub) source for behavioral parity.
// See NOTICE.md, "Local fixes applied", for the Avalonia 12 port this is part of.
namespace Avalonia.Utilities
{
    internal static class MathUtilities
    {
        internal const double DoubleEpsilon = 2.2204460492503131e-016;

        public static bool AreClose(double value1, double value2)
        {
            if (value1 == value2) return true;
            double eps = (Math.Abs(value1) + Math.Abs(value2) + 10.0) * DoubleEpsilon;
            double delta = value1 - value2;
            return (-eps < delta) && (eps > delta);
        }

        public static bool GreaterThan(double value1, double value2)
        {
            return (value1 > value2) && !AreClose(value1, value2);
        }

        public static bool IsZero(double value)
        {
            return Math.Abs(value) < 10.0 * DoubleEpsilon;
        }
    }
}
