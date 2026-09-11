using System.Drawing;
using System.Drawing.Imaging;
using System.Globalization;
using System.Runtime.InteropServices;
using System.ServiceProcess;
using System.Text;
using System.Text.Json;
using System.Threading;
using LibreHardwareMonitor.Hardware;

// Hardware-free checks must run before directories, mutexes, telemetry or USB.
if (args.Contains("--self-test", StringComparer.OrdinalIgnoreCase))
{
    Environment.ExitCode = SafetyChecks.Run();
    return;
}

var options = AppOptions.Parse(args);
var appRoot = AppContext.BaseDirectory;
var runtimeRoot = Path.Combine(appRoot, "runtime");
var logsRoot = Path.Combine(appRoot, "logs");
var statePath = Path.Combine(runtimeRoot, "state.json");
var imagePath = Path.Combine(runtimeRoot, "kraken_lcd.png");
var configPath = Path.Combine(appRoot, "config.json");
var healthPath = Path.Combine(runtimeRoot, "health.json");
var controllerLogPath = Path.Combine(logsRoot, "controller.log");
var supervisorLogPath = Path.Combine(logsRoot, "supervisor.log");

// Unavailable diagnostic storage must not prevent hardware cooling startup.
ControllerFiles.TryEnsureDirectory(runtimeRoot);
ControllerFiles.TryEnsureDirectory(logsRoot);

if (options.SupervisorMode)
{
    var supervisor = new KrakenSupervisor(
        Path.Combine(AppContext.BaseDirectory, "KrakenHost.exe"),
        healthPath,
        supervisorLogPath);
    Environment.ExitCode = supervisor.Run();
    return;
}

if (options.WindowsServiceMode)
{
    ServiceBase.Run(new KrakenSupervisorWindowsService(
        Path.Combine(AppContext.BaseDirectory, "KrakenHost.exe"),
        healthPath,
        supervisorLogPath));
    return;
}

using var controllerMutex = new Mutex(false, @"Global\DrakulaKrakenController");
bool ownsController;
try { ownsController = controllerMutex.WaitOne(0); }
catch (AbandonedMutexException) { ownsController = true; }
if (!ownsController)
{
    throw new InvalidOperationException("Another Kraken controller instance is already running.");
}

if (args.Contains("--verify-cooling-hold", StringComparer.OrdinalIgnoreCase))
{
    if (options.CpuGpuOnly || options.RenderOnly || options.MaxCoolingMode || options.ResetLcdMode || options.Once)
        throw new ArgumentException("Cooling-hold verification must continue into the normal controller.");
    // Retain the controller mutex throughout proof and normal startup. Closing
    // a HID handle does not reset the device or relinquish controller ownership.
    CoolingHoldVerification.Run(Path.Combine(runtimeRoot, "cooling-hold-proof.json"));
}

using var krakenDevice = options.CpuGpuOnly || options.RenderOnly ? null : new KrakenNativeDevice();

// Establish conservative cooling before optional CPU/GPU libraries or LCD work.
// The existing running controller is never touched by a second process: the
// exclusive mutex above is also required for emergency commands.
krakenDevice?.ApplyFixedDuties(100, 100);

if (options.MaxCoolingMode)
{
    if (krakenDevice is null)
    {
        throw new InvalidOperationException("Kraken device is required in max cooling mode.");
    }

    Thread.Sleep(500);
    var status = krakenDevice.ReadStatus();
    if (status.PumpDuty != 100 || status.FanDuty != 100 || status.PumpRpm < 1500 || status.FanRpm < 1000)
        throw new InvalidOperationException("Maximum cooling readback was not confirmed.");
    Console.WriteLine($"pump={status.PumpRpm}rpm fan={status.FanRpm}rpm liquid={status.LiquidTempC:0.0}C");
    return;
}

