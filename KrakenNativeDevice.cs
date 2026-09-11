using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.Globalization;
using System.Runtime.InteropServices;
using Microsoft.Win32;
using Microsoft.Win32.SafeHandles;
using HidSharp;

internal sealed record KrakenDeviceStatus(
    float? LiquidTempC,
    int? PumpRpm,
    int? PumpDuty,
    int? FanRpm,
    int? FanDuty);

internal sealed class KrakenNativeDevice : IDisposable
{
    private const int VendorId = 0x1E71;
    private const int ProductIdKrakenElite = 0x300C;
    private const int InputReportLength = 64;
    private const int OutputReportPayloadLength = 64;
    private const int BulkEndpoint = 0x02;
    private const int LcdWidth = 640;
    private const int LcdHeight = 640;

    private readonly HidDevice _hidDevice;
    private readonly HidStream _hidStream;
    private WinUsbBulkPipe? _bulkPipe;
    private readonly int _hidWriteLength;
    private readonly int _hidReadLength;
    private readonly int _hidWriteOffset;
    private int _brightness = 100;
    private int _orientationQuarterTurns = 1;
    private static readonly byte[] CommonWriteHeader = new byte[]
    {
        0x12, 0xFA, 0x01, 0xE8,
        0xAB, 0xCD, 0xEF, 0x98,
        0x76, 0x54, 0x32, 0x10
    };

    public KrakenNativeDevice()
    {
        _hidDevice = DeviceList.Local
            .GetHidDevices(VendorId, ProductIdKrakenElite)
            .FirstOrDefault(device => device.DevicePath?.Contains("mi_01", StringComparison.OrdinalIgnoreCase) == true)
            ?? throw new InvalidOperationException("NZXT Kraken Elite HID interface was not found.");

        if (!_hidDevice.TryOpen(out _hidStream))
        {
            throw new InvalidOperationException("NZXT Kraken Elite HID interface could not be opened.");
        }

        try
        {
            _hidStream.ReadTimeout = 2000;
            _hidStream.WriteTimeout = 5000;
            _hidWriteLength = Math.Max(_hidDevice.GetMaxOutputReportLength(), OutputReportPayloadLength);
            _hidReadLength = Math.Max(_hidDevice.GetMaxInputReportLength(), InputReportLength);
            _hidWriteOffset = _hidWriteLength > OutputReportPayloadLength ? 1 : 0;
        }
        catch
        {
            _hidStream.Dispose();
            throw;
        }
    }

    public void Initialize(int brightness, int rotationDegrees)
    {
        DrainReports();
        WriteCommand(0x36, 0x03);
        ConfigureLcd(brightness, rotationDegrees);
        SetLcdMode(0x02, 0x00);
        Thread.Sleep(100);
        SetLcdMode(0x04, 0x00);
    }

    public void ReinitializeLcd()
    {
        DrainReports();
        WriteCommand(0x36, 0x03);
        ConfigureLcd(_brightness, _orientationQuarterTurns * 90);
        SetLcdMode(0x02, 0x00);
        Thread.Sleep(100);
        SetLcdMode(0x04, 0x00);
    }

    public KrakenDeviceStatus ReadStatus()
    {
        DrainReports();
        WriteCommand(0x74, 0x01);
        return ParseStatusReport(ReadUntilPrefix(0x75, 0x01, 50));
    }

    internal static KrakenDeviceStatus ParseStatusReport(byte[] msg)
    {
        if (msg.Length < 26 || msg[0] != 0x75 || msg[1] != 0x01)
        {
            throw new InvalidDataException($"Invalid Kraken status report ({msg.Length} bytes).");
        }

        return new KrakenDeviceStatus(
            LiquidTempC: msg[15] + msg[16] / 10f,
            PumpRpm: (msg[18] << 8) | msg[17],
            PumpDuty: msg[19],
            FanRpm: (msg[24] << 8) | msg[23],
            FanDuty: msg[25]);
    }

