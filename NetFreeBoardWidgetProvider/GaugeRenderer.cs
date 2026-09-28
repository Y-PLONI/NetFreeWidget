using System;
using System.IO;
using System.IO.Compression;

namespace NetFreeBoardWidgetProvider
{
    /// <summary>
    /// Draws the dashboard-style usage gauge as a PNG data URI. Pure math per pixel (signed distances with 1px
    /// anti-aliasing) streamed row by row into the PNG encoder, so no graphics library or bitmap buffer is needed.
    /// Colors are chosen to read on both the light and the dark board theme; the background is transparent.
    /// </summary>
    internal static class GaugeRenderer
    {
        // Rendered at ~3x the displayed size so it stays crisp on high-DPI screens.
        private const int W = 600, H = 400;
        private const double Cx = 300, Cy = 292;
        private const double R = 240;          // arc centerline radius
        private const double Stroke = 30;
        private const double StartDeg = 200, SweepDeg = 220;

        private static readonly (double at, double r, double g, double b)[] Stops =
        {
            (0.00, 0.13, 0.77, 0.37), // green
            (0.55, 0.92, 0.70, 0.03), // amber
            (0.80, 0.98, 0.45, 0.09), // orange
            (1.00, 0.94, 0.27, 0.27), // red
        };

        private static readonly object Gate = new();
        private static string? _cacheKey;
        private static string _cacheUri = "";

        /// <param name="used">Used fraction of the package (NaN when unknown); above 1 pins the needle.</param>
        /// <param name="expectedLo">Lower end of the on-track fraction for today (NaN to hide the marker).</param>
        /// <param name="expectedHi">Upper end; equal to <paramref name="expectedLo"/> when the reset day is exact.</param>
        public static string Render(double used, double expectedLo, double expectedHi)
        {
            used = double.IsNaN(used) ? double.NaN : Math.Clamp(used, 0, 1);
            expectedLo = double.IsNaN(expectedLo) ? double.NaN : Math.Clamp(expectedLo, 0, 1);
            expectedHi = double.IsNaN(expectedHi) ? expectedLo : Math.Clamp(expectedHi, 0, 1);

            // Rounded to what is visible, so small changes reuse the last image.
            string key = $"{Round(used)}|{Round(expectedLo)}|{Round(expectedHi)}";
            lock (Gate)
            {
                if (key == _cacheKey)
                    return _cacheUri;

                _cacheUri = "data:image/png;base64," + Convert.ToBase64String(EncodePng(used, expectedLo, expectedHi));
                _cacheKey = key;
                return _cacheUri;
            }
        }

        private static string Round(double v) => double.IsNaN(v) ? "-" : Math.Round(v * 400).ToString();

        private static byte[] EncodePng(double used, double expLo, double expHi)
        {
            using var png = new MemoryStream(32 * 1024);
            png.Write(new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A });

            var ihdr = new byte[13];
            WriteBE(ihdr, 0, W);
            WriteBE(ihdr, 4, H);
            ihdr[8] = 8;  // bit depth
            ihdr[9] = 6;  // RGBA
            WriteChunk(png, "IHDR", ihdr);

            using (var idat = new MemoryStream(32 * 1024))
            {
                using (var z = new ZLibStream(idat, CompressionLevel.Optimal, leaveOpen: true))
                {
                    var row = new byte[1 + W * 4]; // filter byte 0 + pixels
                    var scene = new Scene(used, expLo, expHi);
                    for (int y = 0; y < H; y++)
                    {
                        for (int x = 0; x < W; x++)
                            scene.Shade(x + 0.5, y + 0.5, row, 1 + x * 4);
                        z.Write(row);
                    }
                }
                WriteChunk(png, "IDAT", idat.ToArray());
            }

            WriteChunk(png, "IEND", Array.Empty<byte>());
            return png.ToArray();
        }

