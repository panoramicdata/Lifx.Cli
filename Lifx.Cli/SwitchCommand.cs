namespace Lifx.Cli;

internal static class SwitchCommand
{
	public static async Task RunAsync(CommandContext context)
	{
		var logger = context.Logger;
		var deviceHostName = context.GetRequiredArgument(1, "deviceHostname");
		logger.LogDebug("Device hostname: {DeviceHostname}", deviceHostName);
		var desiredState = context.GetRequiredArgument(2, "desiredState");
		logger.LogDebug("Desired state: {DesiredState}", desiredState);
		var transition = context.GetTransition(3, "transitionTimeSpan");

		var (lightBulb, lightState) = await context.FindLightAsync(deviceHostName).ConfigureAwait(false);

		bool? turnOn = desiredState switch
		{
			"on" => true,
			"off" => false,
			"toggle" => !lightState.IsOn,
			_ => null
		};

		if (turnOn is null)
		{
			logger.LogError("Unsupported state: {DesiredState}", desiredState);
			return;
		}

		logger.LogInformation("Request: 'Switch {LightLabel} {DesiredState}'", lightState.Label, desiredState);
		if (turnOn.Value == lightState.IsOn)
		{
			logger.LogInformation("Light is already {DesiredState}.  Doing nothing.", desiredState);
			return;
		}

		var switchTask = turnOn.Value
			? context.Client.TurnBulbOnAsync(lightBulb, transition)
			: context.Client.TurnBulbOffAsync(lightBulb, transition);
		await switchTask.ConfigureAwait(false);
		logger.LogDebug("{Message}", "Done");
	}
}
