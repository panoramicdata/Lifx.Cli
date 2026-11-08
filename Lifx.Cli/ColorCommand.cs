namespace Lifx.Cli;

internal static class ColorCommand
{
	public static async Task RunAsync(CommandContext context)
	{
		var logger = context.Logger;
		var deviceHostName = context.GetRequiredArgument(1, "deviceHostname");
		logger.LogDebug("Device hostname: {DeviceHostname}", deviceHostName);
		var desiredColorText = context.GetRequiredArgument(2, "desiredColor");
		var desiredColor = ParseColor(desiredColorText);
		var desiredKelvin = ParseKelvin(context.GetRequiredArgument(3, "desiredKelvin"));
		var transition = context.GetTransition(4, "transitionTimeSpanMs");

		var (lightBulb, lightState) = await context.FindLightAsync(deviceHostName).ConfigureAwait(false);

		logger.LogInformation("Request: 'Color {LightLabel} {DesiredColor} {DesiredKelvin}'", lightState.Label, desiredColorText, desiredKelvin);
		await context.Client
			.SetColorAsync(lightBulb, desiredColor, desiredKelvin, transition)
			.ConfigureAwait(false);
		logger.LogDebug("{Message}", "Done");
	}

	private static Color ParseColor(string text)
	{
		var color = ColorExtensions.FromText(text)
			?? throw new ArgumentException($"Could not determine color from '{text}'");
		return new Color
		{
			R = color.R,
			G = color.G,
			B = color.B
		};
	}

	private static ushort ParseKelvin(string text)
		=> ushort.TryParse(text, out var kelvin)
			? kelvin
			: throw new ArgumentException($"Non-ushort parameter: desiredKelvin '{text}'");
}