if (options.ResetLcdMode)
{
    if (krakenDevice is null)
    {
        throw new InvalidOperationException("Kraken device is required in reset mode.");
    }

    var config = ControllerConfig.Load(configPath);
    krakenDevice.Initialize(config.Brightness, config.RotationDegrees);
    return;
}

using var cpuReader = new CpuTelemetryReader();
using var gpuReader = new NvmlGpuTelemetryReader();
using var controllerProcess = System.Diagnostics.Process.GetCurrentProcess();
var processStartedAt = new DateTimeOffset(controllerProcess.StartTime.ToUniversalTime());

WriteBanner(options);
ControllerLog.Write(controllerLogPath, "controller started");

ControllerConfig? lastConfig = null;
int? lastDuty = null;
DateTimeOffset? lastHighDutyAt = null;
DateTimeOffset? lcdReadyAt = null;
DateTimeOffset? lastLcdFailureAt = null;
var lcdInitialized = false;
var lastTelemetryLogAt = DateTimeOffset.MinValue;
string? lastLoggedControlState = null;

do
{
    var config = ControllerConfig.Load(configPath);
    var krakenStatus = krakenDevice?.ReadStatus();
    var snapshot = new TelemetrySnapshot(
        DateTimeOffset.Now,
        ControllerConfig.NormalizeTemperature(cpuReader.TryReadCpuPackageTempC()),
        ControllerConfig.NormalizeTemperature(gpuReader.TryReadGpuTempC()),
        ControllerConfig.NormalizeTemperature(krakenStatus?.LiquidTempC),
        krakenStatus?.PumpRpm,
        krakenStatus?.FanRpm);

    string lcdStatus;
    if (options.RenderOnly)
    {
        KrakenLcdRenderer.RenderPreview(snapshot, imagePath, config.TextOffsetYPx, config.SectionGapPx);
        lcdStatus = "preview";
    }
    else
    {
        var now = DateTimeOffset.Now;
        int? duty = krakenDevice is null ? null : ControllerConfig.ComputeDuty(snapshot, config, lastDuty, lastHighDutyAt, now);
        if (krakenDevice is not null && duty.HasValue && duty != lastDuty)
        {
            krakenDevice.ApplyFixedDuties(duty.Value, duty.Value);
            lastDuty = duty;
            lastHighDutyAt = duty == config.HighDuty ? now : null;
            Thread.Sleep(250);
        }

        try
        {
            if (options.NoLcd)
            {
                lcdStatus = "disabled";
            }
            else if (lastLcdFailureAt.HasValue && now - lastLcdFailureAt.Value < TimeSpan.FromSeconds(10))
            {
                lcdStatus = "failure_backoff";
            }
            else if (krakenDevice is not null && !lcdInitialized)
            {
                krakenDevice.Initialize(config.Brightness, config.RotationDegrees);
                lcdInitialized = true;
                lastConfig = config;
                lcdReadyAt = now.AddSeconds(3);
                lcdStatus = "startup_wait";
            }
            else if (lcdReadyAt.HasValue && now < lcdReadyAt.Value)
            {
                lcdStatus = "startup_wait";
            }
            else
            {
                if (krakenDevice is not null && ConfigAffectsLcd(lastConfig, config))
                {
                    krakenDevice.ConfigureLcd(config.Brightness, config.RotationDegrees);
                    lastConfig = config;
                }
                var lcdResult = KrakenLcdRenderer.TryPush(
                    krakenDevice,
                    snapshot,
                    imagePath,
                    options.MinLcdPushInterval,
                    options.MaxLcdRefreshInterval,
                    config.TextOffsetYPx,
                    config.SectionGapPx);
                lcdStatus = lcdResult.Status;
                lastLcdFailureAt = lcdResult.Success ? null : now;
                if (!lcdResult.Success)
                    ControllerLog.Write(controllerLogPath, "LCD degraded: " + lcdResult.Status);
            }
        }
        catch (Exception ex)
        {
            lcdStatus = "failed";
            lastLcdFailureAt = now;
            ControllerLog.Write(controllerLogPath, "LCD degraded: " + ex.Message);
        }
    }

    StateWriter.Write(statePath, snapshot);
    ControllerHealth.Write(healthPath, new ControllerHealthSnapshot(
        Timestamp: snapshot.Timestamp,
        Status: "ok",
        CpuTempC: snapshot.CpuTempC,
        GpuTempC: snapshot.GpuTempC,
        LiquidTempC: snapshot.LiquidTempC,
        PumpRpm: snapshot.PumpRpm,
        FanRpm: snapshot.FanRpm,
        Duty: lastDuty,
        LcdStatus: lcdStatus,
        Error: "",
        ProcessId: Environment.ProcessId,
        ProcessStartedAt: processStartedAt,
        PumpDuty: krakenStatus?.PumpDuty,
        FanDuty: krakenStatus?.FanDuty));
    var controlState = $"DUTY={lastDuty} LCD={(lastLcdFailureAt.HasValue ? "degraded" : "ok")}";
    if (controlState != lastLoggedControlState || DateTimeOffset.Now - lastTelemetryLogAt >= TimeSpan.FromSeconds(30))
    {
        ControllerLog.Write(controllerLogPath, snapshot.ToConsoleLine() + " " + controlState);
        lastLoggedControlState = controlState;
        lastTelemetryLogAt = DateTimeOffset.Now;
    }
    Console.WriteLine(snapshot.ToConsoleLine() + $" DUTY={(lastDuty?.ToString(CultureInfo.InvariantCulture) ?? "N/A")}% LCD={lcdStatus}");

    if (options.Once)
    {
        break;
    }

    Thread.Sleep(TimeSpan.FromSeconds(Math.Max(1, config.IntervalSeconds)));
}
while (true);

