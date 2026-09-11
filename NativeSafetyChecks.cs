using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;

// Hardware-free: never constructs KrakenNativeDevice, enumerates devices, or opens USB/HID.
internal static class NativeSafetyChecks
{
    public static int Run()
    {
        var checks = 0;
        void Check(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
            checks++;
        }

        foreach (var channel in new byte[] { 0x01, 0x02 })
        foreach (var duty in new[] { int.MinValue, 0, 19, 20, 80, 100, int.MaxValue })
        {
            var profile = KrakenNativeDevice.CreateFixedDutyProfile(channel, duty);
            var header = new byte[] { 0x72, channel, 0x01, channel == 0x02 ? (byte)0x01 : (byte)0x00 };
            var clampedDuty = Math.Clamp(duty, channel == 0x01 ? 20 : 0, 100);
            Check(profile.Length == 44 && profile.Take(4).SequenceEqual(header) &&
                  profile.Skip(4).Take(39).All(value => value == clampedDuty) && profile[43] == 100,
                $"Invalid 40-point cooling profile for channel {channel}, duty {duty}.");
        }
        foreach (var channel in new byte[] { 0x00, 0x03, 0xFF })
            Check(Throws<ArgumentOutOfRangeException>(() => KrakenNativeDevice.CreateFixedDutyProfile(channel, 100)),
                $"Unsupported cooling channel {channel} was accepted.");

        foreach (var length in new[] { 0, 1, 25 })
        {
            Check(Throws<InvalidDataException>(() => KrakenNativeDevice.ParseStatusReport(new byte[length])),
                "Short status report was not rejected.");
        }
        var statusReport = new byte[26];
        statusReport[0] = 0x75; statusReport[1] = 0x01;
        statusReport[15] = 55; statusReport[16] = 3;
        statusReport[17] = 0xD2; statusReport[18] = 0x04; statusReport[19] = 65;
        statusReport[23] = 0x29; statusReport[24] = 0x09; statusReport[25] = 44;
        var status = KrakenNativeDevice.ParseStatusReport(statusReport);
        Check(status.LiquidTempC == 55.3f && status.PumpRpm == 1234 && status.PumpDuty == 65 &&
              status.FanRpm == 2345 && status.FanDuty == 44, "Status decoding changed.");
        statusReport[0] = 0;
        Check(Throws<InvalidDataException>(() => KrakenNativeDevice.ParseStatusReport(statusReport)),
            "Unrelated report was accepted as status.");

        var calls = 0;
        var expected = new byte[] { 0x75, 0x01, 42 };
        var matched = KrakenNativeDevice.ReadMatchingReport(_ => ++calls < 3 ? new byte[] { 0 } : expected,
            0x75, 0x01, 5, 2000);
        Check(ReferenceEquals(matched, expected) && calls == 3, "Prefix matching changed.");
        calls = 0;
        Check(Throws<TimeoutException>(() => KrakenNativeDevice.ReadMatchingReport(_ =>
        {
            calls++;
            return Array.Empty<byte>();
        }, 0x75, 0x01, 3, 2000)) && calls == 3, "Prefix attempt limit was not enforced.");
        calls = 0;
        Check(Throws<TimeoutException>(() => KrakenNativeDevice.ReadMatchingReport(_ =>
        {
            calls++;
            throw new TimeoutException("Simulated HID timeout.");
        }, 0x75, 0x01, 50, 2000)) && calls == 1, "Timed-out reads were retried.");
        calls = 0;
        var timeouts = new List<int>();
        Check(Throws<TimeoutException>(() => KrakenNativeDevice.ReadMatchingReport(timeout =>
        {
            calls++;
            timeouts.Add(timeout);
            Thread.Sleep(3);
            return Array.Empty<byte>();
        }, 0x75, 0x01, 1000, 15)) && calls < 1000 &&
            timeouts.All(value => value > 0 && value <= 15) &&
            timeouts.SequenceEqual(timeouts.OrderByDescending(value => value)),
            "Continuous unrelated reports escaped the shared deadline.");

        calls = 0;
        KrakenNativeDevice.DrainReports(() => { calls++; return 64; });
        Check(calls > 0 && calls <= 64, "Drain report limit was not enforced.");
        calls = 0;
        KrakenNativeDevice.DrainReports(() => { calls++; return 0; });
        Check(calls == 1, "Empty drain did not stop.");
        calls = 0;
        KrakenNativeDevice.DrainReports(() => { calls++; throw new TimeoutException(); });
        Check(calls == 1, "Drain timeout did not stop.");
        calls = 0;
        KrakenNativeDevice.DrainReports(() => { calls++; throw new IOException(); });
        Check(calls == 1, "Drain IO failure did not stop.");
        calls = 0;
        KrakenNativeDevice.DrainReports(() => { calls++; Thread.Sleep(3); return 64; }, 1000, 15);
        Check(calls > 0 && calls < 1000, "Continuous drain escaped its deadline.");

        foreach (var size in new[] { new Size(1, 1), new Size(3, 5), new Size(257, 3), new Size(640, 640) })
        {
            using var bitmap = new Bitmap(size.Width, size.Height, PixelFormat.Format24bppRgb);
            for (var y = 0; y < bitmap.Height; y++)
            for (var x = 0; x < bitmap.Width; x++)
                bitmap.SetPixel(x, y, Color.FromArgb((x + y * 17) & 255, (x * 3 + y) & 255, (x * 7 + y * 11) & 255));
            Check(Q565Encoder.Encode(bitmap).SequenceEqual(EncodeReference(bitmap)),
                $"Q565 output changed for {size.Width}x{size.Height} (including padded rows).");
            if (size.Width == 640)
            {
                var oldMilliseconds = MedianMilliseconds(() => EncodeReference(bitmap));
                var newMilliseconds = MedianMilliseconds(() => Q565Encoder.Encode(bitmap));
                Console.WriteLine($"Q565 640x640 median of 3: GetPixel={oldMilliseconds:F3}ms LockBits={newMilliseconds:F3}ms");
            }
        }

        const int stride = 12, negativeHeight = 4;
        var storage = Marshal.AllocHGlobal(stride * negativeHeight);
        try
        {
            var bytes = Enumerable.Range(0, stride * negativeHeight).Select(i => (byte)(i * 5)).ToArray();
            Marshal.Copy(bytes, 0, storage, bytes.Length);
            using var bottomUp = new Bitmap(3, negativeHeight, -stride, PixelFormat.Format24bppRgb,
                IntPtr.Add(storage, stride * (negativeHeight - 1)));
            Check(Q565Encoder.Encode(bottomUp).SequenceEqual(EncodeReference(bottomUp)),
                "Q565 negative-stride output changed.");
        }
        finally
        {
            Marshal.FreeHGlobal(storage);
        }

        using var unsupported = new Bitmap(1, 1, PixelFormat.Format32bppArgb);
        Check(Throws<ArgumentException>(() => Q565Encoder.Encode(unsupported)), "Unsupported pixel format was not rejected.");
        return checks;
    }

