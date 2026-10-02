using LibUsbDotNet;
using LibUsbDotNet.Main;
using TVSecurityToolkit.Core.Exceptions;
using TVSecurityToolkit.Core.Interfaces;
using TVSecurityToolkit.Core.Models;

namespace TVSecurityToolkit.Device.Communication;

/// <summary>Bulk-endpoint USB transport (LibUsbDotNet). On Windows the device needs a WinUSB/libusb driver (e.g. via Zadig).</summary>
public sealed class UsbTransport : IDeviceTransport
{
    private readonly ConnectionSettings _s;
    private UsbDevice? _dev;
    private UsbEndpointReader? _reader;
    private UsbEndpointWriter? _writer;

    public UsbTransport(ConnectionSettings settings) => _s = settings;
    public string Name => $"usb {_s.Vid:x4}:{_s.Pid:x4}";

    public Task OpenAsync(CancellationToken ct) => Task.Run(() =>
    {
        _dev = UsbDevice.OpenUsbDevice(new UsbDeviceFinder(_s.Vid, _s.Pid))
               ?? throw new DeviceException($"USB device {_s.Vid:x4}:{_s.Pid:x4} not found");
        if (_dev is IUsbDevice whole) { whole.SetConfiguration(1); whole.ClaimInterface(_s.Interface); }
        _reader = _dev.OpenEndpointReader((ReadEndpointID)_s.EpIn);
        _writer = _dev.OpenEndpointWriter((WriteEndpointID)_s.EpOut);
    }, ct);

    public Task WriteAsync(byte[] data, CancellationToken ct) => Task.Run(() =>
    {
        var ec = _writer!.Write(data, _s.TimeoutMs, out _);
        if (ec != ErrorCode.None) throw new DeviceException("USB write failed: " + ec);
    }, ct);

    public Task<int> ReadAsync(byte[] buffer, int timeoutMs, CancellationToken ct) => Task.Run(() =>
    {
        var ec = _reader!.Read(buffer, timeoutMs, out var n);
        if (ec == ErrorCode.IoTimedOut) return 0;
        if (ec != ErrorCode.None) throw new DeviceException("USB read failed: " + ec);
        return n;
    }, ct);

    public Task CloseAsync()
    {
        if (_dev is not null)
        {
            if (_dev.IsOpen)
            {
                if (_dev is IUsbDevice whole) whole.ReleaseInterface(_s.Interface);
                _dev.Close();
            }
            _dev = null;
        }
        UsbDevice.Exit();
        return Task.CompletedTask;
    }

    public async ValueTask DisposeAsync() => await CloseAsync();
}