static bool ConfigAffectsLcd(ControllerConfig? previous, ControllerConfig current)
{
    return previous is null ||
           previous.Brightness != current.Brightness ||
           previous.RotationDegrees != current.RotationDegrees;
}

static void WriteBanner(AppOptions options)
{
    Console.WriteLine("KrakenHost");
    Console.WriteLine("  Mode: manual foreground");
    Console.WriteLine("  GPU telemetry: NVML");
    Console.WriteLine("  CPU telemetry: LibreHardwareMonitor");
    Console.WriteLine($"  Kraken device: {(options.CpuGpuOnly ? "disabled" : "native HID/WinUSB")}");
    Console.WriteLine($"  LCD updates: {(options.NoLcd ? "disabled" : "enabled")}");
    Console.WriteLine();
}

internal sealed record AppOptions(
    bool Once,
    int IntervalSeconds,
    bool NoLcd,
    bool RenderOnly,
    bool CpuGpuOnly,
    bool ResetLcdMode,
    bool MaxCoolingMode,
    TimeSpan MinLcdPushInterval,
    TimeSpan MaxLcdRefreshInterval,
    bool SupervisorMode,
    bool WindowsServiceMode)
{
    public static AppOptions Parse(string[] args)
    {
        var once = false;
        var intervalSeconds = 2;
        var noLcd = false;
        var renderOnly = false;
        var cpuGpuOnly = false;
        var resetLcdMode = false;
        var maxCoolingMode = false;
        var minPush = TimeSpan.FromSeconds(2);
        var maxRefresh = TimeSpan.FromSeconds(30);
        var supervisorMode = false;
        var windowsServiceMode = false;

        foreach (var arg in args)
        {
            if (arg.Equals("--once", StringComparison.OrdinalIgnoreCase))
            {
                once = true;
                continue;
            }

            if (arg.Equals("--no-lcd", StringComparison.OrdinalIgnoreCase))
            {
                noLcd = true;
                continue;
            }

            if (arg.Equals("--render-only", StringComparison.OrdinalIgnoreCase))
            {
                renderOnly = true;
                continue;
            }

            if (arg.Equals("--cpu-gpu-only", StringComparison.OrdinalIgnoreCase))
            {
                cpuGpuOnly = true;
                continue;
            }

            if (arg.Equals("--reset-lcd", StringComparison.OrdinalIgnoreCase))
            {
                resetLcdMode = true;
                continue;
            }

            if (arg.Equals("--max-cooling", StringComparison.OrdinalIgnoreCase))
            {
                maxCoolingMode = true;
                continue;
            }

            if (arg.Equals("--supervisor", StringComparison.OrdinalIgnoreCase))
            {
                supervisorMode = true;
                continue;
            }

            if (arg.Equals("--windows-service", StringComparison.OrdinalIgnoreCase))
            {
                windowsServiceMode = true;
                continue;
            }

            if (arg.StartsWith("--interval=", StringComparison.OrdinalIgnoreCase) &&
                int.TryParse(arg.Split('=', 2)[1], out var parsedInterval) &&
                parsedInterval >= 1)
            {
                intervalSeconds = parsedInterval;
                continue;
            }

            if (arg.StartsWith("--min-lcd-push=", StringComparison.OrdinalIgnoreCase) &&
                int.TryParse(arg.Split('=', 2)[1], out var parsedMinPush) &&
                parsedMinPush >= 1)
            {
                minPush = TimeSpan.FromSeconds(parsedMinPush);
                continue;
            }

            if (arg.StartsWith("--max-lcd-refresh=", StringComparison.OrdinalIgnoreCase) &&
                int.TryParse(arg.Split('=', 2)[1], out var parsedMaxRefresh) &&
                parsedMaxRefresh >= 1)
            {
                maxRefresh = TimeSpan.FromSeconds(parsedMaxRefresh);
            }
        }

        return new AppOptions(once, intervalSeconds, noLcd, renderOnly, cpuGpuOnly, resetLcdMode, maxCoolingMode, minPush, maxRefresh, supervisorMode, windowsServiceMode);
    }
}

