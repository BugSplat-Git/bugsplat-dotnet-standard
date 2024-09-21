#if WINDOWS
using System;
using System.Runtime.InteropServices;
using System.IO;
using System.Diagnostics;

public static class MiniDumpWriter
{
    [DllImport("Dbghelp.dll")]
    private static extern bool MiniDumpWriteDump(IntPtr hProcess, uint ProcessId, IntPtr hFile, int DumpType, IntPtr ExceptionParam, IntPtr UserStreamParam, IntPtr CallbackParam);

    public static void WriteMiniDump(string filePath)
    {
        using (FileStream fs = new FileStream(filePath, FileMode.Create, FileAccess.ReadWrite, FileShare.Write))
        {
            Process currentProcess = Process.GetCurrentProcess();

            MiniDumpWriteDump(
                currentProcess.Handle,
                (uint)currentProcess.Id,
                fs.SafeFileHandle.DangerousGetHandle(),
                (int)MINIDUMP_TYPE.MiniDumpNormal,
                IntPtr.Zero,
                IntPtr.Zero,
                IntPtr.Zero);
        }
    }

    [Flags]
    private enum MINIDUMP_TYPE
    {
        MiniDumpNormal = 0x00000000,
        MiniDumpWithFullMemory = 0x00000002,
        // Add more flags as needed
    }
}

#endif