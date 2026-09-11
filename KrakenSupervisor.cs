using System.Diagnostics;
using System.ServiceProcess;
using System.Text;
using System.Text.Json;
using System.Threading;

internal sealed class KrakenSupervisor
{
    private readonly string _controllerExePath;
    private readonly string _healthPath;
    private readonly string _logPath;
    private Process? _controller;
    private DateTimeOffset _controllerStartedAt;
    private ControllerHealthSnapshot? _lastGoodHealth;
    private static readonly TimeSpan HealthTimeout = TimeSpan.FromSeconds(20);

    public KrakenSupervisor(string controllerExePath, string healthPath, string logPath)
    {
        _controllerExePath = Path.GetFullPath(controllerExePath);
        _healthPath = healthPath;
        _logPath = logPath;
    }

    public int Run(CancellationToken cancellationToken = default)
    {
        using var mutex = new Mutex(false, @"Global\DrakulaKrakenSupervisor");
        var acquired = false;
        try
        {
            try { acquired = mutex.WaitOne(0); }
            catch (AbandonedMutexException) { acquired = true; }
            if (!acquired)
            {
                Log("another supervisor instance is already running");
                return 0;
            }

            ControllerFiles.TryEnsureDirectory(Path.GetDirectoryName(_logPath)!);
            Log("supervisor started");
            RunLoop(cancellationToken);
            return 0;
        }
        finally
        {
            // Disposing a Process handle does not stop the controller.
            _controller?.Dispose();
            _controller = null;
            _controllerStartedAt = default;
            _lastGoodHealth = null;
            if (acquired) mutex.ReleaseMutex();
        }
    }

    private void RunLoop(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                var controller = FindControllerProcess();
                if (controller is null)
                {
                    if (ControllerMutexIsAvailable() && !cancellationToken.IsCancellationRequested)
                    {
                        StartControllerHidden();
                    }
                    else
                    {
                        Log("controller ownership unavailable; preserving the existing hardware owner");
                    }
                }
                else if (!IsHealthy(controller, out var reason))
                {
                    var startup = IsWithinStartupGrace(_controllerStartedAt, DateTimeOffset.UtcNow);
                    Log(startup
                        ? $"controller pid={controller.Id} startup grace: {reason}"
                        : $"controller pid={controller.Id} unhealthy; recovery blocked without proven cooling fallback: {reason}");
                }
            }
            catch (Exception ex)
            {
                Log("supervisor_error " + ex.Message);
            }

