namespace Lifx.Cli;

internal static class HelpCommand
{
	public static void Run(CommandContext context)
	{
		if (context.Args.Count <= 1)
		{
			throw new ArgumentException("Missing mode");
		}

		var mode = context.Args[1];
		switch (mode)
		{
			case "discover":
				context.Logger.LogInformation(@"Usage:
Lifx.Cli.exe discover [maxDiscoveryTimeInSeconds, default=10]");
				return;
			case "set":
				context.Logger.LogInformation(@"Usage:
Lifx.Cli.exe set <deviceHostname> <deviceMacAddress> <on|off>");
				return;
			default:
				context.Logger.LogError(@"Unsupported help mode '{Mode}'", mode);
				return;
		}
	}
}
