namespace Lifx.Cli;

public static class Program
{
	private static readonly Dictionary<string, Device> _devices = new();

	private static readonly ILogger Logger = new ConsoleLogger(new()
#if DEBUG
	{ LogLevel = LogLevel.Debug }
#endif
	);

	public static async Task<int> Main(string[] args)
	{
		try
		{
			// Set up cancellation
			using CancellationTokenSource CancellationTokenSource = new();
			var cancellationToken = CancellationTokenSource.Token;
			Console.CancelKeyPress += delegate
			{
				Logger.LogDebug("Ctrl+C pressed");
				CancellationTokenSource.Cancel();
			};

			// Determine the mode
			if ((args ?? throw new ArgumentNullException(nameof(args))).Length == 0)
			{
				args = new string[] { "help" };
			}
			var mode = args[0];

			switch (mode)
			{
				case "discover":
					await DiscoveryModeAsync(args, cancellationToken)
						.ConfigureAwait(false);
					Logger.LogDebug("Exiting");
					break;
				case "set":
					await SetModeAsync(args, cancellationToken)
						.ConfigureAwait(false);
					Logger.LogDebug("Exiting");
					break;
				case "help":
					HelpMode(args);
					break;
				default:
					Logger.LogError("Unsupported mode '{Mode}'", mode);
					break;
			}

			return 0;
		}
		catch (TaskCanceledException exception)
		{
			Logger.LogError(exception, "Timeout: {Message}", exception.Message);
			return 1;
		}
		catch (ArgumentException exception)
		{
			Logger.LogError(exception, "Usage incorrect: {Message}", exception.Message);
			return 2;
		}
	}

	private static async Task SetModeAsync(string[] args, CancellationToken cancellationToken)
	{
		using var client = new LifxLanClient(new LifxLanClientOptions {
			Logger = Logger
		});
		client.Start(cancellationToken);
		client.DeviceDiscovered += Client_DeviceDiscovered;
		client.DeviceLost += Client_DeviceLost;
		client.StartDeviceDiscovery();

		if (args.Length < 2)
		{
			throw new ArgumentException("Missing parameter: deviceHostname");
		}

		var deviceHostName = args[1];
		Logger.LogDebug("Device hostname: {DeviceHostname}", deviceHostName);

		if (args.Length < 3)
		{
			throw new ArgumentException("Missing parameter: desiredState");
		}

		var desiredState = args[2];
		Logger.LogDebug("Desired state: {DesiredState}", desiredState);

		TimeSpan transitionTimeSpan;
		if (args.Length < 4)
		{
			Logger.LogDebug("{Message}", "Missing parameter: transitionTimeSpan.  Using 0");
			transitionTimeSpan = TimeSpan.Zero;
		}
		else
		{
			transitionTimeSpan = TimeSpan.FromMilliseconds(int.TryParse(args[4], out var transitionTimeSpanSeconds) ? transitionTimeSpanSeconds : 0);
			Logger.LogDebug("TransitionTimeSpan {TransitionTimeSpanMs}ms", transitionTimeSpan.TotalMilliseconds);
		}

		var stopwatch = Stopwatch.StartNew();
		Device? device;
		while (!_devices.TryGetValue(deviceHostName, out device))
		{
			await Task.Delay(TimeSpan.FromMilliseconds(100), cancellationToken).ConfigureAwait(false);
			Logger.LogTrace("{Message}", "Waiting to find device...");
			if (stopwatch.ElapsedMilliseconds > 5000)
			{
				throw new TimeoutException("Timed out waiting to find device.");
			}
		}
		// We found it

		// Create a lightbulb
		var lightbulb = new LightBulb(device.HostName, device.MacAddress, device.Service, device.Port);

		// Determine the current start
		var lightState = await client.GetLightStateAsync(lightbulb)
			.ConfigureAwait(false);

		var lightLabel = lightState.Label;

		// Determine the desired state
		switch (desiredState)
		{
			case "on":
				Logger.LogInformation("Request: 'Switch {LightLabel} {DesiredState}'", lightLabel, desiredState);
				if (!lightState.IsOn)
				{
					await client
						.TurnBulbOnAsync(lightbulb, transitionTimeSpan)
						.ConfigureAwait(false);
					Logger.LogDebug("{Message}", "Done");
				}
				else
				{
					Logger.LogInformation("Light is already {DesiredState}.  Doing nothing.", desiredState);
				}

				break;
			case "off":
				Logger.LogInformation("Request: 'Switch {LightLabel} {DesiredState}'", lightLabel, desiredState);
				if (lightState.IsOn)
				{
					await client
					.TurnBulbOffAsync(lightbulb, transitionTimeSpan)
					.ConfigureAwait(false);
					Logger.LogDebug("{Message}", "Done");
				}
				else
				{
					Logger.LogInformation("Light is already {DesiredState}.  Doing nothing.", desiredState);
				}

				break;
			case "toggle":
				Logger.LogInformation("Request: 'Switch {LightLabel} {DesiredState}'", lightLabel, desiredState);
				if (lightState.IsOn)
				{
					await client
					.TurnBulbOffAsync(lightbulb, transitionTimeSpan)
					.ConfigureAwait(false);
				}
				else
				{
					await client
					.TurnBulbOnAsync(lightbulb, transitionTimeSpan)
					.ConfigureAwait(false);
				}

				Logger.LogDebug("{Message}", "Done");
				break;
			case "color":
				if (args.Length < 5)
				{
					throw new ArgumentException("Missing parameter: desiredColor");
				}

				var desiredColorText = args[4];
				var desiredColor = ColorExtensions.FromText(desiredColorText)
					?? throw new ArgumentException($"Could not determine color from '{desiredColorText}'");
				var desiredLifxColor = new Color {
					R = desiredColor.R,
					G = desiredColor.G,
					B = desiredColor.B
				};
				if (args.Length < 6)
				{
					throw new ArgumentException("Missing parameter: desiredKelvin");
				}

				var desiredKelvinText = args[5];
				if (!ushort.TryParse(desiredKelvinText, out var desiredKelvin))
				{
					throw new ArgumentException($"Non-ushort parameter: desiredKelvin '{desiredKelvinText}'");
				}

				Logger.LogInformation("Request: 'Switch {LightLabel} {DesiredState} {DesiredColor} {DesiredKelvin}'", lightLabel, desiredState, desiredColorText, desiredKelvin);
				await client
				.SetColorAsync(lightbulb, desiredLifxColor, desiredKelvin, transitionTimeSpan)
				.ConfigureAwait(false);
				Logger.LogDebug("{Message}", "Done");

				break;
			default:
				Logger.LogError("Unsupported state: {DesiredState}", desiredState);
				return;
		}
	}

