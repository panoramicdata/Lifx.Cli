namespace Lifx.Cli;

internal sealed class DeviceRegistry
{
	private const int FindDeviceTimeoutMilliseconds = 10000;
	private static readonly TimeSpan PollInterval = TimeSpan.FromMilliseconds(100);

	private readonly Dictionary<string, Device> _devices = new();
	private readonly ILogger _logger;

	public DeviceRegistry(ILogger logger)
	{
		_logger = logger;
	}

	// A snapshot, because discovery events can change the dictionary while callers iterate.
	public IReadOnlyList<Device> Devices => _devices.Values.ToList();

	public void Attach(LifxLanClient client)
	{
		client.DeviceDiscovered += OnDeviceDiscovered;
		client.DeviceLost += OnDeviceLost;
	}

	public async Task<Device> WaitForDeviceAsync(string hostName, CancellationToken cancellationToken)
	{
		var stopwatch = Stopwatch.StartNew();
		Device? device;
		while (!_devices.TryGetValue(hostName, out device))
		{
			await Task.Delay(PollInterval, cancellationToken).ConfigureAwait(false);
			_logger.LogTrace("{Message}", "Waiting to find device...");
			if (stopwatch.ElapsedMilliseconds > FindDeviceTimeoutMilliseconds)
			{
				throw new TimeoutException("Timed out waiting to find device.");
			}
		}

		return device;
	}

	private void OnDeviceDiscovered(object? sender, LifxLanClient.DeviceDiscoveryEventArgs deviceDiscoveryEvent)
	{
		var device = deviceDiscoveryEvent.Device;
		LogDevice("Found", device);
		_devices[device.HostName] = device;
	}

	private void OnDeviceLost(object? sender, LifxLanClient.DeviceDiscoveryEventArgs deviceLostEvent)
	{
		var device = deviceLostEvent.Device;
		LogDevice("Lost", device);
		_devices.Remove(device.HostName);
	}

	private void LogDevice(string eventName, Device device)
		=> _logger.LogDebug("{Event}: {DeviceHostName} (mac={DeviceMacAddress}, service={DeviceService})",
			eventName,
			device.HostName,
			device.MacAddressName,
			device.Service);
}