        private readonly struct Scene
        {
            private readonly double _used, _expLo, _expHi;
            private readonly double _needleX, _needleY;

            public Scene(double used, double expLo, double expHi)
            {
                _used = used;
                _expLo = expLo;
                _expHi = expHi;
                var (nx, ny) = PointAt(double.IsNaN(used) ? 0 : used, R - 8);
                _needleX = nx;
                _needleY = ny;
            }

            public void Shade(double x, double y, byte[] row, int i)
            {
                double r = 0, g = 0, b = 0, a = 0;
                double v = ValueAt(x, y);
                // In the gap under the gauge, take the color of the nearer end (so the start cap stays green).
                double gapMid = 1 + (360 - SweepDeg) / SweepDeg / 2;
                var (gr, gg, gb) = Gradient(v > gapMid ? 0 : Math.Clamp(v, 0, 1));

                // Dim track showing the color zones of the whole scale.
                Over(ref r, ref g, ref b, ref a, gr, gg, gb, 0.20 * Cover(ArcDistance(x, y, v, R, 0, 1) - Stroke / 2));

                // Filled part up to the current usage.
                if (!double.IsNaN(_used) && _used > 0)
                    Over(ref r, ref g, ref b, ref a, gr, gg, gb, Cover(ArcDistance(x, y, v, R, 0, _used) - Stroke / 2));

                // Tick marks inside the ring, every 10%, longer at 0 / 50 / 100.
                double inner = R - Stroke / 2 - 10;
                for (int t = 0; t <= 10; t++)
                {
                    bool major = t % 5 == 0;
                    var (x0, y0) = PointAt(t / 10.0, inner);
                    var (x1, y1) = PointAt(t / 10.0, inner - (major ? 30 : 16));
                    double d = SegmentDistance(x, y, x0, y0, x1, y1, out _) - (major ? 3.5 : 2.2);
                    Over(ref r, ref g, ref b, ref a, 0.55, 0.55, 0.58, 0.85 * Cover(d));
                }

                // On-track zone for today, just outside the ring (a band when the reset day is uncertain).
                if (!double.IsNaN(_expLo))
                {
                    double lo = Math.Max(0, _expLo - 0.012), hi = Math.Min(1, _expHi + 0.012);
                    Over(ref r, ref g, ref b, ref a, 0.23, 0.59, 0.87, Cover(ArcDistance(x, y, v, R + Stroke / 2 + 14, lo, hi) - 5));
                }

                // Tapered needle and hub.
                double nd = SegmentDistance(x, y, Cx, Cy, _needleX, _needleY, out double t01) - (14 - 10 * t01) / 2;
                double needleAlpha = double.IsNaN(_used) ? 0.35 : 1;
                Over(ref r, ref g, ref b, ref a, 1.00, 0.36, 0.21, needleAlpha * Cover(nd));

                double hub = Math.Sqrt((x - Cx) * (x - Cx) + (y - Cy) * (y - Cy));
                Over(ref r, ref g, ref b, ref a, 1.00, 0.36, 0.21, Cover(hub - 24));
                Over(ref r, ref g, ref b, ref a, 0.22, 0.22, 0.24, Cover(hub - 16));

                row[i] = ToByte(r);
                row[i + 1] = ToByte(g);
                row[i + 2] = ToByte(b);
                row[i + 3] = ToByte(a);
            }
        }

        /// <summary>Position along the scale (0..1 on the arc; outside that range in the gap under the gauge).</summary>
        private static double ValueAt(double x, double y)
        {
            double deg = Math.Atan2(Cy - y, x - Cx) * 180 / Math.PI;
            if (deg > StartDeg) deg -= 360;
            if (deg <= StartDeg - 360) deg += 360;
            return (StartDeg - deg) / SweepDeg;
        }

        private static (double x, double y) PointAt(double value, double radius)
        {
            double rad = (StartDeg - value * SweepDeg) * Math.PI / 180;
            return (Cx + radius * Math.Cos(rad), Cy - radius * Math.Sin(rad));
        }