    public void ApplyFixedDuties(int pumpDuty, int fanDuty)
    {
        WriteCommand(CreateFixedDutyProfile(0x01, pumpDuty));
        WriteCommand(CreateFixedDutyProfile(0x02, fanDuty));
    }

    public void ConfigureLcd(int brightness, int rotationDegrees)
    {
        brightness = Math.Clamp(brightness, 0, 100);
        var orientation = NormalizeQuarterTurns(rotationDegrees);
        WriteCommand(0x30, 0x02, 0x01, (byte)brightness, 0x00, 0x00, 0x01, 0x03);
        _brightness = brightness;
        _orientationQuarterTurns = orientation;
    }

    public void PushStaticImage(string imagePath)
    {
        var data = PrepareStaticImagePayload(imagePath, _orientationQuarterTurns);
        SendStaticImage(data);
    }

    public void Dispose()
    {
        _hidStream.Dispose();
        _bulkPipe?.Dispose();
    }

    private void SendStaticImage(byte[] data)
    {
        // Cooling only needs HID; discover/open the LCD interface only for an upload.
        var bulkPipe = EnsureBulkPipe();
        DrainReports();
        WriteCommand(0x36, 0x01, 0x00, 0x01, 0x08);
        var response = ReadUntilPrefix(0x37, 0x01, 50);
        if (!StandardOk(response))
        {
            throw new InvalidOperationException("Kraken LCD did not acknowledge frame upload start.");
        }

        var header = CommonWriteHeader
            .Concat(new byte[] { 0x08, 0x00, 0x00, 0x00 })
            .Concat(BitConverter.GetBytes(data.Length))
            .ToArray();

        bulkPipe.Write(header);
        bulkPipe.Write(data);
        Thread.Sleep(50);
        WriteCommand(0x36, 0x02);
        response = ReadUntilPrefix(0x37, 0x02, 50);
        if (!StandardOk(response))
        {
            throw new InvalidOperationException("Kraken LCD did not acknowledge frame upload finish.");
        }
    }

    internal static byte[] CreateFixedDutyProfile(byte channel, int duty)
    {
        if (channel is not (0x01 or 0x02))
            throw new ArgumentOutOfRangeException(nameof(channel));

        duty = Math.Clamp(duty, channel == 0x01 ? 20 : 0, 100);
        // The device stores 40 points for coolant temperatures 20-59 C.
        var payload = new byte[4 + 40];
        payload[0] = 0x72;
        payload[1] = channel;
        payload[2] = 0x01;
        payload[3] = channel == 0x02 ? (byte)0x01 : (byte)0x00;
        Array.Fill(payload, (byte)duty, 4, 39);
        payload[^1] = 100; // Critical-temperature endpoint must never reduce cooling.
        return payload;
    }

    private byte[] PrepareStaticImagePayload(string imagePath, int orientationQuarterTurns)
    {
        using var source = new Bitmap(imagePath);
        using var bitmap = new Bitmap(LcdWidth, LcdHeight, PixelFormat.Format24bppRgb);
        using (var graphics = Graphics.FromImage(bitmap))
        {
            graphics.Clear(Color.Black);
            graphics.DrawImage(source, 0, 0, LcdWidth, LcdHeight);
        }

        bitmap.RotateFlip(orientationQuarterTurns switch
        {
            1 => RotateFlipType.Rotate270FlipNone,
            2 => RotateFlipType.Rotate180FlipNone,
            3 => RotateFlipType.Rotate90FlipNone,
            _ => RotateFlipType.RotateNoneFlipNone,
        });

        return Q565Encoder.Encode(bitmap);
    }

    private void WriteCommand(params byte[] payload)
    {
        var buffer = new byte[_hidWriteLength];
        if (_hidWriteOffset == 1)
        {
            buffer[0] = 0x00;
        }

        Array.Copy(payload, 0, buffer, _hidWriteOffset, Math.Min(payload.Length, buffer.Length - _hidWriteOffset));
        _hidStream.Write(buffer);
    }