internal sealed record TelemetrySnapshot(
    DateTimeOffset Timestamp,
    float? CpuTempC,
    float? GpuTempC,
    float? LiquidTempC,
    int? PumpRpm,
    int? FanRpm)
{
    public string ToConsoleLine()
    {
        return $"{Timestamp:yyyy-MM-dd HH:mm:ss} " +
               $"CPU={FormatWhole(CpuTempC)} " +
               $"GPU={FormatWhole(GpuTempC)} " +
               $"LIQ={FormatSingleDecimal(LiquidTempC)} " +
               $"PUMP={FormatInteger(PumpRpm)} " +
               $"FAN={FormatInteger(FanRpm)}";
    }

    public string CpuText => FormatWhole(CpuTempC);
    public string GpuText => FormatWhole(GpuTempC);
    public string LiquidText => FormatSingleDecimal(LiquidTempC);

    private static string FormatWhole(float? value) => value is null ? "N/A" : $"{MathF.Round(value.Value):0} C";
    private static string FormatSingleDecimal(float? value) => value is null ? "N/A" : $"{value.Value:0.0} C";
    private static string FormatInteger(int? value) => value is null ? "N/A" : $"{value.Value}";
}

internal static class StateWriter
{
    public static void Write(string path, TelemetrySnapshot snapshot)
    {
        ControllerFiles.TryWriteJson(path, snapshot);
    }
}

internal sealed class CpuTelemetryReader : IDisposable
{
    private readonly Computer _computer;
    private readonly bool _available;

    public CpuTelemetryReader()
    {
        _computer = new Computer
        {
            IsCpuEnabled = true
        };
        try
        {
            _computer.Open();
            _available = true;
        }
        catch
        {
            // Missing optional telemetry selects full duty; it must not abort
            // the cooling controller that already established safe startup duty.
            try { _computer.Close(); } catch { }
        }
    }

