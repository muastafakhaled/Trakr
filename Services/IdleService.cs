using System;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace JiraTimeTracker.Services;

public class IdleService
{
    [DllImport("user32.dll")]
    private static extern bool GetLastInputInfo(ref LASTINPUTINFO plii);

    [StructLayout(LayoutKind.Sequential)]
    private struct LASTINPUTINFO
    {
        public uint cbSize;
        public uint dwTime;
    }

    public int GetIdleSeconds()
    {
        var info = new LASTINPUTINFO { cbSize = (uint)Marshal.SizeOf<LASTINPUTINFO>() };
        GetLastInputInfo(ref info);
        return (int)((Environment.TickCount - info.dwTime) / 1000);
    }

    public bool IsPCLocked()
    {
        var procs = Process.GetProcessesByName("LogonUI");
        return procs.Length > 0;
    }
}