    private byte[] WriteThenRead(params byte[] payload)
    {
        WriteCommand(payload);
        return ReadReport();
    }

    private byte[] ReadUntilPrefix(byte first, byte second, int maxAttempts = 12)
    {
        var originalTimeout = _hidStream.ReadTimeout;
        try
        {
            return ReadMatchingReport(timeout =>
            {
                _hidStream.ReadTimeout = timeout;
                return ReadReport();
            }, first, second, maxAttempts, 2000);
        }
        finally
        {
            _hidStream.ReadTimeout = originalTimeout;
        }
    }

    internal static byte[] ReadMatchingReport(Func<int, byte[]> readReport, byte first, byte second,
        int maxAttempts, int timeoutMilliseconds)
    {
        var elapsed = Stopwatch.StartNew();
        for (var attempt = 0; attempt < maxAttempts; attempt++)
        {
            var remaining = timeoutMilliseconds - (int)elapsed.ElapsedMilliseconds;
            if (remaining <= 0)
            {
                break;
            }

            var msg = readReport(remaining);
            if (msg.Length >= 2 && msg[0] == first && msg[1] == second)
            {
                return msg;
            }
        }

        throw new TimeoutException($"Timed out waiting for Kraken report {first:X2} {second:X2}.");
    }

    private byte[] ReadReport()
    {
        var buffer = new byte[_hidReadLength];
        var bytesRead = _hidStream.Read(buffer, 0, buffer.Length);
        if (bytesRead <= 0)
        {
            throw new InvalidOperationException("Kraken HID read returned no data.");
        }

        if (bytesRead > InputReportLength && buffer[0] == 0x00)
        {
            return buffer.Skip(1).Take(bytesRead - 1).ToArray();
        }

        return buffer.Take(bytesRead).ToArray();
    }

    private void DrainReports()
    {
        var originalTimeout = _hidStream.ReadTimeout;
        try
        {
            _hidStream.ReadTimeout = 1;
            var buffer = new byte[_hidReadLength];
            DrainReports(() => _hidStream.Read(buffer, 0, buffer.Length));
        }
        finally
        {
            _hidStream.ReadTimeout = originalTimeout;
        }
    }

    internal static void DrainReports(Func<int> readReport, int maxReports = 64, int timeoutMilliseconds = 50)
    {
        var elapsed = Stopwatch.StartNew();
        for (var count = 0; count < maxReports && elapsed.ElapsedMilliseconds < timeoutMilliseconds; count++)
        {
            try
            {
                if (readReport() <= 0)
                {
                    break;
                }
            }
            catch (TimeoutException)
            {
                break;
            }
            catch (IOException)
            {
                break;
            }
        }
    }

    private static int NormalizeQuarterTurns(int rotationDegrees)
    {
        var normalized = ((rotationDegrees % 360) + 360) % 360;
        return normalized / 90;
    }