	private static void HelpMode(string[] args)
	{
		if (args.Length <= 1)
		{
			throw new ArgumentException("Missing mode");
		}

		var mode = args[1];
		switch (mode)
		{
			case "discover":
				Logger.LogInformation(@"Usage:
Lifx.Cli.exe discover [maxDiscoveryTimeInSeconds, default=10]");
				return;
			case "set":
				Logger.LogInformation(@"Usage:
Lifx.Cli.exe set <deviceHostname> <deviceMacAddress> <on|off>");
				return;
			default:
				Logger.LogError(@"Unsupported help mode '{Mode}'", mode);
				return;
		}
	}

	private static async Task DiscoveryModeAsync(string[] args, CancellationToken cancellationToken)
	{
		if (args.Length > 1 && int.TryParse(args[1], out var discoveryTimeSeconds))
		{
			Logger.LogDebug("Discovery time: {DiscoveryTimeSeconds}s", discoveryTimeSeconds);
		}
		else
		{
			discoveryTimeSeconds = 10;
			Logger.LogDebug("Discovery time: {DiscoveryTimeSeconds}s (default)", discoveryTimeSeconds);
		}

		await DiscoverAsync(TimeSpan.FromSeconds(discoveryTimeSeconds), cancellationToken)
			.ConfigureAwait(false);
	}

	private static async Task DiscoverAsync(TimeSpan discoveryTime, CancellationToken cancellationToken)
	{
		using var client = new LifxLanClient(new LifxLanClientOptions {
			Logger = Logger
		});
		client.Start(cancellationToken);
		client.DeviceDiscovered += Client_DeviceDiscovered;
		client.DeviceLost += Client_DeviceLost;
		client.StartDeviceDiscovery();
		try
		{
			// Wait for 10s or until Ctrl+C is pressed
			await Task.Delay(discoveryTime, cancellationToken).ConfigureAwait(false);
			Logger.LogDebug("{Message}", "Timed out waiting for discovery");
		}
		finally
		{
			// We should not have to do this.
			client.StopDeviceDiscovery();
		}
	}

	private static void Client_DeviceDiscovered(object? sender, LifxLanClient.DeviceDiscoveryEventArgs deviceDiscoveryEvent)
	{
		var device = deviceDiscoveryEvent.Device;
		Logger.LogInformation("Found: {DeviceHostName} (mac={DeviceMacAddress}, service={DeviceService})",
			device.HostName,
			device.MacAddressName,
			device.Service);
		_devices[device.HostName] = device;
	}

	private static void Client_DeviceLost(object? sender, LifxLanClient.DeviceDiscoveryEventArgs deviceLostEvent)
	{
		var device = deviceLostEvent.Device;
		Logger.LogInformation("Lost: {DeviceHostName} (mac={DeviceMacAddress}, service={DeviceService})",
			device.HostName,
			device.MacAddressName,
			device.Service);
		_devices.Remove(device.HostName);
	}
}