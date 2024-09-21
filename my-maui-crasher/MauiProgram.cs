using System.Diagnostics;
using Microsoft.Extensions.Logging;

namespace my_maui_crasher;

public static class MauiProgram
{
	public static MauiApp CreateMauiApp()
	{
		var builder = MauiApp.CreateBuilder();
		builder
			.UseMauiApp<App>()
			.ConfigureFonts(fonts =>
			{
				fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
				fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
			});

#if DEBUG
		builder.Logging.AddDebug();
#endif

		MauiExceptions.UnhandledException += (sender, args) =>
		{
			Debug.WriteLine("Unhandled Exception: " + args.ExceptionObject);
		};

		return builder.Build();
	}
}
