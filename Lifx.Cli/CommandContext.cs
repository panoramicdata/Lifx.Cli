namespace Lifx.Cli;

internal sealed class CommandContext
{
	public CommandContext(
		LifxLanClient client,
		DeviceRegistry devices,
		string[] args,
		ILogger logger,
		CancellationToken cancellationToken)
	{
		Client = client;
		Devices = devices;
		Args = args;
		Logger = logger;
		CancellationToken = cancellationToken;
	}

	public LifxLanClient Client { get; }

	public DeviceRegistry Devices { get; }

	public IReadOnlyList<string> Args { get; }

	public ILogger Logger { get; }

	public CancellationToken CancellationToken { get; }

	public string GetRequiredArgument(int index, string name)
		=> Args.Count > index
			? Args[index]
			: throw new ArgumentException($"Missing parameter: {name}");

	public TimeSpan GetTransition(int index, string name)
	{
		if (Args.Count <= index)
		{
			Logger.LogDebug("{Message}", $"Missing parameter: {name}.  Using 0");
			return TimeSpan.Zero;
		}

		var transition = TimeSpan.FromMilliseconds(int.TryParse(Args[index], out var milliseconds) ? milliseconds : 0);
		Logger.LogDebug("TransitionTimeSpan {TransitionTimeSpanMs}ms", transition.TotalMilliseconds);
		return transition;
	}

	public async Task<(LightBulb LightBulb, LightStateResponse LightState)> FindLightAsync(string hostName)
	{
		var device = await Devices.WaitForDeviceAsync(hostName, CancellationToken).ConfigureAwait(false);
		return await ReadLightAsync(device).ConfigureAwait(false);
	}

	public async Task<(LightBulb LightBulb, LightStateResponse LightState)> ReadLightAsync(Device device)
	{
		var lightBulb = new LightBulb(device.HostName, device.MacAddress, device.Service, device.Port);
		var lightState = await Client.GetLightStateAsync(lightBulb).ConfigureAwait(false);
		return (lightBulb, lightState);
	}
}
