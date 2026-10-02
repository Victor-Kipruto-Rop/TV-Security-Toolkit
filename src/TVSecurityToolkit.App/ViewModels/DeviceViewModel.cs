using System.Collections.ObjectModel;
using System.Globalization;
using TVSecurityToolkit.App.Commands;
using TVSecurityToolkit.App.Services;
using TVSecurityToolkit.Core.Enums;
using TVSecurityToolkit.Core.Models;
using TVSecurityToolkit.Device.Discovery;
using TVSecurityToolkit.Device.PlatformAdapters;
using Serilog;

namespace TVSecurityToolkit.App.ViewModels;

public sealed class DeviceViewModel : ViewModelBase
{
    private readonly ApplicationService _app;
    private ConnectionType _type = ConnectionType.Simulator;
    private string _vid = "", _pid = "", _epOut = "1", _epIn = "129", _serialPort = "COM3", _host = "127.0.0.1", _port = "5000", _flaws = "";
    private string _status = "Not connected";
    private string _rokuHost = "";
    private string _rokuStatus = "No Roku endpoint checked.";

    public DeviceViewModel(ApplicationService app)
    {
        _app = app;
        ConnectCommand = new AsyncRelayCommand(_ => ConnectAsync(), null, OnError);
        DisconnectCommand = new AsyncRelayCommand(async _ => { await app.DisconnectAsync(); Status = "Not connected"; }, null, OnError);
        DiscoverCommand = new AsyncRelayCommand(_ => DiscoverAsync(), null, OnError);
        CheckPlatformToolsCommand = new AsyncRelayCommand(_ => CheckPlatformToolsAsync(), null, OnError);
        CheckRokuCommand = new AsyncRelayCommand(_ => CheckRokuAsync(), null, OnRokuError);
    }

    public Array ConnectionTypes { get; } = Enum.GetValues(typeof(ConnectionType));
    public ObservableCollection<string> Discovered { get; } = new();
    public ObservableCollection<PlatformAdapterStatus> PlatformAdapters { get; } = new();
    public AsyncRelayCommand ConnectCommand { get; }
    public AsyncRelayCommand DisconnectCommand { get; }
    public AsyncRelayCommand DiscoverCommand { get; }
    public AsyncRelayCommand CheckPlatformToolsCommand { get; }
    public AsyncRelayCommand CheckRokuCommand { get; }

    public ConnectionType SelectedType { get => _type; set { if (Set(ref _type, value)) { Raise(nameof(IsUsb)); Raise(nameof(IsSerial)); Raise(nameof(IsNetwork)); Raise(nameof(IsSimulator)); } } }
    public bool IsUsb => _type == ConnectionType.Usb;
    public bool IsSerial => _type == ConnectionType.Serial;
    public bool IsNetwork => _type == ConnectionType.Network;
    public bool IsSimulator => _type == ConnectionType.Simulator;
    public string Vid { get => _vid; set => Set(ref _vid, value); }
    public string Pid { get => _pid; set => Set(ref _pid, value); }
    public string EpOut { get => _epOut; set => Set(ref _epOut, value); }
    public string EpIn { get => _epIn; set => Set(ref _epIn, value); }
    public string SerialPortName { get => _serialPort; set => Set(ref _serialPort, value); }
    public string Host { get => _host; set => Set(ref _host, value); }
    public string Port { get => _port; set => Set(ref _port, value); }
    public string Flaws { get => _flaws; set => Set(ref _flaws, value); }
    public string Status { get => _status; private set => Set(ref _status, value); }
    public string RokuHost { get => _rokuHost; set => Set(ref _rokuHost, value); }
    public string RokuStatus { get => _rokuStatus; private set => Set(ref _rokuStatus, value); }

    private static int ParseInt(string s) =>
        s.StartsWith("0x", StringComparison.OrdinalIgnoreCase) ? int.Parse(s[2..], NumberStyles.HexNumber) : int.Parse(s.Length == 0 ? "0" : s);

    private async Task ConnectAsync()
    {
        Status = "Connecting...";
        var s = new ConnectionSettings
        {
            Type = _type, Vid = ParseInt(_vid), Pid = ParseInt(_pid), EpOut = ParseInt(_epOut), EpIn = ParseInt(_epIn),
            SerialPort = _serialPort, Host = _host, Port = ParseInt(_port), Flaws = _flaws
        };
        await _app.ConnectAsync(s, CancellationToken.None);
        var i = _app.DeviceInfo!;
        Status = i.Connection == ConnectionType.Simulator
            ? $"Connected to simulator: {i.Identity?.Model}, firmware {i.Firmware?.Version}"
            : $"Transport connected: {i.Identity?.Model} ({i.Identity?.DeviceId}), firmware {i.Firmware?.Version}. Identity is NOT vendor-authenticated; test execution is blocked.";
        _app.Notifications.Notify(Status);
    }

    private async Task DiscoverAsync()
    {
        Discovered.Clear();
        var errors = new List<string>();
        var found = await new DeviceDiscovery().DiscoverAllAsync(
            new[] { $"{_host}:{_port}" },
            CancellationToken.None,
            (source, error) =>
            {
                errors.Add($"{source}: {error.GetType().Name}");
                Log.Warning("Device discovery source failed for {Source}; error type {ErrorType}",
                    source, error.GetType().Name);
            });
        foreach (var error in errors) Discovered.Add("Warning: " + error);
        foreach (var d in found) Discovered.Add($"{d.Type}  {d.Id}  {d.Description}");
        Log.Information("Device discovery completed with {DeviceCount} candidates", found.Count);
        if (found.Count == 0) Discovered.Add("No devices found. Reachability does not authenticate identity or establish compatibility.");
    }

    private async Task CheckPlatformToolsAsync()
    {
        PlatformAdapters.Clear();
        PlatformAdapters.Add(new PlatformAdapterStatus("Platform tools", "", "Checking read-only device-list commands..."));
        var results = await new PlatformAdapterDiscovery().DiscoverAsync(CancellationToken.None);
        PlatformAdapters.Clear();
        foreach (var result in results)
        {
            PlatformAdapters.Add(result);
            Log.Information("Platform adapter discovery for {Platform} via {Tool}: {Summary}",
                result.Platform, result.Tool, result.Summary);
        }
    }

    private async Task CheckRokuAsync()
    {
        RokuStatus = "Checking the explicitly entered host only...";
        var result = await new RokuEcpAdapter().ProbeAsync(RokuHost, CancellationToken.None);
        RokuStatus = $"{result.Summary} Discovery does not scan the network; test execution remains blocked.";
        Log.Information("Roku read-only ECP probe completed");
    }

    private void OnError(Exception e)
    {
        Log.Error("Device operation failed; error type {ErrorType}", e.GetType().Name);
        Status = "Error: " + e.Message;
        _app.Dialogs.ShowError(e.Message);
    }

    private void OnRokuError(Exception e)
    {
        Log.Error("Roku read-only discovery failed; error type {ErrorType}", e.GetType().Name);
        RokuStatus = "Error: " + e.Message;
        _app.Dialogs.ShowError(e.Message);
    }
}
