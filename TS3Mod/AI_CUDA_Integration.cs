using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using HarmonyLib;
using TS3Mod.Core;

namespace TS3Mod.AI
{
    public static class ProcessOptimiser
    {
        private static string _pythonExe;
        private static readonly Dictionary<string, int> SpawnedPids = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        // ... (Keep existing PYTHON_CANDIDATES and LauncherScript)

        public static void Apply(ProcessStartInfo startInfo)
        {
            if (startInfo == null || string.IsNullOrWhiteSpace(startInfo.FileName)) return;
            string exeName = Path.GetFileName(startInfo.FileName).ToLowerInvariant();

            bool isRecog = exeName == "recog.exe" || exeName == "rm.exe";
            bool isCpm = exeName == "cpm.exe";
            bool isTts = exeName == "tts.exe";

            if (!isRecog && !isCpm && !isTts) return;

            string module = Path.GetFileNameWithoutExtension(startInfo.FileName).ToLowerInvariant();
            string python = FindPython(startInfo);

            if (string.IsNullOrEmpty(python))
            {
                Log.E("[TS3Mod] Python not found. Bypass aborted.");
                return;
            }

            string extractDir, pycPath;
            if (!TryResolveExtractedLayout(startInfo.FileName, module, out extractDir, out pycPath)) return;

            string tempDir = UnityEngine.Application.temporaryCachePath;
            string launcherPath = Path.Combine(tempDir, module + "_launcher.py");

            // --- DIAGNOSTIC PATH INJECTION ---
            string pyDir = Path.GetDirectoryName(python);
            string sitePkgs = Path.Combine(pyDir, "Lib", "site-packages");

            if (Directory.Exists(sitePkgs))
            {
                string cudnnBin = Path.Combine(sitePkgs, "nvidia", "cudnn", "bin");
                string torchLib = Path.Combine(sitePkgs, "torch", "lib");
                string existingPath = startInfo.EnvironmentVariables["PATH"] ?? Environment.GetEnvironmentVariable("PATH");

                string newPath = $"{cudnnBin};{torchLib};{existingPath}";
                startInfo.EnvironmentVariables["PATH"] = newPath;

                Log.I($"[TS3Mod] DEBUG: Injected PATH to: {newPath.Substring(0, Math.Min(newPath.Length, 100))}...");
            }

            File.WriteAllText(launcherPath, LauncherScript, Encoding.UTF8);

            startInfo.FileName = python;
            startInfo.Arguments = $"-u \"{launcherPath}\" \"{module}\" \"{extractDir}\" \"{pycPath}\" \"{Path.Combine(TS3Plugin.RuntimeLogDir, module + ".stderr.log")}\" \"{Path.Combine(TS3Plugin.RuntimeLogDir, module + ".stdout.log")}\" \"{Path.Combine(TS3Plugin.RuntimeLogDir, module + ".crash.log")}\" \"{Path.Combine(TS3Plugin.RuntimeLogDir, module + ".preflight.log")}\" {(isTts ? "1" : "0")} \"1\" \"{TS3Plugin.MirrorLogDirPrimary ?? ""}\" \"{TS3Plugin.MirrorLogDirFallback ?? ""}\" {startInfo.Arguments}".Trim();

            Log.I("Reroute execution success: " + module);
        }

        private static string FindPython(ProcessStartInfo startInfo)
        {
            if (!string.IsNullOrWhiteSpace(_pythonExe) && File.Exists(_pythonExe)) return _pythonExe;
            foreach (var cand in PYTHON_CANDIDATES) { try { if (File.Exists(cand)) { _pythonExe = cand; return _pythonExe; } } catch { } }
            return null;
        }

        private static bool TryResolveExtractedLayout(string originalExePath, string module, out string extractDir, out string pycPath)
        {
            extractDir = null; pycPath = null;
            string exeDir = Path.GetDirectoryName(originalExePath);
            string[] candDirs = new[] { originalExePath + "_extracted", Path.Combine(exeDir, module + "_extracted"), Path.Combine(exeDir, "_internal") };
            foreach (var d in candDirs)
            {
                if (!Directory.Exists(d)) continue;
                string[] pycs = Directory.GetFiles(d, "*.pyc");
                if (pycs.Length > 0) { extractDir = d; pycPath = pycs[0]; return true; }
            }
            return false;
        }

        public static void ResetSession() { SpawnedPids.Clear(); }
        private static void SafeDelete(string p) { try { if (File.Exists(p)) File.Delete(p); } catch { } }
    }
}
```

**After you deploy this version**, run the game, perform a command, and then check the console log again. Look for the `[TS3Mod] DEBUG: Injected PATH to:` line.If you see it, tell me if the paths listed actually exist on your hard drive. If they don't, we have found our problem!