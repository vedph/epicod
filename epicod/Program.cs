using Serilog;
using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace Epicod.Cli
{
    public static class Program
    {
#if DEBUG
        private static void DeleteLogs()
        {
            foreach (var path in Directory.EnumerateFiles(
                AppDomain.CurrentDomain.BaseDirectory, "epicod-log*.txt"))
            {
                try
                {
                    File.Delete(path);
                }
                catch (Exception ex)
                {
                    Debug.WriteLine(ex.ToString());
                }
            }
        }
#endif

        public static int Main(string[] args)
        {
            try
            {
                // https://github.com/serilog/serilog-sinks-file
                string logFilePath = Path.Combine(
                    Path.GetDirectoryName(Assembly.GetExecutingAssembly()?.Location) ?? "",
                    "epicod-log.txt");
                Log.Logger = new LoggerConfiguration()
#if DEBUG
                    .MinimumLevel.Debug()
#else
                    .MinimumLevel.Information()
#endif
                    .Enrich.FromLogContext()
                    .WriteTo.File(logFilePath, rollingInterval: RollingInterval.Day)
                    .CreateLogger();

#if DEBUG
                DeleteLogs();
#endif

                Console.OutputEncoding = Encoding.Unicode;
                Stopwatch stopwatch = new();
                stopwatch.Start();

                Task.Run(async () =>
                {
                    AppOptions? options = AppOptions.Parse(args);
                    if (options?.Command == null)
                    {
                        // RootCommand will have printed help
                        return 1;
                    }

                    Console.Clear();
                    await options.Command.Run();
                    return 0;
                }).Wait();

                Console.ResetColor();
                Console.CursorVisible = true;
                Console.WriteLine();
                Console.WriteLine();

                stopwatch.Stop();
                if (stopwatch.ElapsedMilliseconds > 1000)
                {
                    Console.WriteLine("\nTime: {0}d{1}h{2}'{3}\"",
                        stopwatch.Elapsed.Days,
                        stopwatch.Elapsed.Hours,
                        stopwatch.Elapsed.Minutes,
                        stopwatch.Elapsed.Seconds);
                }

                return 0;
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex.ToString());
                Console.CursorVisible = true;
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine(ex.ToString());
                Console.ResetColor();
                return 2;
            }
        }
    }
}