    public float? TryReadCpuPackageTempC()
    {
        if (!_available) return null;
        try
        {
            foreach (var hardware in _computer.Hardware)
            {
                hardware.Update();
                foreach (var sub in hardware.SubHardware)
                {
                    sub.Update();
                }
            }

            var package = _computer.Hardware
                .SelectMany(Flatten)
                .SelectMany(h => h.Sensors)
                .Where(s => s.SensorType == SensorType.Temperature && s.Value.HasValue)
                .OrderByDescending(s => IsPreferredCpuSensorName(s.Name))
                .ThenBy(s => s.Name)
                .FirstOrDefault();

            return package?.Value;
        }
        catch
        {
            return null;
        }
    }

    public void Dispose()
    {
        try { _computer.Close(); } catch { }
    }

    private static IEnumerable<IHardware> Flatten(IHardware hardware)
    {
        yield return hardware;
        foreach (var sub in hardware.SubHardware)
        {
            foreach (var nested in Flatten(sub))
            {
                yield return nested;
            }
        }
    }

    private static int IsPreferredCpuSensorName(string name)
    {
        if (name.Contains("Package", StringComparison.OrdinalIgnoreCase))
        {
            return 3;
        }

        if (name.Contains("CPU", StringComparison.OrdinalIgnoreCase))
        {
            return 2;
        }

        return 1;
    }
}

internal sealed class NvmlGpuTelemetryReader : IDisposable
{
    private IntPtr _device;
    private bool _initialized;

    public NvmlGpuTelemetryReader()
    {
        try
        {
            var result = NvmlNative.nvmlInit_v2();
            if (result != NvmlReturn.Success)
            {
                return;
            }

            _initialized = true;
            result = NvmlNative.nvmlDeviceGetHandleByIndex_v2(0, out _device);
            if (result != NvmlReturn.Success)
            {
                _device = IntPtr.Zero;
            }
        }
        catch
        {
            _initialized = false;
            _device = IntPtr.Zero;
        }
    }

    public float? TryReadGpuTempC()
    {
        if (!_initialized || _device == IntPtr.Zero)
        {
            return null;
        }

        try
        {
            var result = NvmlNative.nvmlDeviceGetTemperature(_device, NvmlTemperatureSensor.Gpu, out var tempC);
            if (result == NvmlReturn.Success && tempC > 0 && tempC < 120)
            {
                return tempC;
            }
        }
        catch
        {
        }

        return null;
    }

    public void Dispose()
    {
        if (!_initialized)
        {
            return;
        }

        try
        {
            NvmlNative.nvmlShutdown();
        }
        catch
        {
        }
    }
}

internal static class KrakenLcdRenderer
{
    private static TelemetrySnapshot? _lastPushedSnapshot;
    private static DateTimeOffset _lastPushedAt;

    public static void RenderPreview(TelemetrySnapshot snapshot, string outputPath, int textOffsetYPx, int sectionGapPx)
    {
        Render(snapshot, outputPath, textOffsetYPx, sectionGapPx);
    }

    public static LcdPushResult TryPush(KrakenNativeDevice? krakenDevice, TelemetrySnapshot snapshot, string imagePath, TimeSpan minInterval, TimeSpan maxRefreshInterval, int textOffsetYPx, int sectionGapPx)
    {
        if (krakenDevice is null)
        {
            return LcdPushResult.Skipped("disabled");
        }

        var valuesChanged = _lastPushedSnapshot is null ||
            _lastPushedSnapshot.CpuText != snapshot.CpuText ||
            _lastPushedSnapshot.GpuText != snapshot.GpuText ||
            _lastPushedSnapshot.LiquidText != snapshot.LiquidText ||
            _lastPushedSnapshot.PumpRpm != snapshot.PumpRpm ||
            _lastPushedSnapshot.FanRpm != snapshot.FanRpm;
        var refreshExpired = _lastPushedAt == default || DateTimeOffset.Now - _lastPushedAt >= maxRefreshInterval;

        if (!valuesChanged && !refreshExpired)
        {
            return LcdPushResult.Skipped("unchanged");
        }

        if (_lastPushedAt != default && DateTimeOffset.Now - _lastPushedAt < minInterval)
        {
            return LcdPushResult.Skipped("min_interval");
        }

        try
        {
            Render(snapshot, imagePath, textOffsetYPx, sectionGapPx);
            krakenDevice.PushStaticImage(imagePath);
            _lastPushedSnapshot = snapshot;
            _lastPushedAt = DateTimeOffset.Now;
            return LcdPushResult.Ok(valuesChanged ? "updated" : "refresh");
        }
        catch (Exception ex)
        {
            return LcdPushResult.Failed(ex.Message);
        }
    }