    private static bool Throws<T>(Action action) where T : Exception
    {
        try { action(); }
        catch (T) { return true; }
        return false;
    }

    private static double MedianMilliseconds(Func<byte[]> encode)
    {
        var samples = new double[3];
        for (var i = 0; i < samples.Length; i++)
        {
            var elapsed = Stopwatch.StartNew();
            var result = encode();
            samples[i] = elapsed.Elapsed.TotalMilliseconds;
            GC.KeepAlive(result);
        }
        Array.Sort(samples);
        return samples[1];
    }

    private static byte[] EncodeReference(Bitmap bitmap)
    {
        var bytes = new List<byte>(8 + bitmap.Width * bitmap.Height * 3 + 1)
        {
            (byte)'q', (byte)'5', (byte)'6', (byte)'5',
            (byte)bitmap.Width, (byte)(bitmap.Width >> 8), (byte)bitmap.Height, (byte)(bitmap.Height >> 8)
        };
        for (var y = 0; y < bitmap.Height; y++)
        for (var x = 0; x < bitmap.Width; x++)
        {
            var color = bitmap.GetPixel(x, y);
            var red = (color.R * 249 + 1014) >> 11;
            var green = (color.G * 253 + 505) >> 10;
            var blue = (color.B * 249 + 1014) >> 11;
            var rgb565 = (ushort)((red << 11) | (green << 5) | blue);
            bytes.Add(0xFE); bytes.Add((byte)rgb565); bytes.Add((byte)(rgb565 >> 8));
        }
        bytes.Add(0xFF);
        return bytes.ToArray();
    }
}
