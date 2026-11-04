// Synthetic Job Object experiment. For a GitHub-hosted disposable Windows runner only.
using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;

internal static class Program
{
    const uint SUSPENDED = 0x00000004, NO_WINDOW = 0x08000000;
    const uint WAIT_OK = 0, WAIT_TIMEOUT = 0x102;
    const int ACCOUNTING = 1;
    static int checks;

    static int Main(string[] args)
    {
        if (args.Length == 1 && args[0] == "--worker") { Thread.Sleep(4000); return 0; }
        if (args.Length != 0) return 2;
        try
        {
            Check(OperatingSystem.IsWindows() &&
                  Environment.GetEnvironmentVariable("GITHUB_ACTIONS") == "true" &&
                  Environment.GetEnvironmentVariable("RUNNER_OS") == "Windows" &&
                  Environment.GetEnvironmentVariable("RUNNER_ENVIRONMENT") == "github-hosted" &&
                  Environment.GetEnvironmentVariable("SYNTHETIC_WINJOB_APPROVED") == "true", "runner_guard");
            IntPtr job = Win.CreateJobObject(IntPtr.Zero, null);
            Check(job != IntPtr.Zero, "create_job");
            try
            {
                Check(Active(job) == 0, "empty_job");
                Check(Win.IsProcessInJob(Win.GetCurrentProcess(), job, out bool controllerInJob) &&
                      !controllerInJob, "controller_outside_job");
                using (var negative = Worker.StartSuspended())
                {
                    Check(!Win.AssignProcessToJobObject(IntPtr.Zero, negative.Process), "invalid_assignment_rejected");
                    Check(!negative.Resumed, "invalid_assignment_never_resumed");
                }
                using (var worker = Worker.StartSuspended())
                {
                    Check(Win.AssignProcessToJobObject(job, worker.Process), "assign_worker");
                    Check(Win.IsProcessInJob(worker.Process, job, out bool inJob) && inJob, "worker_membership");
                    Check(Active(job) == 1, "suspended_worker_accounted");
                    worker.Resume();
                    Check(Win.WaitForSingleObject(worker.Process, 0) == WAIT_TIMEOUT &&
                          Active(job) == 1, "running_worker_accounted");
                    Check(Win.WaitForSingleObject(worker.Process, 12000) == WAIT_OK, "worker_exited");
                    worker.Exited = true;
                    var timer = Stopwatch.StartNew();
                    while (Active(job) != 0 && timer.ElapsedMilliseconds < 3000) Thread.Sleep(25);
                    Check(Active(job) == 0, "job_empty_after_exit");
                }
            }
            finally { Win.CloseHandle(job); }
            Console.WriteLine($"LAB_PASS checks={checks}");
            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"LAB_FAIL type={ex.GetType().Name} detail=redacted");
            return 1;
        }
    }

    static uint Active(IntPtr job)
    {
        bool ok = Win.QueryInformationJobObject(job, ACCOUNTING, out var info,
            (uint)Marshal.SizeOf<Win.JobAccounting>(), out _);
        if (!ok) throw new InvalidOperationException("job accounting failed");
        return info.ActiveProcesses;
    }
    static void Check(bool ok, string label)
    {
        if (!ok) throw new InvalidOperationException(label);
        checks++;
        Console.WriteLine($"PASS {label}");
    }

    sealed class Worker : IDisposable
    {
        readonly Win.ProcessInformation pi;
        public IntPtr Process => pi.Process;
        public bool Resumed { get; private set; }
        public bool Exited { get; set; }
        Worker(Win.ProcessInformation info) { pi = info; }
        public static Worker StartSuspended()
        {
            string exe = Environment.ProcessPath ?? throw new InvalidOperationException("runtime missing");
            if (!string.Equals(System.IO.Path.GetFileName(exe), "dotnet.exe", StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("unexpected runtime");
            string dll = typeof(Program).Assembly.Location;
            var si = new Win.StartupInformation { Size = (uint)Marshal.SizeOf<Win.StartupInformation>() };
            var cmd = new StringBuilder($"\"{exe}\" \"{dll}\" --worker");
            if (!Win.CreateProcess(exe, cmd, IntPtr.Zero, IntPtr.Zero, false,
                    SUSPENDED | NO_WINDOW, IntPtr.Zero, null, ref si, out var info))
                throw new InvalidOperationException("synthetic process launch failed");
            return new Worker(info);
        }
        public void Resume()
        {
            if (Resumed || Win.ResumeThread(pi.Thread) == uint.MaxValue)
                throw new InvalidOperationException("resume failed");
            Resumed = true;
        }
        public void Dispose()
        {
            if (pi.Process != IntPtr.Zero)
            {
                if (!Exited && Win.WaitForSingleObject(pi.Process, 0) == WAIT_TIMEOUT)
                {
                    // Only the exact handle of this experiment's newly created worker.
                    Win.TerminateProcess(pi.Process, 1);
                    Win.WaitForSingleObject(pi.Process, 5000);
                }
                Win.CloseHandle(pi.Process);
            }
            if (pi.Thread != IntPtr.Zero) Win.CloseHandle(pi.Thread);
        }
    }

    static class Win
    {
        [StructLayout(LayoutKind.Sequential)] internal struct StartupInformation
        {
            public uint Size;
            public IntPtr Reserved, Desktop, Title;
            public uint X, Y, Width, Height, XChars, YChars, Fill, Flags;
            public ushort ShowWindow, ReservedCount;
            public IntPtr Reserved2, StandardInput, StandardOutput, StandardError;
        }
        [StructLayout(LayoutKind.Sequential)] internal struct ProcessInformation
        { public IntPtr Process, Thread; public uint ProcessId, ThreadId; }
        [StructLayout(LayoutKind.Sequential)] internal struct JobAccounting
        {
            public long UserTime, KernelTime, PeriodUserTime, PeriodKernelTime;
            public uint PageFaults, TotalProcesses, ActiveProcesses, TerminatedProcesses;
        }
        [DllImport("kernel32.dll", EntryPoint = "CreateJobObjectW", CharSet = CharSet.Unicode, SetLastError = true)]
        internal static extern IntPtr CreateJobObject(IntPtr attributes, string? name);
        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool AssignProcessToJobObject(IntPtr job, IntPtr process);
        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool IsProcessInJob(IntPtr process, IntPtr job,
            [MarshalAs(UnmanagedType.Bool)] out bool result);
        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool QueryInformationJobObject(IntPtr job, int infoClass,
            out JobAccounting info, uint length, out uint resultLength);
        [DllImport("kernel32.dll", EntryPoint = "CreateProcessW", CharSet = CharSet.Unicode, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool CreateProcess(string app, StringBuilder command, IntPtr processAttributes,
            IntPtr threadAttributes, [MarshalAs(UnmanagedType.Bool)] bool inheritHandles,
            uint flags, IntPtr environment, string? directory, ref StartupInformation startup,
            out ProcessInformation processInfo);
        [DllImport("kernel32.dll", SetLastError = true)]
        internal static extern uint ResumeThread(IntPtr thread);
        [DllImport("kernel32.dll", SetLastError = true)]
        internal static extern uint WaitForSingleObject(IntPtr handle, uint milliseconds);
        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool TerminateProcess(IntPtr process, uint exitCode);
        [DllImport("kernel32.dll")] internal static extern IntPtr GetCurrentProcess();
        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool CloseHandle(IntPtr handle);
    }
}