    private static string? FindBulkInterfacePath(int productId)
    {
        const string registryRoot = @"SYSTEM\CurrentControlSet\Control\DeviceClasses\{dee824ef-729b-4a0e-9c14-b7117d33a817}";
        var needle = string.Format(CultureInfo.InvariantCulture, "vid_{0:x4}&pid_{1:x4}&mi_00", VendorId, productId);

        using var root = Registry.LocalMachine.OpenSubKey(registryRoot);
        if (root is null)
        {
            return null;
        }

        foreach (var subkeyName in root.GetSubKeyNames())
        {
            if (!subkeyName.Contains(needle, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (!subkeyName.EndsWith("}", StringComparison.Ordinal))
            {
                continue;
            }

            return subkeyName.Replace("##?#", @"\\?\");
        }

        return null;
    }

    private WinUsbBulkPipe EnsureBulkPipe()
    {
        _bulkPipe ??= new WinUsbBulkPipe(
            FindBulkInterfacePath(ProductIdKrakenElite)
                ?? throw new InvalidOperationException("NZXT Kraken Elite WinUSB interface was not found."),
            BulkEndpoint);
        return _bulkPipe;
    }

    private bool SetLcdMode(byte mode, byte bucket)
    {
        DrainReports();
        WriteCommand(0x38, 0x01, mode, bucket);
        var response = ReadUntilPrefix(0x39, 0x01, 50);
        return StandardOk(response);
    }

    private static bool StandardOk(byte[] packet)
    {
        return packet.Length > 14 && packet[14] == 0x01;
    }
}

internal static class Q565Encoder
{
    private const int Q565Magic = ('q' << 24) | ('5' << 16) | ('6' << 8) | '5';
    private const byte Q565OpRgb565 = 0b1111_1110;
    private const byte Q565OpEnd = 0b1111_1111;

    public static byte[] Encode(Bitmap bitmap)
    {
        if (bitmap.PixelFormat != PixelFormat.Format24bppRgb)
        {
            throw new ArgumentException("Q565 encoding requires a 24-bit RGB bitmap.", nameof(bitmap));
        }

        var width = bitmap.Width;
        var height = bitmap.Height;
        if (width > ushort.MaxValue || height > ushort.MaxValue)
        {
            throw new ArgumentOutOfRangeException(nameof(bitmap), "Q565 dimensions must fit in 16 bits.");
        }
        var writer = new ByteListWriter(checked(8 + (width * height * 3) + 1));

        Write32(writer, Q565Magic);
        Write16(writer, (ushort)width);
        Write16(writer, (ushort)height);

        var pixels = bitmap.LockBits(new Rectangle(0, 0, width, height), ImageLockMode.ReadOnly, PixelFormat.Format24bppRgb);
        try
        {
            var row = new byte[checked(width * 3)];
            for (var y = 0; y < height; y++)
            {
                // Scan0 is the first logical row; signed stride also handles bottom-up storage.
                Marshal.Copy(IntPtr.Add(pixels.Scan0, checked(y * pixels.Stride)), row, 0, row.Length);
                for (var x = 0; x < row.Length; x += 3)
                {
                    var current = ToRgb565(row[x + 2], row[x + 1], row[x]);
                    writer.Write(Q565OpRgb565);
                    writer.Write((byte)(current & 0xFF));
                    writer.Write((byte)(current >> 8));
                }
            }
        }
        finally
        {
            bitmap.UnlockBits(pixels);
        }

        writer.Write(Q565OpEnd);
        return writer.ToArray();
    }

    private static void Write32(ByteListWriter writer, int value)
    {
        writer.Write((byte)((value >> 24) & 0xFF));
        writer.Write((byte)((value >> 16) & 0xFF));
        writer.Write((byte)((value >> 8) & 0xFF));
        writer.Write((byte)(value & 0xFF));
    }

    private static void Write16(ByteListWriter writer, ushort value)
    {
        writer.Write((byte)(value & 0xFF));
        writer.Write((byte)((value >> 8) & 0xFF));
    }

    private static ushort ToRgb565(byte red, byte green, byte blue)
    {
        var r = (red * 249 + 1014) >> 11;
        var g = (green * 253 + 505) >> 10;
        var b = (blue * 249 + 1014) >> 11;
        return (ushort)((r << 11) | (g << 5) | b);
    }

}

internal sealed class ByteListWriter
{
    private readonly List<byte> _buffer;

    public ByteListWriter(int capacity)
    {
        _buffer = new List<byte>(capacity);
    }

    public void Write(byte value) => _buffer.Add(value);

    public byte[] ToArray() => _buffer.ToArray();
}

internal sealed class WinUsbBulkPipe : IDisposable
{
    private readonly SafeFileHandle _deviceHandle;
    private IntPtr _winUsbHandle;
    private readonly byte _pipeId;

    public WinUsbBulkPipe(string devicePath, byte pipeId)
    {
        _pipeId = pipeId;
        _deviceHandle = NativeMethods.CreateFile(
            devicePath,
            NativeMethods.GenericRead | NativeMethods.GenericWrite,
            NativeMethods.FileShareRead | NativeMethods.FileShareWrite,
            IntPtr.Zero,
            NativeMethods.OpenExisting,
            NativeMethods.FileAttributeNormal | NativeMethods.FileFlagOverlapped,
            IntPtr.Zero);

        try
        {
            if (_deviceHandle.IsInvalid)
            {
                throw new InvalidOperationException($"Failed to open Kraken WinUSB device. Win32={Marshal.GetLastWin32Error()} Path={devicePath}");
            }

            if (!NativeMethods.WinUsb_Initialize(_deviceHandle, out _winUsbHandle))
            {
                throw new InvalidOperationException($"Failed to initialize WinUSB for Kraken LCD. Win32={Marshal.GetLastWin32Error()} Path={devicePath}");
            }

            uint timeoutMilliseconds = 2000;
            if (!NativeMethods.WinUsb_SetPipePolicy(_winUsbHandle, _pipeId,
                    NativeMethods.PipeTransferTimeout, sizeof(uint), ref timeoutMilliseconds))
            {
                throw new InvalidOperationException($"Failed to set Kraken LCD transfer timeout. Win32={Marshal.GetLastWin32Error()}");
            }
        }
        catch
        {
            Dispose();
            throw;
        }
    }

    public void Write(byte[] buffer)
    {
        if (!NativeMethods.WinUsb_WritePipe(_winUsbHandle, _pipeId, buffer, buffer.Length, out var transferred, IntPtr.Zero))
        {
            throw new InvalidOperationException($"WinUSB bulk write to Kraken LCD failed. Win32={Marshal.GetLastWin32Error()}");
        }

        if (transferred != buffer.Length)
        {
            throw new InvalidOperationException($"Kraken LCD bulk write incomplete: {transferred}/{buffer.Length} bytes.");
        }
    }

    public void Dispose()
    {
        if (_winUsbHandle != IntPtr.Zero)
        {
            NativeMethods.WinUsb_Free(_winUsbHandle);
            _winUsbHandle = IntPtr.Zero;
        }

        _deviceHandle.Dispose();
    }

    private static class NativeMethods
    {
        public const uint GenericRead = 0x80000000;
        public const uint GenericWrite = 0x40000000;
        public const uint FileShareRead = 0x00000001;
        public const uint FileShareWrite = 0x00000002;
        public const uint OpenExisting = 3;
        public const uint FileAttributeNormal = 0x00000080;
        public const uint FileFlagOverlapped = 0x40000000;
        public const uint PipeTransferTimeout = 0x03;

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        public static extern SafeFileHandle CreateFile(
            string lpFileName,
            uint dwDesiredAccess,
            uint dwShareMode,
            IntPtr lpSecurityAttributes,
            uint dwCreationDisposition,
            uint dwFlagsAndAttributes,
            IntPtr hTemplateFile);

        [DllImport("winusb.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool WinUsb_Initialize(SafeFileHandle deviceHandle, out IntPtr interfaceHandle);

        [DllImport("winusb.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool WinUsb_Free(IntPtr interfaceHandle);

        [DllImport("winusb.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool WinUsb_SetPipePolicy(
            IntPtr interfaceHandle,
            byte pipeId,
            uint policyType,
            uint valueLength,
            ref uint value);

        [DllImport("winusb.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool WinUsb_WritePipe(
            IntPtr interfaceHandle,
            byte pipeId,
            byte[] buffer,
            int bufferLength,
            out int lengthTransferred,
            IntPtr overlapped);
    }
}
