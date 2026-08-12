using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

namespace Sunrise.Installer.Services;

public sealed class ConsoleWindow : IDisposable
{
    private const int StdInputHandle = -10;
    private const int StdOutputHandle = -11;
    private const int StdErrorHandle = -12;
    private const uint EnableVirtualTerminalProcessing = 0x0004;
    private bool allocated;

    public ConsoleWindow(string title)
    {
        allocated = AllocConsole();
        if (!allocated)
        {
            return;
        }

        SetConsoleTitle(title);
        ConfigureStreams();
        EnableAnsiOutput();
    }

    public void Dispose()
    {
        if (allocated)
        {
            Console.Out.Flush();
            Console.Error.Flush();
            FreeConsole();
            allocated = false;
        }
    }

    private static void ConfigureStreams()
    {
        Console.SetIn(new StreamReader(OpenHandle(StdInputHandle)));
        Console.SetOut(new StreamWriter(OpenHandle(StdOutputHandle)) { AutoFlush = true });
        Console.SetError(new StreamWriter(OpenHandle(StdErrorHandle)) { AutoFlush = true });
        Console.OutputEncoding = System.Text.Encoding.UTF8;
    }

    private static FileStream OpenHandle(int handleId)
    {
        IntPtr handle = GetStdHandle(handleId);
        SafeFileHandle safeHandle = new(handle, ownsHandle: false);
        FileAccess access = handleId == StdInputHandle ? FileAccess.Read : FileAccess.Write;
        return new FileStream(safeHandle, access);
    }

    private static void EnableAnsiOutput()
    {
        IntPtr output = GetStdHandle(StdOutputHandle);
        if (GetConsoleMode(output, out uint mode))
        {
            SetConsoleMode(output, mode | EnableVirtualTerminalProcessing);
        }
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool AllocConsole();

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool FreeConsole();

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetConsoleTitle(string title);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr GetStdHandle(int handleId);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetConsoleMode(IntPtr handle, out uint mode);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetConsoleMode(IntPtr handle, uint mode);
}
