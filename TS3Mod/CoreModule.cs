using System;
using System.Collections;
using System.IO;
using System.Reflection;
using BepInEx;
using HarmonyLib;
using UnityEngine;

namespace TS3Mod.Core
{
    // NEW: A standalone public class to safely hold variables without needing BepInEx references
    public static class ModState
    {
        public static string RuntimeLogDir;
        public static string MirrorLogDirPrimary;
        public static string MirrorLogDirFallback;
    }

    [BepInPlugin("com.wzrddoom.ts3.performance", "TS3 Voice Recognition Optimiser", "4.0.1")]
    public class TS3Plugin : BaseUnityPlugin
    {
        public static BepInEx.Logging.ManualLogSource SharedLogger;

        private Harmony _harmony;

        private void Awake()
        {
            SharedLogger = Logger;
            Log.I("[TS3Mod] Initialising modular core 3.1.2...");

            try
            {
                ModState.RuntimeLogDir = Path.Combine(Application.temporaryCachePath, "TS3ModLogs");
                Directory.CreateDirectory(ModState.RuntimeLogDir);
                Log.I("[TS3Mod] Runtime log dir: " + ModState.RuntimeLogDir);
            }
            catch (Exception ex)
            {
                Log.E("[TS3Mod] Runtime log dir failed: " + ex.Message);
                ModState.RuntimeLogDir = Application.temporaryCachePath;
            }

            try
            {
                string gameRoot = Paths.GameRootPath;
                ModState.MirrorLogDirPrimary = Path.Combine(gameRoot, "BepInEx", "plugins", "TS3ModLogs");
                Directory.CreateDirectory(ModState.MirrorLogDirPrimary);
                Log.I("[TS3Mod] Mirror primary: " + ModState.MirrorLogDirPrimary);
            }
            catch (Exception ex)
            {
                Log.W("[TS3Mod] Mirror primary unavailable: " + ex.Message);
                ModState.MirrorLogDirPrimary = null;
            }

            try
            {
                ModState.MirrorLogDirFallback = Path.Combine(Path.GetTempPath(), "TS3ModLogs");
                Directory.CreateDirectory(ModState.MirrorLogDirFallback);
                Log.I("[TS3Mod] Mirror fallback: " + ModState.MirrorLogDirFallback);
            }
            catch (Exception ex)
            {
                Log.W("[TS3Mod] Mirror fallback unavailable: " + ex.Message);
                ModState.MirrorLogDirFallback = null;
            }

            _harmony = new Harmony("com.lions.ts3.performance.harmony");

            try
            {
                // CRITICAL FIX: Force search in the actual BepInEx plugins folder.
                // Assembly.Location points to an empty temporary cache folder!
                string pluginDir = Paths.PluginPath;

                string[] modularDlls = Directory.GetFiles(pluginDir, "TS3Mod*.dll", SearchOption.AllDirectories);
                foreach (string dll in modularDlls)
                {
                    try
                    {
                        string asmName = Path.GetFileNameWithoutExtension(dll);
                        bool loaded = false;

                        // Check if it's already in the AppDomain to prevent crashes
                        foreach (var existing in AppDomain.CurrentDomain.GetAssemblies())
                        {
                            if (existing.GetName().Name == asmName) { loaded = true; break; }
                        }

                        if (!loaded)
                        {
                            Assembly.LoadFrom(dll);
                            Log.I($"[TS3Mod] Force-loaded modular assembly: {asmName}");
                        }
                    }
                    catch (Exception ex) { Log.W($"[TS3Mod] Could not load {Path.GetFileName(dll)}: {ex.Message}"); }
                }

                var assemblies = AppDomain.CurrentDomain.GetAssemblies();
                foreach (var asm in assemblies)
                {
                    if (asm.GetName().Name.StartsWith("TS3Mod"))
                    {
                        try
                        {
                            _harmony.PatchAll(asm);
                            Log.I($"[TS3Mod] Applied patches for: {asm.GetName().Name}");
                        }
                        catch (Exception exType) { Log.W($"[TS3Mod] Patch skip for assembly {asm.GetName().Name}: {exType.Message}"); }
                    }
                }
            }
            catch (Exception ex)
            {
                Log.E("[TS3Mod] Patch bootstrap failure: " + ex.Message);
            }

            Log.I("[TS3Mod] Harmony modular patches applied.");
            StartCoroutine(LogMonitor());
        }

        private IEnumerator LogMonitor()
        {
            yield return new WaitForSeconds(2f);

            while (true)
            {
                try
                {
                    if (Directory.Exists(ModState.RuntimeLogDir))
                    {
                        string[] files = Directory.GetFiles(ModState.RuntimeLogDir, "*.log", SearchOption.TopDirectoryOnly);
                        for (int i = 0; i < files.Length; i++)
                        {
                            SafeMirror(files[i]);
                            try
                            {
                                string readPath = files[i] + ".read";
                                File.Copy(files[i], readPath, true);
                                SafeMirror(readPath);
                            }
                            catch { }
                        }
                    }
                }
                catch { }

                yield return new WaitForSeconds(2f);
            }
        }

        internal static void SafeMirror(string sourcePath)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(sourcePath) || !File.Exists(sourcePath)) return;
                string name = Path.GetFileName(sourcePath);

                if (!string.IsNullOrWhiteSpace(ModState.MirrorLogDirFallback))
                {
                    try
                    {
                        Directory.CreateDirectory(ModState.MirrorLogDirFallback);
                        File.Copy(sourcePath, Path.Combine(ModState.MirrorLogDirFallback, name), true);
                    }
                    catch { }
                }

                if (!string.IsNullOrWhiteSpace(ModState.MirrorLogDirPrimary))
                {
                    try
                    {
                        Directory.CreateDirectory(ModState.MirrorLogDirPrimary);
                        File.Copy(sourcePath, Path.Combine(ModState.MirrorLogDirPrimary, name), true);
                    }
                    catch { }
                }
            }
            catch { }
        }
    }

    // Shared logger helper for all modules
    public static class Log
    {
        public static void I(string msg) { try { UnityEngine.Debug.Log("[TS3DBG] " + msg); } catch { } try { TS3Plugin.SharedLogger.LogInfo("[TS3DBG] " + msg); } catch { } }
        public static void W(string msg) { try { UnityEngine.Debug.LogWarning("[TS3DBG] " + msg); } catch { } try { TS3Plugin.SharedLogger.LogWarning("[TS3DBG] " + msg); } catch { } }
        public static void E(string msg) { try { UnityEngine.Debug.LogError("[TS3DBG] " + msg); } catch { } try { TS3Plugin.SharedLogger.LogError("[TS3DBG] " + msg); } catch { } }
    }
}