        /// <summary>Distance to the arc between two scale values, with round caps.</summary>
        private static double ArcDistance(double x, double y, double v, double radius, double from, double to)
        {
            if (v >= from && v <= to)
                return Math.Abs(Math.Sqrt((x - Cx) * (x - Cx) + (y - Cy) * (y - Cy)) - radius);

            var (ax, ay) = PointAt(from, radius);
            var (bx, by) = PointAt(to, radius);
            return Math.Min(Math.Sqrt((x - ax) * (x - ax) + (y - ay) * (y - ay)),
                            Math.Sqrt((x - bx) * (x - bx) + (y - by) * (y - by)));
        }

        private static double SegmentDistance(double x, double y, double ax, double ay, double bx, double by, out double t)
        {
            double dx = bx - ax, dy = by - ay;
            t = Math.Clamp(((x - ax) * dx + (y - ay) * dy) / (dx * dx + dy * dy), 0, 1);
            double px = ax + t * dx - x, py = ay + t * dy - y;
            return Math.Sqrt(px * px + py * py);
        }

        private static (double r, double g, double b) Gradient(double v)
        {
            for (int i = 1; i < Stops.Length; i++)
            {
                if (v <= Stops[i].at)
                {
                    var p = Stops[i - 1];
                    var q = Stops[i];
                    double k = (v - p.at) / (q.at - p.at);
                    return (p.r + (q.r - p.r) * k, p.g + (q.g - p.g) * k, p.b + (q.b - p.b) * k);
                }
            }
            var last = Stops[^1];
            return (last.r, last.g, last.b);
        }

        /// <summary>Coverage of a pixel by a shape at signed distance <paramref name="d"/> (negative = inside).</summary>
        private static double Cover(double d) => Math.Clamp(0.5 - d, 0, 1);

        /// <summary>Source-over compositing in straight (non-premultiplied) alpha.</summary>
        private static void Over(ref double r, ref double g, ref double b, ref double a, double sr, double sg, double sb, double sa)
        {
            if (sa <= 0)
                return;
            double outA = sa + a * (1 - sa);
            r = (sr * sa + r * a * (1 - sa)) / outA;
            g = (sg * sa + g * a * (1 - sa)) / outA;
            b = (sb * sa + b * a * (1 - sa)) / outA;
            a = outA;
        }

        private static byte ToByte(double c) => (byte)Math.Round(Math.Clamp(c, 0, 1) * 255);

        private static void WriteBE(byte[] buf, int offset, int value)
        {
            buf[offset] = (byte)(value >> 24);
            buf[offset + 1] = (byte)(value >> 16);
            buf[offset + 2] = (byte)(value >> 8);
            buf[offset + 3] = (byte)value;
        }

        private static void WriteChunk(Stream s, string type, byte[] data)
        {
            var header = new byte[8];
            WriteBE(header, 0, data.Length);
            for (int i = 0; i < 4; i++)
                header[4 + i] = (byte)type[i];
            s.Write(header);
            s.Write(data);

            uint crc = Crc32(header.AsSpan(4, 4), 0xFFFFFFFF);
            crc = Crc32(data, crc) ^ 0xFFFFFFFF;
            var tail = new byte[4];
            WriteBE(tail, 0, (int)crc);
            s.Write(tail);
        }

        private static uint[]? _crcTable;

        private static uint Crc32(ReadOnlySpan<byte> data, uint crc)
        {
            var table = _crcTable ??= BuildCrcTable();
            foreach (byte x in data)
                crc = table[(crc ^ x) & 0xFF] ^ (crc >> 8);
            return crc;
        }

        private static uint[] BuildCrcTable()
        {
            var table = new uint[256];
            for (uint n = 0; n < 256; n++)
            {
                uint c = n;
                for (int k = 0; k < 8; k++)
                    c = (c & 1) != 0 ? 0xEDB88320 ^ (c >> 1) : c >> 1;
                table[n] = c;
            }
            return table;
        }
    }
}
