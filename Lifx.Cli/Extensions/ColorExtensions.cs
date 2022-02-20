using Color = System.Drawing.Color;

namespace Lifx.Cli.Extensions
{
	public static class ColorExtensions
	{
		/// <summary>
		/// Convert to an HTML RGB color string (6 characters)
		/// </summary>
		/// <param name="color"></param>
		public static string ToHex(this Color color)
		{
			return $"#{color.R:X2}{color.G:X2}{color.B:X2}";
		}

		/// <summary>
		/// Convert to a Color from text
		/// </summary>
		public static Color? FromText(string text)
		{
			if (text == null)
			{
				return null;
			}

			// Color.FromName returns black if it's not a known color
			if (new[] { "BLACK", "AUTO" }.Contains(text.ToUpperInvariant()))
			{
				return Color.FromName("black");
			}
			// The string is not "black"

			var color = Color.FromName(text);

			// RM-7370: label font colour has no effect. Using "Red" would result in R as 255, G as 0, B as 0, which skipped
			// this (as there was && here not ||), then tried to parse from Argb, which failed, leaving black in the ChartProcessingEngine's
			// GetTextAnnotation method
			if (color.R != 0 || color.G != 0 || color.B != 0)
			{
				return color;
			}

			// Failed to parse. Let's try HEX.
			try
			{
				// >>> We may update this so that you can do RGB or ARGB rather than RRGGBB or AARRGGBB
				return text.Replace("#", string.Empty, StringComparison.Ordinal).Length == 8
					? Color.FromArgb(int.Parse(text.Replace("#", string.Empty, StringComparison.Ordinal), NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture))
					: Color.FromArgb(int.Parse($"FF{text.Replace("#", string.Empty, StringComparison.Ordinal)}", NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture));
			}
			catch
			{
				throw new FormatException($"Not a valid text or hex color: {text}");
			}
		}
	}
}
