using System.Diagnostics;
using BugSplatDotNetStandard;
using Microsoft.Extensions.Logging;

namespace my_maui_crasher;

public static class MauiProgram
{
	public static MauiApp CreateMauiApp()
	{
		var bugsplat = new BugSplat("fred", "my-maui-crasher", "1.0");

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

			#if WINDOWS
			var path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "crash.dmp");
			MiniDumpWriter.WriteMiniDump(path);
			bugsplat.MinidumpType = BugSplat.MinidumpTypeId.DotNet;
			Task.Run(() => bugsplat.Post(new FileInfo(path), new MinidumpPostOptions())).Wait();
			#else
			Task.Run(() => bugsplat.Post((Exception)args.ExceptionObject)).Wait();
			#endif

			Debug.WriteLine("Done!");
		};

		return builder.Build();
	}
}
