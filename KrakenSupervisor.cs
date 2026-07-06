using System.Diagnostics;
using System.ServiceProcess;
using System.Text;
using System.Text.Json;
using System.Threading;

internal sealed class KrakenSupervisor
{
    private readonly int _supervisorPid;
    private readonly string _controllerExePath;
    private readonly string _healthPath;
    private readonly string _logPath;
    private readonly Mutex _mutex;

    public KrakenSupervisor(string controllerExePath, string healthPath, string logPath)
    {
        _supervisorPid = Environment.ProcessId;
        _controllerExePath = controllerExePath;
        _healthPath = healthPath;
        _logPath = logPath;
        _mutex = new Mutex(false, @"Global\DrakulaKrakenSupervisor");
    }

    public int Run()
    {
        if (!_mutex.WaitOne(0))
        {
            Log("another supervisor instance is already running");
            return 0;
        }

        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_logPath)!);
            Log("supervisor started");
            RunLoop(CancellationToken.None);
            return 0;
        }
        finally
        {
            try
            {
                _mutex.ReleaseMutex();
            }
            catch
            {
            }

            _mutex.Dispose();
        }
    }

    public void RunLoop(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                var controller = FindControllerProcess();
                var healthy = controller is not null && IsHealthy(controller);

                if (!healthy)
                {
                    TryMaxCooling();

                    if (controller is not null)
                    {
                        TryKill(controller);
                        Log($"controller pid={controller.Id} stopped for recovery");
                    }

                    StartControllerHidden();
                }
            }
            catch (Exception ex)
            {
                Log("supervisor_error " + ex.Message);
            }

            cancellationToken.WaitHandle.WaitOne(TimeSpan.FromSeconds(10));
        }
    }

    private bool IsHealthy(Process controller)
    {
        if (!File.Exists(_healthPath))
        {
            Log("health check failed: health file missing");
            return false;
        }

        var age = DateTimeOffset.Now - File.GetLastWriteTime(_healthPath);
        if (age > TimeSpan.FromSeconds(20))
        {
            Log($"health check failed: stale health age={age.TotalSeconds:0.0}s");
            return false;
        }

        try
        {
            var payload = JsonSerializer.Deserialize<ControllerHealthSnapshot>(File.ReadAllText(_healthPath, Encoding.UTF8));
            if (payload is null)
            {
                Log("health check failed: empty payload");
                return false;
            }

            if (!string.Equals(payload.Status, "ok", StringComparison.OrdinalIgnoreCase))
            {
                Log($"health check failed: status={payload.Status}");
                return false;
            }

            if (!payload.PumpRpm.HasValue || payload.PumpRpm.Value < 1500)
            {
                Log($"health check failed: pump_rpm={payload.PumpRpm}");
                return false;
            }

            if (!payload.FanRpm.HasValue || payload.FanRpm.Value < 1000)
            {
                Log($"health check failed: fan_rpm={payload.FanRpm}");
                return false;
            }

            if (string.Equals(payload.LcdStatus, "failed", StringComparison.OrdinalIgnoreCase) ||
                payload.LcdStatus.Contains("acknowledge", StringComparison.OrdinalIgnoreCase))
            {
                Log($"health check failed: lcd_status={payload.LcdStatus}");
                return false;
            }

            if (controller.HasExited)
            {
                Log("health check failed: controller exited");
                return false;
            }

            return true;
        }
        catch
        {
            Log("health check failed: payload parse error");
            return false;
        }
    }

    private Process? FindControllerProcess()
    {
        return Process.GetProcessesByName("KrakenHost")
            .FirstOrDefault(process => process.Id != _supervisorPid);
    }

    private void StartControllerHidden()
    {
        var process = Process.Start(new ProcessStartInfo
        {
            FileName = _controllerExePath,
            Arguments = "--interval=2 --min-lcd-push=2 --max-lcd-refresh=30",
            WorkingDirectory = Path.GetDirectoryName(_controllerExePath)!,
            UseShellExecute = false,
            CreateNoWindow = true,
            WindowStyle = ProcessWindowStyle.Hidden
        });

        Log($"controller started pid={process?.Id}");
    }

    private void TryMaxCooling()
    {
        try
        {
            using var process = Process.Start(new ProcessStartInfo
            {
                FileName = _controllerExePath,
                Arguments = "--max-cooling",
                WorkingDirectory = Path.GetDirectoryName(_controllerExePath)!,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
                WindowStyle = ProcessWindowStyle.Hidden
            });

            process?.WaitForExit(5000);
            Log("emergency max-cooling invoked");
        }
        catch (Exception ex)
        {
            Log("max-cooling failed " + ex.Message);
        }
    }

    private static void TryKill(Process process)
    {
        try
        {
            process.Kill(entireProcessTree: true);
            process.WaitForExit(5000);
        }
        catch
        {
        }
    }
    private void Log(string message)
    {
        var line = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} {message}";
        File.AppendAllText(_logPath, line + Environment.NewLine, Encoding.UTF8);
    }
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
        _thread = new Thread(() => _supervisor.RunLoop(_cts.Token))
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
