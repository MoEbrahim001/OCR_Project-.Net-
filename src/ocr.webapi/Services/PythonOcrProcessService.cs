using System.Diagnostics;

namespace Ocr.WebApi.Services;

public sealed class PythonOcrProcessService : IHostedService
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<PythonOcrProcessService> _logger;

    private Process? _process;
    private bool _ownsProcess;

    public PythonOcrProcessService(
        IConfiguration configuration,
        ILogger<PythonOcrProcessService> logger)
    {
        _configuration = configuration;
        _logger = logger;
    }

    public async Task StartAsync(
        CancellationToken cancellationToken)
    {
        var baseUrl =
            _configuration["Ocr:BaseUrl"]
            ?? "http://127.0.0.1:8001";

        // If OCR is already running, don't start another copy.
        if (await IsHealthyAsync(
                baseUrl,
                cancellationToken))
        {
            _logger.LogInformation(
                "OCR Python service is already running."
            );

            return;
        }

        var configuredPath =
            _configuration["Ocr:ExecutablePath"];

        string exePath;

        if (!string.IsNullOrWhiteSpace(configuredPath))
        {
            exePath =
                Path.IsPathRooted(configuredPath)
                    ? configuredPath
                    : Path.Combine(
                        AppContext.BaseDirectory,
                        configuredPath
                    );
        }
        else
        {
            exePath = Path.Combine(
                AppContext.BaseDirectory,
                "Python",
                "OcrPython.exe"
            );
        }

        exePath = Path.GetFullPath(exePath);

        if (!File.Exists(exePath))
        {
            throw new FileNotFoundException(
                $"OCR Python executable not found: {exePath}"
            );
        }

        var workingDirectory =
            Path.GetDirectoryName(exePath)!;

        var startInfo =
    new ProcessStartInfo
    {
        FileName = exePath,
        WorkingDirectory = workingDirectory,

        UseShellExecute = false,
        CreateNoWindow = true,
        WindowStyle = ProcessWindowStyle.Hidden,

        RedirectStandardOutput = true,
        RedirectStandardError = true
    };

        // Give Paddle enough time for cold-start inference.
        startInfo.Environment[
            "OCR_PADDLE_TIMEOUT"
        ] = "120";
        startInfo.Environment[
    "PYTHONUNBUFFERED"
] = "1";
        _process = new Process
        {
            StartInfo = startInfo,
            EnableRaisingEvents = true
        };

        _process.OutputDataReceived += (_, e) =>
        {
            if (!string.IsNullOrWhiteSpace(e.Data))
            {
                _logger.LogInformation(
                    "[Python OCR] {Message}",
                    e.Data
                );
            }
        };

        _process.ErrorDataReceived += (_, e) =>
        {
            if (!string.IsNullOrWhiteSpace(e.Data))
            {
                _logger.LogWarning(
                    "[Python OCR] {Message}",
                    e.Data
                );
            }
        };

        _process.Exited += (_, _) =>
        {
            _logger.LogError(
                "OCR Python process exited. ExitCode: {ExitCode}",
                _process?.ExitCode
            );
        };

        if (!_process.Start())
        {
            throw new InvalidOperationException(
                "Could not start OCR Python service."
            );
        }

        _process.BeginOutputReadLine();
        _process.BeginErrorReadLine();

        _ownsProcess = true;

        _logger.LogInformation(
            "Started OCR Python process. PID: {Pid}",
            _process.Id
        );

        // Wait for Python / models to become ready.
        var deadline =
            DateTime.UtcNow.AddSeconds(120);

        while (
            DateTime.UtcNow < deadline
            &&
            !cancellationToken.IsCancellationRequested)
        {
            if (_process.HasExited)
            {
                throw new InvalidOperationException(
                    $"OCR Python process exited unexpectedly. Exit code: {_process.ExitCode}"
                );
            }

            if (await IsHealthyAsync(
                    baseUrl,
                    cancellationToken))
            {
                _logger.LogInformation(
                    "OCR Python service is ready."
                );

                return;
            }

            await Task.Delay(
                500,
                cancellationToken
            );
        }

        throw new TimeoutException(
            "OCR Python service did not become ready within 120 seconds."
        );
    }

    public Task StopAsync(
        CancellationToken cancellationToken)
    {
        if (
            _ownsProcess
            &&
            _process is not null
            &&
            !_process.HasExited)
        {
            try
            {
                _process.Kill(
                    entireProcessTree: true
                );

                _process.WaitForExit(
                    5000
                );
            }
            catch (Exception ex)
            {
                _logger.LogWarning(
                    ex,
                    "Could not stop OCR Python process cleanly."
                );
            }
        }

        _process?.Dispose();
        _process = null;

        return Task.CompletedTask;
    }

    private static async Task<bool> IsHealthyAsync(
        string baseUrl,
        CancellationToken cancellationToken)
    {
        try
        {
            using var client =
                new HttpClient
                {
                    Timeout =
                        TimeSpan.FromSeconds(2)
                };

            using var response =
                await client.GetAsync(
                    $"{baseUrl.TrimEnd('/')}/health",
                    cancellationToken
                );

            if (!response.IsSuccessStatusCode)
            {
                return false;
            }

            var body =
                await response.Content
                    .ReadAsStringAsync(
                        cancellationToken
                    );

            return body.Contains(
                "\"modelsLoaded\":true",
                StringComparison.OrdinalIgnoreCase
            );
        }
        catch
        {
            return false;
        }
    }
}