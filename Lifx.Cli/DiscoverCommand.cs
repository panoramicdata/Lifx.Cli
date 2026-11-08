namespace Lifx.Cli;

internal static class DiscoverCommand
{
	private const int DefaultDiscoveryTimeSeconds = 5;

	public static async Task RunAsync(CommandContext context)
	{
		var discoveryTime = TimeSpan.FromSeconds(GetDiscoveryTimeSeconds(context));
		await WaitForDiscoveryAsync(context, discoveryTime).ConfigureAwait(false);

		foreach (var device in context.Devices.Devices)
		{
			var (_, lightState) = await context.ReadLightAsync(device).ConfigureAwait(false);
			context.Logger.LogInformation("Found: {DeviceHostName} (mac={DeviceMacAddress}, service={DeviceService}), Called: {Label}, BSHK=({Brightness}, {Saturation}, {Hue}, {Kelvin})",
				device.HostName,
				device.MacAddressName,
				device.Service,
				lightState.Label,
				lightState.Brightness,
				lightState.Saturation,
				lightState.Hue,
				lightState.Kelvin);
		}
	}

	private static int GetDiscoveryTimeSeconds(CommandContext context)
	{
		if (context.Args.Count > 1 && int.TryParse(context.Args[1], out var discoveryTimeSeconds))
		{
			context.Logger.LogDebug("Discovery time: {DiscoveryTimeSeconds}s", discoveryTimeSeconds);
			return discoveryTimeSeconds;
		}

		context.Logger.LogDebug("Discovery time: {DiscoveryTimeSeconds}s (default)", DefaultDiscoveryTimeSeconds);
		return DefaultDiscoveryTimeSeconds;
	}

	// Main has already started the client and discovery; starting either again throws "Already started".
	private static async Task WaitForDiscoveryAsync(CommandContext context, TimeSpan discoveryTime)
	{
		try
		{
			await Task.Delay(discoveryTime, context.CancellationToken).ConfigureAwait(false);
			context.Logger.LogDebug("{Message}", "Timed out waiting for discovery");
		}
		finally
		{
			context.Client.StopDeviceDiscovery();
		}
	}
}
