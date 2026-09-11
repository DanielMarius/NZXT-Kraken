using System.Diagnostics;

internal static class CoolingHoldVerification
{
    // Operator-only commissioning test. The caller owns the global controller
    // mutex. No LCD, CPU driver, USB reset, power change or second writer is used.
    public static void Run(string proofPath)
    {
        KrakenNativeDevice? device = null;
        var samples = new List<object>();
        var startedAt = DateTimeOffset.UtcNow;
        try
        {
            device = new KrakenNativeDevice();
            device.ApplyFixedDuties(100, 100);
            Thread.Sleep(5000);
            var initial = device.ReadStatus();
            RequireSafe(initial);
            samples.Add(new { Timestamp = DateTimeOffset.UtcNow, Phase = "maximum-established", Status = initial });

            device.Dispose();
            device = null;
            var hold = Stopwatch.StartNew();
            device = new KrakenNativeDevice();
            // ReadStatus sends only the non-control status query 0x74/0x01.
            // Do not rewrite duties during this observation: the firmware must
            // retain the profile across handle closure and reopening itself.
            for (var sample = 0; sample < 10; sample++)
            {
                Thread.Sleep(2000);
                var status = device.ReadStatus();
                RequireSafe(status);
                samples.Add(new { Timestamp = DateTimeOffset.UtcNow, Phase = "firmware-hold", Status = status });
                if (!ControllerFiles.TryWriteJson(proofPath, new
                    {
                        ProcessId = Environment.ProcessId, StartedAt = startedAt,
                        Completed = false, HoldSeconds = hold.Elapsed.TotalSeconds, Samples = samples
                    }))
                    throw new IOException("Cooling-hold evidence could not be published.");
            }

            if (!ControllerFiles.TryWriteJson(proofPath, new
                {
                    ProcessId = Environment.ProcessId, StartedAt = startedAt,
                    Completed = true, HoldSeconds = hold.Elapsed.TotalSeconds, Samples = samples
                }))
                throw new IOException("Completed cooling-hold evidence could not be published.");
            Console.WriteLine($"Cooling hold verified: {hold.Elapsed.TotalSeconds:0.0}s at 100% across HID close/reopen; normal controller starting.");
        }
        catch
        {
            // Best effort from this same exclusive owner before the commissioning
            // harness restores its prepared controller. Never claim this succeeded
            // without a subsequent fresh device readback.
            try
            {
                device ??= new KrakenNativeDevice();
                device.ApplyFixedDuties(100, 100);
            }
            catch { }
            throw;
        }
        finally { device?.Dispose(); }
    }

    internal static bool IsSafe(KrakenDeviceStatus status) =>
        status.PumpDuty == 100 && status.FanDuty == 100 &&
        status.PumpRpm >= 1500 && status.FanRpm >= 1000 &&
        status.LiquidTempC.HasValue && float.IsFinite(status.LiquidTempC.Value) &&
        status.LiquidTempC.Value is > 0 and < 50;

    private static void RequireSafe(KrakenDeviceStatus status)
    {
        if (!IsSafe(status)) throw new InvalidOperationException($"Cooling-hold readback failed: {status}");
    }
}
