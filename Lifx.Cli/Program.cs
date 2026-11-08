namespace Lifx.Cli;

public static class Program
{
	private static readonly ILogger Logger = new ConsoleLogger(new()
#if DEBUG
	{ LogLevel = LogLevel.Debug }
#endif
	);

	public static async Task<int> Main(string[] args)
	{
		try
		{
			using CancellationTokenSource cancellationTokenSource = new();
			Console.CancelKeyPress += delegate
			{
				Logger.LogDebug("Ctrl+C pressed");
				cancellationTokenSource.Cancel();
			};

			if ((args ?? throw new ArgumentNullException(nameof(args))).Length == 0)
			{
				args = new[] { "help" };
			}

			using var client = new LifxLanClient(new LifxLanClientOptions
			{
				Logger = Logger
			});
			var devices = new DeviceRegistry(Logger);
			client.Start(cancellationTokenSource.Token);
			devices.Attach(client);
			client.StartDeviceDiscovery();

			var context = new CommandContext(client, devices, args, Logger, cancellationTokenSource.Token);
			await RunModeAsync(args[0], context).ConfigureAwait(false);
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

	private static async Task RunModeAsync(string mode, CommandContext context)
	{
		Func<CommandContext, Task>? command = mode switch
		{
			"discover" => DiscoverCommand.RunAsync,
			"switch" => SwitchCommand.RunAsync,
			"color" => ColorCommand.RunAsync,
			_ => null
		};

		if (command is not null)
		{
			await command(context).ConfigureAwait(false);
			Logger.LogDebug("Exiting");
			return;
		}

		if (mode == "help")
		{
			HelpCommand.Run(context);
			return;
		}

		Logger.LogError("Unsupported mode '{Mode}'", mode);
	}
}