    private static void Render(TelemetrySnapshot snapshot, string outputPath, int textOffsetYPx, int sectionGapPx)
    {
        using var bitmap = new Bitmap(640, 640);
        using var graphics = Graphics.FromImage(bitmap);
        graphics.Clear(Color.Black);
        graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.HighQuality;
        graphics.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;
        using var outlinePen = new Pen(Color.FromArgb(55, 55, 60), 6f);
        graphics.DrawEllipse(outlinePen, 18, 18, 640 - 36, 640 - 36);

        const int gaugeInset = 38;
        const int gaugeSpan = 130;
        const int arcWidth = 18;
        var gaugeRect = new Rectangle(gaugeInset, gaugeInset, 640 - (gaugeInset * 2), 640 - (gaugeInset * 2));

        DrawGauge(graphics, gaugeRect, 278, gaugeSpan, TempRatio(snapshot.CpuTempC), GaugeColor(snapshot.CpuTempC, Color.FromArgb(0, 180, 255), Color.FromArgb(255, 180, 0), Color.FromArgb(255, 55, 55)), arcWidth);
        DrawGauge(graphics, gaugeRect, 132, gaugeSpan, TempRatio(snapshot.GpuTempC), GaugeColor(snapshot.GpuTempC, Color.FromArgb(214, 0, 191), Color.FromArgb(255, 130, 0), Color.FromArgb(255, 55, 55)), arcWidth);

        using var labelFont = FindFont(42);
        using var valueFont = FindFont(76);
        using var smallFont = FindFont(30);
        using var cpuLabelBrush = new SolidBrush(Color.FromArgb(107, 0, 222));
        using var gpuLabelBrush = new SolidBrush(Color.FromArgb(214, 0, 191));
        using var liquidLabelBrush = new SolidBrush(Color.FromArgb(190, 190, 190));
        using var whiteBrush = new SolidBrush(Color.White);
        using var smallBrush = new SolidBrush(Color.FromArgb(130, 130, 130));

        var y = textOffsetYPx;
        DrawCenteredText(graphics, "CPU", labelFont, cpuLabelBrush, 122 + y);
        DrawCenteredText(graphics, FormatDisplayTemp(snapshot.CpuTempC), valueFont, whiteBrush, 166 + y);
        DrawCenteredText(graphics, "GPU", labelFont, gpuLabelBrush, 270 + y + sectionGapPx);
        DrawCenteredText(graphics, FormatDisplayTemp(snapshot.GpuTempC), valueFont, whiteBrush, 314 + y + sectionGapPx);
        DrawCenteredText(graphics, "LIQUID", labelFont, liquidLabelBrush, 418 + y + (sectionGapPx * 2));
        DrawCenteredText(graphics, FormatDisplayTemp(snapshot.LiquidTempC), valueFont, whiteBrush, 462 + y + (sectionGapPx * 2));
        DrawCenteredText(graphics, $"PUMP {FormatDisplayRpm(snapshot.PumpRpm)}  FAN {FormatDisplayRpm(snapshot.FanRpm)}", smallFont, smallBrush, 560 + y + (sectionGapPx * 2));

        bitmap.Save(outputPath, ImageFormat.Png);
    }