            cancellationToken.WaitHandle.WaitOne(TimeSpan.FromSeconds(10));
        }
    }

    private bool IsHealthy(Process controller, out string reason)
    {
        if (!MatchesController(controller, _controllerStartedAt))
        {
            reason = "controller executable or start identity could not be verified";
            return false;
        }

        var now = DateTimeOffset.UtcNow;
        if (TryReadHealth(out var payload, out var readError))
        {
            var result = EvaluateHealth(payload, controller.Id, _controllerStartedAt, now);
            // A newer explicit failure invalidates an older healthy sample.
            _lastGoodHealth = result.Healthy ? payload : null;
            reason = result.Reason;
            return result.Healthy;
        }

        // A read failure must not renew the original sample's freshness deadline.
        var cached = EvaluateHealth(_lastGoodHealth, controller.Id, _controllerStartedAt, now);
        reason = cached.Healthy ? "transient health read failure; last valid sample is still fresh" : readError;
        return cached.Healthy;
    }

    internal static bool IsWithinStartupGrace(DateTimeOffset processStartedAt, DateTimeOffset now)
    {
        var age = now - processStartedAt;
        return processStartedAt != default && age >= TimeSpan.Zero && age < HealthTimeout;
    }

    internal static (bool Healthy, string Reason) EvaluateHealth(
        ControllerHealthSnapshot? payload, int expectedProcessId,
        DateTimeOffset expectedProcessStartedAt, DateTimeOffset now)
    {
        if (payload is null) return (false, "health payload missing");
        if (expectedProcessId <= 0 || payload.ProcessId != expectedProcessId ||
            expectedProcessStartedAt == default || payload.ProcessStartedAt != expectedProcessStartedAt)
            return (false, "health belongs to a different or unverified controller");
        if (payload.Timestamp < expectedProcessStartedAt || payload.Timestamp > now.AddSeconds(5))
            return (false, "health timestamp is invalid");
        if (now - payload.Timestamp > HealthTimeout)
            return (false, "health payload is stale");
        if (!string.Equals(payload.Status, "ok", StringComparison.OrdinalIgnoreCase))
            return (false, $"health status={payload.Status}");
        if (!payload.PumpRpm.HasValue || payload.PumpRpm.Value < 1500)
            return (false, $"pump_rpm={payload.PumpRpm}");
        if (!payload.FanRpm.HasValue || payload.FanRpm.Value < 1000)
            return (false, $"fan_rpm={payload.FanRpm}");
        // LCD failure is not evidence that a healthy cooling controller should be stopped.
        return (true, "ok");
    }

    private bool TryReadHealth(out ControllerHealthSnapshot? payload, out string error)
    {
        payload = null;
        error = "health file unreadable";
        for (var attempt = 0; attempt < 3; attempt++)
        {
            try
            {
                // Allow atomic replacement while this handle reads a complete old snapshot.
                using var stream = new FileStream(_healthPath, FileMode.Open, FileAccess.Read,
                    FileShare.ReadWrite | FileShare.Delete);
                using var reader = new StreamReader(stream, Encoding.UTF8);
                payload = JsonSerializer.Deserialize<ControllerHealthSnapshot>(reader.ReadToEnd());
                if (payload is not null) return true;
                error = "health payload is empty";
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
            {
                error = "health read failed: " + ex.GetType().Name;
            }
            if (attempt < 2) Thread.Sleep(25);
        }
        return false;
    }

    private Process? FindControllerProcess()
    {
        if (_controller is not null)
        {
            try
            {
                _controller.Refresh();
                if (!_controller.HasExited) return _controller;
            }
            catch (Exception ex)
            {
                Log("controller liveness could not be verified; preserving it: " + ex.GetType().Name);
                return _controller;
            }
            _controller.Dispose();
            _controller = null;
            _controllerStartedAt = default;
            _lastGoodHealth = null;
        }

        // Adopt only the exact producer identified by new-format health; legacy PID=0
        // is left alone and the global controller mutex prevents a second USB owner.
        if (!TryReadHealth(out var payload, out _) || payload is null || payload.ProcessId <= 0)
            return null;
        Process? candidate = null;
        try
        {
            candidate = Process.GetProcessById(payload.ProcessId);
            if (!MatchesController(candidate, payload.ProcessStartedAt)) return null;
            _controller = candidate;
            _controllerStartedAt = payload.ProcessStartedAt;
            candidate = null;
            Log($"adopted verified controller pid={_controller.Id}");
            return _controller;
        }
        catch (Exception ex)
        {
            Log("health producer unavailable: " + ex.GetType().Name);
            return null;
        }
        finally { candidate?.Dispose(); }
    }

    private bool MatchesController(Process process, DateTimeOffset expectedStartedAt)
    {
        try
        {
            return process.Id != Environment.ProcessId && !process.HasExited &&
                expectedStartedAt != default &&
                new DateTimeOffset(process.StartTime.ToUniversalTime()) == expectedStartedAt &&
                string.Equals(Path.GetFullPath(process.MainModule!.FileName), _controllerExePath,
                    StringComparison.OrdinalIgnoreCase);
        }
        catch { return false; }
    }

    private void StartControllerHidden()
    {
        _controller = Process.Start(new ProcessStartInfo
        {
            FileName = _controllerExePath,
            Arguments = "--interval=2 --min-lcd-push=2 --max-lcd-refresh=30",
            WorkingDirectory = Path.GetDirectoryName(_controllerExePath)!,
            UseShellExecute = false,
            CreateNoWindow = true,
            WindowStyle = ProcessWindowStyle.Hidden
        });

        if (_controller is null) throw new InvalidOperationException("Controller process did not start.");
        _controllerStartedAt = new DateTimeOffset(_controller.StartTime.ToUniversalTime());
        _lastGoodHealth = null;
        Log($"controller started pid={_controller.Id}");
    }

    private bool ControllerMutexIsAvailable()
    {
        try
        {
            using var mutex = new Mutex(false, @"Global\DrakulaKrakenController");
            var acquired = false;
            try
            {
                try { acquired = mutex.WaitOne(0); }
                catch (AbandonedMutexException) { acquired = true; }
                return acquired;
            }
            finally { if (acquired) mutex.ReleaseMutex(); }
        }
        catch (Exception ex)
        {
            Log("controller mutex could not be verified: " + ex.GetType().Name);
            return false;
        }
    }

    private void Log(string message) => ControllerLog.Write(_logPath, message);
}

internal sealed class KrakenSupervisorWindowsService : ServiceBase
{
    private readonly KrakenSupervisor _supervisor;
    private CancellationTokenSource? _cts;
    private Thread? _thread;

    public KrakenSupervisorWindowsService(string controllerExePath, string healthPath, string logPath)
    {
        ServiceName = "KrakenSupervisor";
        CanStop = true;
        CanShutdown = true;
        AutoLog = false;
        _supervisor = new KrakenSupervisor(controllerExePath, healthPath, logPath);
    }

    protected override void OnStart(string[] args)
    {
        _cts = new CancellationTokenSource();
        _thread = new Thread(() => _supervisor.Run(_cts.Token))
        {
            IsBackground = true,
            Name = "KrakenSupervisorLoop"
        };
        _thread.Start();
    }

    protected override void OnStop()
    {
        _cts?.Cancel();
        _thread?.Join(TimeSpan.FromSeconds(5));
    }

    protected override void OnShutdown()
    {
        OnStop();
        base.OnShutdown();
    }
}
