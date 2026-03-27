using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text.Json;
using Microsoft.Web.WebView2.WinForms;

namespace InRemedy.Desktop;

public partial class Form1 : Form
{
    private readonly HttpClient _httpClient = new() { Timeout = TimeSpan.FromSeconds(2) };
    private readonly WebView2 _webView;
    private readonly Panel _splashPanel;
    private readonly Label _statusLabel;
    private readonly Label _subStatusLabel;
    private Process? _backendProcess;
    private AppConfig? _config;
    private bool _closeConfirmed;
    private bool _isStarting;
    private readonly SafeJobHandle _jobHandle;
    private readonly string _appDataDirectory = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "In-Remedy");
    private readonly string _logDirectory;

    public Form1()
    {
        InitializeComponent();
        _jobHandle = JobObjectHelper.CreateKillOnCloseJob();
        _logDirectory = Path.Combine(_appDataDirectory, "logs");

        Text = "In-Remedy";
        MinimumSize = new Size(1200, 800);

        var iconPath = Path.Combine(AppContext.BaseDirectory, "InRemedy.ico");
        if (File.Exists(iconPath))
        {
            Icon = new Icon(iconPath);
        }

        _webView = new WebView2
        {
            Dock = DockStyle.Fill,
            Visible = false,
            BackColor = Color.FromArgb(18, 20, 23),
        };

        _splashPanel = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = Color.FromArgb(18, 20, 23),
        };

        _statusLabel = new Label
        {
            AutoSize = false,
            Dock = DockStyle.Top,
            Height = 48,
            Font = new Font("Segoe UI Semibold", 20f, FontStyle.Bold),
            ForeColor = Color.White,
            TextAlign = ContentAlignment.BottomCenter,
            Text = "Starting In-Remedy",
        };

        _subStatusLabel = new Label
        {
            AutoSize = false,
            Dock = DockStyle.Top,
            Height = 30,
            Font = new Font("Segoe UI", 10.5f, FontStyle.Regular),
            ForeColor = Color.FromArgb(180, 190, 199),
            TextAlign = ContentAlignment.TopCenter,
            Text = "Preparing the application...",
        };

        var splashInner = new Panel
        {
            Size = new Size(540, 128),
            Anchor = AnchorStyles.None,
            BackColor = Color.Transparent,
        };

        splashInner.Controls.Add(_subStatusLabel);
        splashInner.Controls.Add(_statusLabel);
        _splashPanel.Controls.Add(splashInner);
        Controls.Add(_webView);
        Controls.Add(_splashPanel);

        Resize += (_, _) =>
        {
            splashInner.Left = Math.Max((ClientSize.Width - splashInner.Width) / 2, 0);
            splashInner.Top = Math.Max((ClientSize.Height - splashInner.Height) / 2, 0);
        };

        Shown += async (_, _) => await StartApplicationAsync();
        FormClosing += OnFormClosingAsync;
        FormClosed += (_, _) =>
        {
            _backendProcess?.Dispose();
            _httpClient.Dispose();
            _jobHandle.Dispose();
        };
    }

    private async Task StartApplicationAsync()
    {
        if (_isStarting)
        {
            return;
        }

        _isStarting = true;
        try
        {
            _config = LoadConfig();
            await EnsureBackendRunningAsync(_config);
            await InitializeWebViewAsync(_config);
        }
        catch (Exception exception)
        {
            MessageBox.Show(
                this,
                BuildStartupErrorMessage(exception),
                "In-Remedy failed to start",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
            _closeConfirmed = true;
            Close();
        }
        finally
        {
            _isStarting = false;
        }
    }

    private AppConfig LoadConfig()
    {
        var configPath = Path.Combine(AppContext.BaseDirectory, "InRemedy.config.json");
        if (!File.Exists(configPath))
        {
            throw new InvalidOperationException("InRemedy.config.json was not found.");
        }

        var config = JsonSerializer.Deserialize<AppConfig>(File.ReadAllText(configPath), new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
        });

        if (config is null || string.IsNullOrWhiteSpace(config.AppUrl) || string.IsNullOrWhiteSpace(config.ConnectionString))
        {
            throw new InvalidOperationException("The application configuration is invalid.");
        }

        return config;
    }

    private async Task EnsureBackendRunningAsync(AppConfig config)
    {
        UpdateStatus("Starting local service", "Preparing remediation workspace...");

        if (!await IsEndpointReadyAsync(config.HealthUrl))
        {
            StartBackendProcess(config);
            var ready = await WaitForEndpointAsync(config.HealthUrl, TimeSpan.FromSeconds(120));
            if (!ready)
            {
                throw new InvalidOperationException("The local service did not start in time.");
            }
        }
    }

    private void StartBackendProcess(AppConfig config)
    {
        var exePath = Path.Combine(AppContext.BaseDirectory, "InRemedy.Api.exe");
        if (!File.Exists(exePath))
        {
            throw new InvalidOperationException("InRemedy.Api.exe was not found.");
        }

        Directory.CreateDirectory(_logDirectory);

        _backendProcess = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = exePath,
                Arguments = $"--urls {config.AppUrl}",
                WorkingDirectory = AppContext.BaseDirectory,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
            },
            EnableRaisingEvents = true,
        };
        _backendProcess.StartInfo.Environment["INREMEDY_CONNECTION_STRING"] = config.ConnectionString;
        _backendProcess.StartInfo.Environment["ASPNETCORE_ENVIRONMENT"] = "Production";
        _backendProcess.OutputDataReceived += (_, args) => AppendLogLine("app.out.log", args.Data);
        _backendProcess.ErrorDataReceived += (_, args) => AppendLogLine("app.err.log", args.Data);
        _backendProcess.Start();
        _backendProcess.BeginOutputReadLine();
        _backendProcess.BeginErrorReadLine();
        JobObjectHelper.AssignProcess(_jobHandle, _backendProcess);
    }

    private async Task InitializeWebViewAsync(AppConfig config)
    {
        UpdateStatus("Opening workspace", "Loading the remediation interface...");

        Directory.CreateDirectory(_appDataDirectory);
        var webViewEnvironment = await Microsoft.Web.WebView2.Core.CoreWebView2Environment.CreateAsync(
            userDataFolder: Path.Combine(_appDataDirectory, "WebView2"));
        await _webView.EnsureCoreWebView2Async(webViewEnvironment);
        _webView.CoreWebView2.Settings.AreDefaultContextMenusEnabled = true;
        _webView.CoreWebView2.Settings.AreDevToolsEnabled = false;
        _webView.CoreWebView2.Settings.IsStatusBarEnabled = false;
        _webView.CoreWebView2.NewWindowRequested += (_, args) =>
        {
            args.Handled = true;
            Process.Start(new ProcessStartInfo
            {
                FileName = args.Uri,
                UseShellExecute = true,
            });
        };
        _webView.NavigationCompleted += (_, args) =>
        {
            if (args.IsSuccess)
            {
                _splashPanel.Visible = false;
                _webView.Visible = true;
            }
        };
        _webView.Source = new Uri(config.AppUrl);
    }

    private void UpdateStatus(string title, string subtitle)
    {
        _statusLabel.Text = title;
        _subStatusLabel.Text = subtitle;
    }

    private async Task<bool> IsEndpointReadyAsync(string url)
    {
        try
        {
            using var response = await _httpClient.GetAsync(url);
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    private async Task<bool> WaitForEndpointAsync(string url, TimeSpan timeout)
    {
        var stopwatch = Stopwatch.StartNew();
        while (stopwatch.Elapsed < timeout)
        {
            if (await IsEndpointReadyAsync(url))
            {
                return true;
            }

            await Task.Delay(500);
        }

        return false;
    }

    private async void OnFormClosingAsync(object? sender, FormClosingEventArgs e)
    {
        if (_closeConfirmed)
        {
            return;
        }

        e.Cancel = true;
        var result = MessageBox.Show(
            this,
            "Close In-Remedy and stop the local service?",
            "Exit In-Remedy",
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Question);

        if (result != DialogResult.Yes)
        {
            return;
        }

        _closeConfirmed = true;
        UpdateStatus("Closing In-Remedy", "Stopping local processes...");
        _splashPanel.Visible = true;
        _webView.Visible = false;
        await ShutdownBackendAsync();
        Close();
    }

    private async Task ShutdownBackendAsync()
    {
        if (_config is not null)
        {
            try
            {
                using var request = new HttpRequestMessage(HttpMethod.Post, _config.ShutdownUrl);
                await _httpClient.SendAsync(request);
            }
            catch
            {
            }
        }

        if (_backendProcess is not null && !_backendProcess.HasExited)
        {
            if (!await Task.Run(() => _backendProcess.WaitForExit(5000)))
            {
                _backendProcess.Kill(true);
            }
        }
    }

    private void AppendLogLine(string fileName, string? line)
    {
        if (string.IsNullOrWhiteSpace(line))
        {
            return;
        }

        var path = Path.Combine(_logDirectory, fileName);
        File.AppendAllText(path, line + Environment.NewLine);
    }

    private string BuildStartupErrorMessage(Exception exception)
    {
        var logPath = Path.Combine(_logDirectory, "app.err.log");
        var details = "";

        if (File.Exists(logPath))
        {
            var lines = File.ReadAllLines(logPath).TakeLast(12).ToArray();
            if (lines.Length > 0)
            {
                details = $"{Environment.NewLine}{Environment.NewLine}Latest service log:{Environment.NewLine}{string.Join(Environment.NewLine, lines)}";
            }
        }

        return $"{exception.Message}{Environment.NewLine}{Environment.NewLine}Check that PostgreSQL and WebView2 installed successfully, then try again.{details}";
    }

}

internal sealed class AppConfig
{
    public string AppUrl { get; init; } = "http://127.0.0.1:5180";
    public string ConnectionString { get; init; } = "";

    public string HealthUrl => $"{AppUrl.TrimEnd('/')}/api/admin/health";
    public string ShutdownUrl => $"{AppUrl.TrimEnd('/')}/api/admin/shutdown";
}

internal static class JobObjectHelper
{
    private const int JobObjectExtendedLimitInformation = 9;
    private const uint JobObjectLimitKillOnJobClose = 0x00002000;

    public static SafeJobHandle CreateKillOnCloseJob()
    {
        var handle = CreateJobObject(IntPtr.Zero, null);
        if (handle.IsInvalid)
        {
            throw new InvalidOperationException("Failed to create the process job.");
        }

        var info = new JOBOBJECT_EXTENDED_LIMIT_INFORMATION
        {
            BasicLimitInformation = new JOBOBJECT_BASIC_LIMIT_INFORMATION
            {
                LimitFlags = JobObjectLimitKillOnJobClose,
            },
        };

        var length = Marshal.SizeOf<JOBOBJECT_EXTENDED_LIMIT_INFORMATION>();
        var pointer = Marshal.AllocHGlobal(length);
        try
        {
            Marshal.StructureToPtr(info, pointer, false);
            if (!SetInformationJobObject(handle, JobObjectExtendedLimitInformation, pointer, (uint)length))
            {
                throw new InvalidOperationException("Failed to configure the process job.");
            }
        }
        finally
        {
            Marshal.FreeHGlobal(pointer);
        }

        return handle;
    }

    public static void AssignProcess(SafeJobHandle jobHandle, Process process)
    {
        if (!AssignProcessToJobObject(jobHandle, process.Handle))
        {
            throw new InvalidOperationException("Failed to assign the local service process to the app job.");
        }
    }

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
    private static extern SafeJobHandle CreateJobObject(IntPtr lpJobAttributes, string? lpName);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool SetInformationJobObject(
        SafeJobHandle hJob,
        int JobObjectInfoClass,
        IntPtr lpJobObjectInfo,
        uint cbJobObjectInfoLength);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool AssignProcessToJobObject(SafeJobHandle job, IntPtr process);
}

[StructLayout(LayoutKind.Sequential)]
internal struct JOBOBJECT_BASIC_LIMIT_INFORMATION
{
    public long PerProcessUserTimeLimit;
    public long PerJobUserTimeLimit;
    public uint LimitFlags;
    public nuint MinimumWorkingSetSize;
    public nuint MaximumWorkingSetSize;
    public uint ActiveProcessLimit;
    public nuint Affinity;
    public uint PriorityClass;
    public uint SchedulingClass;
}

[StructLayout(LayoutKind.Sequential)]
internal struct IO_COUNTERS
{
    public ulong ReadOperationCount;
    public ulong WriteOperationCount;
    public ulong OtherOperationCount;
    public ulong ReadTransferCount;
    public ulong WriteTransferCount;
    public ulong OtherTransferCount;
}

[StructLayout(LayoutKind.Sequential)]
internal struct JOBOBJECT_EXTENDED_LIMIT_INFORMATION
{
    public JOBOBJECT_BASIC_LIMIT_INFORMATION BasicLimitInformation;
    public IO_COUNTERS IoInfo;
    public nuint ProcessMemoryLimit;
    public nuint JobMemoryLimit;
    public nuint PeakProcessMemoryUsed;
    public nuint PeakJobMemoryUsed;
}

internal sealed class SafeJobHandle : SafeHandle
{
    public SafeJobHandle() : base(IntPtr.Zero, true)
    {
    }

    public override bool IsInvalid => handle == IntPtr.Zero || handle == new IntPtr(-1);

    protected override bool ReleaseHandle()
    {
        return CloseHandle(handle);
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool CloseHandle(IntPtr hObject);
}
