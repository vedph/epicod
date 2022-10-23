using System.Runtime.InteropServices;

namespace Epicod.Cli
{
    internal static class OsHelper
    {
        public static bool IsWindows() => RuntimeInformation.IsOSPlatform(OSPlatform.Windows);
        public static bool IsMacOS() => RuntimeInformation.IsOSPlatform(OSPlatform.OSX);
        public static bool IsLinux() => RuntimeInformation.IsOSPlatform(OSPlatform.Linux);

        public static string? GetCode()
        {
            if (IsWindows()) return "win";
            if (IsLinux()) return "lin";
            if (IsMacOS()) return "mac";
            return null;
        }
    }
}
