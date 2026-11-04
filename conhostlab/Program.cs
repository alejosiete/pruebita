// Public synthetic test only. Run exclusively on GitHub-hosted disposable Windows.
using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;

internal static class Program
{
    private static int checks;
    static int Main(string[] args)
    {
        if (args.Length == 1 && args[0] == "--synthetic-worker")
        {
            Thread.Sleep(2500);
            return 0;
        }
        if (args.Length != 0) return 2;

        if (Environment.GetEnvironmentVariable("GITHUB_ACTIONS") != "true" ||
            Environment.GetEnvironmentVariable("RUNNER_OS") != "Windows" ||
            Environment.GetEnvironmentVariable("RUNNER_ENVIRONMENT") != "github-hosted" ||
            Environment.GetEnvironmentVariable("GITHUB_REPOSITORY") != "alejosiete/pruebita" ||
            Environment.GetEnvironmentVariable("GITHUB_REF") != "refs/heads/experiment/windows-job-object-20261104" ||
            Environment.GetEnvironmentVariable("SYNTHETIC_WINJOB_APPROVED") != "true")
        {
            Console.WriteLine("LAB_STOP unverified_runner");
            return 1;
        }

        IntPtr inputRead = IntPtr.Zero, inputWrite = IntPtr.Zero;
        IntPtr outputRead = IntPtr.Zero, outputWrite = IntPtr.Zero;
        IntPtr pseudo = IntPtr.Zero;
        try
        {
            Check("runner_scope_verified", true);
            var baseline = DirectChildren();
            if (baseline.Total != 0) return Inconclusive("unexpected_baseline_children");
            Check("baseline_no_direct_children", true);

            if (!Win.CreatePipe(out inputRead, out inputWrite, IntPtr.Zero, 0) ||
                !Win.CreatePipe(out outputRead, out outputWrite, IntPtr.Zero, 0))
                return Inconclusive("anonymous_pipes_unavailable");

            int status = Win.CreatePseudoConsole(new Win.Coord { X = 80, Y = 25 },
                inputRead, outputWrite, 0, out pseudo);
            if (status != 0 || pseudo == IntPtr.Zero)
                return Inconclusive("pseudoconsole_creation_unavailable");
            Check("real_pseudoconsole_created", true);

            Counts console = new();
            bool found = Poll(() =>
            {
                console = DirectChildren();
                return console.Conhost > 0 && console.Total == console.Conhost;
            }, 6000);
            if (!found) return Inconclusive("console_host_not_direct_child");
            Check("genuine_conhost_direct_child_observed", true);
            Check("zero_child_rule_blocked_without_workers", console.Total > 0);

            string dll = typeof(Program).Assembly.Location;
            string host = Environment.GetEnvironmentVariable("DOTNET_HOST_PATH") ?? "dotnet";
            var psi = new ProcessStartInfo(host) { UseShellExecute = false, CreateNoWindow = true };
            psi.ArgumentList.Add(dll);
            psi.ArgumentList.Add("--synthetic-worker");
            using var worker = Process.Start(psi);
            if (worker == null) return Inconclusive("synthetic_worker_launch_failed");
            bool workerVisible = Poll(() =>
            {
                var current = DirectChildren();
                return current.Total > current.Conhost;
            }, 800);
            if (!workerVisible) return Inconclusive("synthetic_worker_parentage_not_observed");
            Check("synthetic_worker_observed", true);
            if (!worker.WaitForExit(8000))
            {
                // Only the exact synthetic process launched by this experiment.
                worker.Kill();
                worker.WaitForExit(5000);
                return 1;
            }
            Check("synthetic_worker_finished", worker.ExitCode == 0);

            Counts after = new();
            bool consoleOnly = Poll(() =>
            {
                after = DirectChildren();
                return after.Conhost > 0 && after.Total == after.Conhost;
            }, 2500);
            if (!consoleOnly) return Inconclusive("console_host_not_retained_after_worker");
            Check("only_console_child_remains", true);
            Check("old_zero_child_gate_remains_blocked", after.Total > 0);

            Win.ClosePseudoConsole(pseudo);
            pseudo = IntPtr.Zero;
            bool drained = Poll(() => DirectChildren().Total == 0, 8000);
            if (!drained) return Inconclusive("console_child_did_not_retire_after_close");
            Check("console_child_retires_after_close", true);
            Console.WriteLine($"LAB_PASS conhost_circular_wait_reproduced checks={checks}");
            return 0;
        }
        catch (Exception e)
        {
            // Intentionally never print OS identifiers, exception details or stack traces.
            Console.WriteLine($"LAB_FAIL type={e.GetType().Name} detail=redacted");
            return 1;
        }
        finally
        {
            if (pseudo != IntPtr.Zero) Win.ClosePseudoConsole(pseudo);
            if (inputRead != IntPtr.Zero) Win.CloseHandle(inputRead);
            if (inputWrite != IntPtr.Zero) Win.CloseHandle(inputWrite);
            if (outputRead != IntPtr.Zero) Win.CloseHandle(outputRead);
            if (outputWrite != IntPtr.Zero) Win.CloseHandle(outputWrite);
        }
    }
    static int Inconclusive(string label) { Console.WriteLine("LAB_INCONCLUSIVE " + label); return 2; }
    static void Check(string label, bool ok)
    {
        if (!ok) throw new InvalidOperationException("synthetic check failed");
        checks++;
        Console.WriteLine("PASS " + label);
    }
    static bool Poll(Func<bool> f, int timeoutMs)
    {
        var sw = Stopwatch.StartNew();
        do
        {
            if (f()) return true;
            Thread.Sleep(100);
        } while (sw.ElapsedMilliseconds < timeoutMs);
        return f();
    }
    readonly record struct Counts(int Total, int Conhost);
    static Counts DirectChildren()
    {
        IntPtr snap = Win.CreateToolhelp32Snapshot(0x00000002, 0);
        if (snap == new IntPtr(-1)) throw new InvalidOperationException("process_snapshot_failed");
        try
        {
            var entry = new Win.ProcessEntry { Size = (uint)Marshal.SizeOf<Win.ProcessEntry>(), FileName = "" };
            int total = 0, console = 0;
            bool ok = Win.Process32FirstW(snap, ref entry);
            if (!ok) throw new InvalidOperationException("process_snapshot_empty");
            uint own = (uint)Environment.ProcessId;
            do
            {
                if (entry.ParentProcessId == own)
                {
                    total++;
                    if (string.Equals(entry.FileName, "conhost.exe", StringComparison.OrdinalIgnoreCase)) console++;
                }
                entry.Size = (uint)Marshal.SizeOf<Win.ProcessEntry>();
            } while (Win.Process32NextW(snap, ref entry));
            return new Counts(total, console);
        }
        finally { Win.CloseHandle(snap); }
    }
    static class Win
    {
        [StructLayout(LayoutKind.Sequential)]
        internal struct Coord { public short X, Y; }
        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        internal struct ProcessEntry
        {
            public uint Size, Usage, ProcessId;
            public IntPtr DefaultHeapId;
            public uint ModuleId, Threads, ParentProcessId;
            public int BasePriority;
            public uint Flags;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)]
            public string FileName;
        }
        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool CreatePipe(out IntPtr read, out IntPtr write, IntPtr security, uint size);
        [DllImport("kernel32.dll", SetLastError = true)]
        internal static extern int CreatePseudoConsole(Coord size, IntPtr input, IntPtr output,
            uint flags, out IntPtr pseudoConsole);
        [DllImport("kernel32.dll")]
        internal static extern void ClosePseudoConsole(IntPtr pseudoConsole);
        [DllImport("kernel32.dll", SetLastError = true)]
        internal static extern IntPtr CreateToolhelp32Snapshot(uint flags, uint processId);
        [DllImport("kernel32.dll", EntryPoint = "Process32FirstW", CharSet = CharSet.Unicode, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool Process32FirstW(IntPtr snapshot, ref ProcessEntry entry);
        [DllImport("kernel32.dll", EntryPoint = "Process32NextW", CharSet = CharSet.Unicode, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool Process32NextW(IntPtr snapshot, ref ProcessEntry entry);
        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool CloseHandle(IntPtr handle);
    }
}
