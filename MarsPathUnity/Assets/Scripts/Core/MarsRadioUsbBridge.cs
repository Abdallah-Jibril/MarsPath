#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace MarsPath
{
    internal sealed class MarsRadioUsbBridge
    {
        private Task<bool> probe;
        private string adb;
        private string pairedSerial;
        private float nextProbe;
        internal bool Ready { get; private set; }

        internal void Poll(float now, string keyPath, int headsetPort, int desktopPort)
        {
            if (probe != null)
            {
                if (!probe.IsCompleted) return;
                Ready = probe.Status == TaskStatus.RanToCompletion && probe.Result;
                _ = probe.Exception;
                probe = null;
            }
            if (now < nextProbe) return;
            nextProbe = now + 2f;
            probe = Task.Run(() =>
            {
                if (adb == null || !File.Exists(adb)) adb = FindAdb();
                if (adb == null) return false;
                string serial = Run(adb, "-d get-serialno")?.Trim();
                if (string.IsNullOrEmpty(serial) || !Regex.IsMatch(serial, "^[a-zA-Z0-9_-]+$"))
                {
                    pairedSerial = null;
                    return false;
                }
                string device = "-s " + serial;
                if (pairedSerial != serial)
                {
                    if (Run(adb, device + " shell getprop ro.product.model")?.Trim() != "Quest 3" ||
                        !Provision(device, keyPath))
                        return false;
                    pairedSerial = serial;
                }

                string remote = $"tcp:{headsetPort}", local = $"tcp:{desktopPort}";
                string list = Run(adb, device + " forward --list");
                if (list == null) return false;
                foreach (string line in list.Split('\n'))
                {
                    string[] fields = line.Split((char[])null, StringSplitOptions.RemoveEmptyEntries);
                    if (fields.Length == 3 && fields[1] == local) return fields[0] == serial && fields[2] == remote;
                }
                return Run(adb, $"{device} forward --no-rebind {local} {remote}") != null;
            });
        }

        private bool Provision(string device, string keyPath)
        {
            const string directory = "/sdcard/Android/data/com.lagrangev.marspath/files";
            string destination = directory + "/" + Path.GetFileName(keyPath);
            return Run(adb, $"{device} shell mkdir -p {directory}") != null &&
                Run(adb, $"{device} push \"{keyPath}\" {destination}.tmp") != null &&
                Run(adb, $"{device} shell mv -f {destination}.tmp {destination}") != null;
        }

        private static string FindAdb()
        {
            foreach (string variable in new[] { "ANDROID_SDK_ROOT", "ANDROID_HOME" })
            {
                string root = Environment.GetEnvironmentVariable(variable);
                if (string.IsNullOrWhiteSpace(root)) continue;
                string path = Path.Combine(root, "platform-tools", "adb.exe");
                if (File.Exists(path)) return path;
            }
            string userSdk = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "Android", "Sdk", "platform-tools", "adb.exe");
            if (File.Exists(userSdk)) return userSdk;
            string editors = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Unity", "Hub", "Editor");
            if (Directory.Exists(editors))
            {
                foreach (string editor in Directory.EnumerateDirectories(editors))
                {
                    string path = Path.Combine(editor, "Editor", "Data", "PlaybackEngines", "AndroidPlayer", "SDK", "platform-tools", "adb.exe");
                    if (File.Exists(path)) return path;
                }
            }
            foreach (string directory in (Environment.GetEnvironmentVariable("PATH") ?? "").Split(Path.PathSeparator))
            {
                if (string.IsNullOrWhiteSpace(directory)) continue;
                string path = Path.Combine(directory.Trim().Trim('"'), "adb.exe");
                if (File.Exists(path)) return path;
            }
            return null;
        }

        private static string Run(string executable, string arguments)
        {
            try
            {
                using var process = Process.Start(new ProcessStartInfo(executable, arguments)
                {
                    CreateNoWindow = true,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true
                });
                if (process == null) return null;
                Task<string> output = process.StandardOutput.ReadToEndAsync();
                _ = process.StandardError.ReadToEndAsync();
                if (process.WaitForExit(1500) && output.Wait(250)) return process.ExitCode == 0 ? output.Result : null;
                if (process.HasExited) return null;
                process.Kill();
            }
            catch (Win32Exception) { }
            catch (InvalidOperationException) { }
            return null;
        }
    }
}
#endif