    private static void DrawGauge(Graphics graphics, Rectangle gaugeRect, int startAngle, int gaugeSpan, float ratio, Color activeColor, int width)
    {
        using var backPen = new Pen(Color.FromArgb(32, 32, 38), width);
        using var activePen = new Pen(activeColor, width);
        graphics.DrawArc(backPen, gaugeRect, startAngle, gaugeSpan);

        var extent = (int)(gaugeSpan * ratio);
        if (extent > 0)
        {
            graphics.DrawArc(activePen, gaugeRect, startAngle, extent);
        }
    }

    private static void DrawCenteredText(Graphics graphics, string text, Font font, Brush brush, int y)
    {
        var size = graphics.MeasureString(text, font);
        var x = (640 - size.Width) / 2;
        graphics.DrawString(text, font, brush, x, y);
    }

    private static float TempRatio(float? value, float low = 30f, float high = 90f)
    {
        if (!value.HasValue)
        {
            return 0f;
        }

        return Math.Clamp((value.Value - low) / (high - low), 0f, 1f);
    }

    private static Color GaugeColor(float? value, Color cool, Color warm, Color hot)
    {
        if (!value.HasValue)
        {
            return Color.FromArgb(65, 65, 70);
        }

        return value.Value switch
        {
            >= 80f => hot,
            >= 65f => warm,
            _ => cool,
        };
    }

    private static Font FindFont(float size)
    {
        return new Font("Segoe UI", size, FontStyle.Bold, GraphicsUnit.Pixel);
    }

    private static string FormatDisplayTemp(float? value) => value.HasValue ? $"{MathF.Round(value.Value):0}C" : "--";
    private static string FormatDisplayRpm(int? value) => value?.ToString(CultureInfo.InvariantCulture) ?? "--";
}

internal sealed record LcdPushResult(bool Attempted, bool Success, string Status)
{
    public static LcdPushResult Skipped(string status) => new(false, true, status);
    public static LcdPushResult Ok(string status) => new(true, true, status);
    public static LcdPushResult Failed(string status) => new(true, false, status);
}

