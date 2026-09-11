using System.Text.Json;

internal static class SafetyChecks
{
    // This entry point never constructs a controller, supervisor, USB device or
    // telemetry reader. Scratch evidence is retained; it never deletes user files.
    public static int Run()
    {
        var root = Path.Combine(Path.GetTempPath(), "KrakenSafetyChecks-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        var passed = 0;
        void Check(bool condition, string name)
        {
            if (!condition) throw new InvalidOperationException("FAILED: " + name);
            passed++;
            Console.WriteLine("PASS: " + name);
        }

        try
        {
            var now = DateTimeOffset.UtcNow;
            var started = now.AddMinutes(-1);
            var healthy = new ControllerHealthSnapshot(now, "ok", 35, 35, 31, 2500, 1500, 80, "updated", "", 123, started);
            Check(KrakenSupervisor.EvaluateHealth(healthy, 123, started, now).Healthy, "fresh same-owner cooling accepted");
            Check(!KrakenSupervisor.EvaluateHealth(healthy, 124, started, now).Healthy, "foreign PID rejected");
            Check(!KrakenSupervisor.EvaluateHealth(healthy, 123, started.AddSeconds(1), now).Healthy, "reused PID rejected");
            Check(!KrakenSupervisor.EvaluateHealth(healthy, 123, started, now.AddSeconds(21)).Healthy, "stale health rejected");
            Check(!KrakenSupervisor.EvaluateHealth(healthy with { Timestamp = now.AddMinutes(1) }, 123, started, now).Healthy, "future health rejected");
            Check(!KrakenSupervisor.EvaluateHealth(healthy with { PumpRpm = 0 }, 123, started, now).Healthy, "stopped pump rejected");
            Check(!KrakenSupervisor.EvaluateHealth(healthy with { FanRpm = null }, 123, started, now).Healthy, "missing fan RPM rejected");
            Check(KrakenSupervisor.EvaluateHealth(healthy with { LcdStatus = "failed" }, 123, started, now).Healthy, "LCD failure does not restart healthy cooling");
            Check(!KrakenSupervisor.EvaluateHealth(healthy with { ProcessId = 0 }, 123, started, now).Healthy, "legacy ownerless payload rejected");
            Check(KrakenSupervisor.IsWithinStartupGrace(now.AddSeconds(-5), now), "new controller gets bounded startup grace");
            Check(!KrakenSupervisor.IsWithinStartupGrace(now.AddSeconds(-21), now), "startup grace expires");

            var coolingHold = new KrakenDeviceStatus(32, 2800, 100, 1700, 100);
            Check(CoolingHoldVerification.IsSafe(coolingHold), "cooling-hold proof requires actual full-duty readback");
            Check(!CoolingHoldVerification.IsSafe(coolingHold with { PumpDuty = 80 }), "cooling-hold rejects reduced pump duty");
            Check(!CoolingHoldVerification.IsSafe(coolingHold with { FanDuty = 80 }), "cooling-hold rejects reduced fan duty");
            Check(!CoolingHoldVerification.IsSafe(coolingHold with { PumpRpm = 0 }), "cooling-hold rejects stopped pump");
            Check(!CoolingHoldVerification.IsSafe(coolingHold with { FanRpm = null }), "cooling-hold rejects missing fan");
            Check(!CoolingHoldVerification.IsSafe(coolingHold with { LiquidTempC = float.NaN }), "cooling-hold rejects invalid temperature");
            Check(!CoolingHoldVerification.IsSafe(coolingHold with { LiquidTempC = 50 }), "cooling-hold rejects excessive liquid temperature");

            var config = new ControllerConfig(1, 80, 100, 40, 3, 10, 100, 90, -50, 10);
            var telemetry = new TelemetrySnapshot(now, 35, 35, 31, 2500, 1500);
            Check(ControllerConfig.ComputeDuty(telemetry, config, null, null, now) == 80, "valid cool telemetry retains existing duty policy");
            Check(ControllerConfig.ComputeDuty(telemetry with { CpuTempC = 85 }, config, 80, null, now) == 100, "hot CPU selects full cooling");
            Check(ControllerConfig.ComputeDuty(telemetry, config, 100, now.AddSeconds(-2), now) == 100, "high-duty hold retained");
            Check(ControllerConfig.ComputeDuty(telemetry with { CpuTempC = null }, config, 80, null, now) == 100, "missing CPU fails to full cooling");
            Check(ControllerConfig.ComputeDuty(telemetry with { GpuTempC = float.NaN }, config, 80, null, now) == 100, "NaN GPU fails to full cooling");
            Check(ControllerConfig.ComputeDuty(telemetry with { LiquidTempC = float.PositiveInfinity }, config, 80, null, now) == 100, "infinite liquid temperature fails safe");
            Check(ControllerConfig.NormalizeTemperature(float.NaN) is null && ControllerConfig.NormalizeTemperature(float.PositiveInfinity) is null, "invalid temperature normalized before JSON publication");
            Check(!ControllerConfig.IsValid(config with { IntervalSeconds = double.NaN }), "NaN interval rejected");
            Check(!ControllerConfig.IsValid(config with { HighDuty = 10 }), "inverted duty limits rejected");
            Check(ControllerConfig.Load(Path.Combine(root, "missing-config.json")).LowDuty == 100, "missing configuration defaults full cooling");

            var healthPath = Path.Combine(root, "health.json");
            Check(ControllerFiles.TryWriteJson(healthPath, healthy), "initial complete health publication");
            var writer = Task.Run(() =>
            {
                var published = 0;
                for (var i = 0; i < 500; i++)
                    if (ControllerFiles.TryWriteJson(healthPath, healthy with { Duty = i % 2 == 0 ? 80 : 100 })) published++;
                return published;
            });
            for (var i = 0; i < 500; i++)
            {
                using var input = new FileStream(healthPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
                var snapshot = JsonSerializer.Deserialize<ControllerHealthSnapshot>(input);
                if (snapshot is null || snapshot.ProcessId != 123 || snapshot.Duty is not (80 or 100))
                    throw new InvalidOperationException("Torn health snapshot observed.");
            }
            var publications = writer.GetAwaiter().GetResult();
            Check(publications > 0, $"concurrent publications make progress ({publications}/500 accepted; {500 - publications} failed attempts; prior health retained)");
            Check(true, "500 concurrent reads only see complete snapshots");
            Check(ControllerFiles.TryWriteJson(healthPath, healthy with { Duty = 100 }), "publication succeeds after concurrency");
            Check(JsonSerializer.Deserialize<ControllerHealthSnapshot>(File.ReadAllText(healthPath))?.Duty == 100, "published value read back");
            using (var locked = new FileStream(healthPath, FileMode.Open, FileAccess.Read, FileShare.None))
                Check(!ControllerFiles.TryWriteJson(healthPath, healthy), "locked destination fails without throwing");
            Check(JsonSerializer.Deserialize<ControllerHealthSnapshot>(File.ReadAllText(healthPath))?.Duty == 100, "failed replacement preserves last complete snapshot");
            Check(!ControllerFiles.TryWriteJson(healthPath, healthy with { CpuTempC = float.NaN }), "unserializable telemetry cannot escape into cooling");
            Check(JsonSerializer.Deserialize<ControllerHealthSnapshot>(File.ReadAllText(healthPath))?.Duty == 100, "serialization failure preserves valid health");
            Check(!ControllerFiles.TryEnsureDirectory(healthPath), "diagnostic directory failure cannot block controller or supervisor startup");
            ControllerLog.Write(Path.Combine(root, "absent", "controller.log"), "must not crash");
            Check(true, "log I/O error does not escape into cooling");
            var logPath = Path.Combine(root, "controller.log");
            using (var file = new FileStream(logPath, FileMode.CreateNew)) file.SetLength(ControllerLog.MaxActiveBytes);
            ControllerLog.Write(logPath, "after rotation");
            Check(new FileInfo(logPath).Length < 1024 && Directory.GetFiles(root, "*.archive").Length == 1, "active log rotates without deleting history");

            passed += NativeSafetyChecks.Run();
            File.WriteAllText(Path.Combine(root, "result.json"), JsonSerializer.Serialize(new { passed, publications, publicationAttempts = 500, hardwareAccess = false }));
            Console.WriteLine($"PASS: {passed} checks; no hardware access. Evidence: {root}");
            return 0;
        }
        catch (Exception ex)
        {
            File.WriteAllText(Path.Combine(root, "failure.txt"), ex.ToString());
            Console.Error.WriteLine(ex);
            Console.Error.WriteLine("Evidence: " + root);
            return 1;
        }
    }
}