internal sealed record ControllerConfig(
    double IntervalSeconds,
    int LowDuty,
    int HighDuty,
    float CoolThresholdC,
    float CoolThresholdHysteresisC,
    int HighHoldSeconds,
    int Brightness,
    int RotationDegrees,
    int TextOffsetYPx,
    int SectionGapPx)
{
    public static ControllerConfig Load(string configPath)
    {
        try
        {
            var json = File.ReadAllText(configPath, Encoding.UTF8);
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            var config = new ControllerConfig(
                root.TryGetProperty("interval_seconds", out var intervalSeconds) ? intervalSeconds.GetDouble() : 2.0,
                root.TryGetProperty("low_duty", out var lowDuty) ? lowDuty.GetInt32() : 80,
                root.TryGetProperty("high_duty", out var highDuty) ? highDuty.GetInt32() : 100,
                root.TryGetProperty("cool_threshold_c", out var threshold) ? threshold.GetSingle() : 40f,
                root.TryGetProperty("cool_threshold_hysteresis_c", out var hysteresis) ? hysteresis.GetSingle() : 3f,
                root.TryGetProperty("high_hold_seconds", out var highHoldSeconds) ? highHoldSeconds.GetInt32() : 10,
                root.TryGetProperty("brightness", out var brightness) ? brightness.GetInt32() : 100,
                root.TryGetProperty("rotation_degrees", out var rotationDegrees) ? rotationDegrees.GetInt32() : 90,
                root.TryGetProperty("text_offset_y_px", out var textOffsetY) ? textOffsetY.GetInt32() : -50,
                root.TryGetProperty("section_gap_px", out var sectionGap) ? sectionGap.GetInt32() : 20);
            if (!IsValid(config))
                throw new FormatException("Cooling configuration is outside safe operating bounds.");
            return config;
        }
        catch
        {
            // Invalid/missing configuration must not reduce cooling.
            return new ControllerConfig(2.0, 100, 100, 40f, 3f, 10, 100, 90, -50, 20);
        }
    }

    internal static bool IsValid(ControllerConfig config) =>
        double.IsFinite(config.IntervalSeconds) && config.IntervalSeconds is >= 1 and <= 5 &&
        config.LowDuty is >= 20 and <= 100 && config.HighDuty >= config.LowDuty && config.HighDuty <= 100 &&
        float.IsFinite(config.CoolThresholdC) && config.CoolThresholdC is >= 25 and <= 90 &&
        float.IsFinite(config.CoolThresholdHysteresisC) && config.CoolThresholdHysteresisC is >= 0 and <= 20 &&
        config.HighHoldSeconds is >= 0 and <= 300;

    public static int ComputeDuty(TelemetrySnapshot snapshot, ControllerConfig config, int? lastDuty, DateTimeOffset? lastHighDutyAt, DateTimeOffset now)
    {
        // Missing/invalid telemetry is not evidence that the machine is cool.
        if (!ValidTemperature(snapshot.CpuTempC) || !ValidTemperature(snapshot.GpuTempC) ||
            !ValidTemperature(snapshot.LiquidTempC))
            return 100;

        var maxTemp = new[]
        {
            snapshot.CpuTempC,
            snapshot.GpuTempC,
            snapshot.LiquidTempC
        }
        .Where(v => v.HasValue)
        .Select(v => v!.Value)
        .DefaultIfEmpty(0f)
        .Max();

        if (lastDuty == config.HighDuty)
        {
            if (lastHighDutyAt.HasValue && now - lastHighDutyAt.Value < TimeSpan.FromSeconds(config.HighHoldSeconds))
            {
                return config.HighDuty;
            }

            return maxTemp <= config.CoolThresholdC - config.CoolThresholdHysteresisC
                ? config.LowDuty
                : config.HighDuty;
        }

        return maxTemp >= config.CoolThresholdC ? config.HighDuty : config.LowDuty;
    }

    internal static bool ValidTemperature(float? value) => value.HasValue && float.IsFinite(value.Value) && value > 0 && value < 120;
    internal static float? NormalizeTemperature(float? value) => ValidTemperature(value) ? value : null;
}

internal sealed record ControllerHealthSnapshot(
    DateTimeOffset Timestamp,
    string Status,
    float? CpuTempC,
    float? GpuTempC,
    float? LiquidTempC,
    int? PumpRpm,
    int? FanRpm,
    int? Duty,
    string LcdStatus,
    string Error,
    int ProcessId = 0,
    DateTimeOffset ProcessStartedAt = default,
    int? PumpDuty = null,
    int? FanDuty = null);

internal static class ControllerHealth
{
    public static void Write(string path, ControllerHealthSnapshot snapshot)
    {
        ControllerFiles.TryWriteJson(path, snapshot);
    }
}

internal static class NvmlNative
{
    [DllImport("nvml.dll", CallingConvention = CallingConvention.Cdecl)]
    public static extern NvmlReturn nvmlInit_v2();

    [DllImport("nvml.dll", CallingConvention = CallingConvention.Cdecl)]
    public static extern NvmlReturn nvmlShutdown();

    [DllImport("nvml.dll", CallingConvention = CallingConvention.Cdecl)]
    public static extern NvmlReturn nvmlDeviceGetHandleByIndex_v2(uint index, out IntPtr device);

    [DllImport("nvml.dll", CallingConvention = CallingConvention.Cdecl)]
    public static extern NvmlReturn nvmlDeviceGetTemperature(IntPtr device, NvmlTemperatureSensor sensorType, out uint temp);
}

internal enum NvmlTemperatureSensor : uint
{
    Gpu = 0
}

internal enum NvmlReturn : uint
{
    Success = 0
}
