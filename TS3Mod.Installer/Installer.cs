using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.IO.Compression;
using System.Collections.Generic;

namespace TS3Mod.Installer
{
    class Installer
    {
        static void Main(string[] args)
        {
            Console.WriteLine("========================================");
            Console.WriteLine(" TS3Mod Environment Setup & Downloader  ");
            Console.WriteLine("========================================");
            Console.WriteLine();
            Console.WriteLine("This utility will download and install the required AI libraries");
            Console.WriteLine("and CUDA dependencies for Tower! Simulator 3.");
            Console.WriteLine();

            string user = Environment.UserName;

            // Candidate paths for Python 3.12 (Recog/CPM/RM)
            string[] py312Paths = {
                $@"C:\Users\{user}\AppData\Local\Programs\Python\Python312\python.exe",
                @"C:\Program Files\Python312\python.exe"
            };

            // Candidate paths for Python 3.11 (TTS)
            string[] py311Paths = {
                $@"C:\Users\{user}\AppData\Local\Programs\Python\Python311\python.exe",
                @"C:\Program Files\Python311\python.exe"
            };

            Console.WriteLine("Scanning for Python 3.12...");
            InstallDependencies("Python 3.12", py312Paths);

            Console.WriteLine("\nScanning for Python 3.11...");
            InstallDependencies("Python 3.11", py311Paths);

            // Download release zip containing DLLs/worker from GitHub Releases and extract
            Console.WriteLine("\nChecking GitHub Releases for bundled DLLs...");
            try
            {
                string owner = "wzrddoom"; // repo owner
                string repo = "TS3Mod";       // repo name
                string releaseTag = "v4.0.1"; // target release tag (DLL_Release_v4.0.1)
                string dest = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "mod_dlls");
                Directory.CreateDirectory(dest);
                DownloadAndExtractReleaseZip(owner, repo, dest, releaseTag);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to download or extract release assets: {ex.Message}");
            }

            Console.WriteLine("\n========================================");
            Console.WriteLine("Setup Complete! Press any key to exit.");
            Console.ReadKey();
        }

        static void InstallDependencies(string versionLabel, string[] candidatePaths)
        {
            string validPath = null;

            foreach (string path in candidatePaths)
            {
                if (File.Exists(path))
                {
                    validPath = path;
                    break;
                }
            }

            if (validPath == null)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"[Error] {versionLabel} was not found on this system.");
                Console.WriteLine("Please ensure it is installed before running TS3Mod.");
                Console.ResetColor();
                return;
            }

            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine($"[Found] {validPath}");
            Console.ResetColor();
            Console.WriteLine($"Downloading and installing dependencies for {versionLabel}. This may take several minutes...");

            // Commands to run via python - upgrade pip and install required packages.
            string[] pipCommands = new[]
            {
                "-m pip install --upgrade pip",
                // Use the cu121 wheels for torch so the CUDA runtime DLLs are downloaded
                "-m pip install torch torchvision torchaudio --index-url https://download.pytorch.org/whl/cu121",
                "-m pip install openai-whisper numpy sentencepiece transformers accelerate safetensors huggingface-hub"
            };

            foreach (var cmd in pipCommands)
            {
                int exit = RunProcess(validPath, cmd, out string output, out string error);
                if (exit != 0)
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine($"[Error] Command '{cmd}' failed with exit code {exit}.");
                    if (!string.IsNullOrEmpty(error))
                    {
                        Console.WriteLine(error);
                    }
                    Console.ResetColor();
                    // Continue trying the rest but warn the user
                }
                else
                {
                    Console.ForegroundColor = ConsoleColor.Green;
                    Console.WriteLine($"[Success] Command '{cmd}' completed.");
                    Console.ResetColor();
                }
            }

            // Attempt to locate the site-packages folder for this python and copy native DLLs (torch CUDA runtime)
            try
            {
                int rc = RunProcess(validPath, "-c \"import sysconfig;print(sysconfig.get_paths()['purelib'])\"", out string siteOut, out string siteErr);
                if (rc == 0 && !string.IsNullOrWhiteSpace(siteOut))
                {
                    string sitePackages = siteOut.Trim();
                    Console.WriteLine($"Detected site-packages: {sitePackages}");

                    // Possible torch lib locations
                    string[] candidates = new[]
                    {
                        Path.Combine(sitePackages, "torch", "lib"),
                        Path.Combine(sitePackages, "torch", "libs"),
                        Path.Combine(sitePackages, "torch", "_C")
                    };

                    string foundLibDir = candidates.FirstOrDefault(Directory.Exists);
                    if (foundLibDir == null)
                    {
                        // try searching for torch folder then look for lib/libs under it
                        string torchDir = Path.Combine(sitePackages, "torch");
                        if (Directory.Exists(torchDir))
                        {
                            var dirs = Directory.GetDirectories(torchDir);
                            foundLibDir = dirs.FirstOrDefault(d => d.EndsWith("lib") || d.EndsWith("libs"));
                        }
                    }

                    if (!string.IsNullOrEmpty(foundLibDir) && Directory.Exists(foundLibDir))
                    {
                        Console.WriteLine($"Found native DLL directory: {foundLibDir}");
                        string destDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "mod_dlls");
                        Directory.CreateDirectory(destDir);

                        var dlls = Directory.GetFiles(foundLibDir, "*.dll");
                        int copied = 0;
                        foreach (var dll in dlls)
                        {
                            string dest = Path.Combine(destDir, Path.GetFileName(dll));
                            try
                            {
                                File.Copy(dll, dest, true);
                                copied++;
                            }
                            catch (Exception ex)
                            {
                                Console.WriteLine($"Failed to copy {dll}: {ex.Message}");
                            }
                        }

                        Console.WriteLine($"Copied {copied} native DLL(s) to {destDir}");
                    }
                    else
                    {
                        Console.WriteLine("Could not locate torch native DLL directory automatically. If you experience runtime errors, locate the torch 'lib' or 'libs' folder under site-packages and copy its DLLs into the game's plugin folder.");
                    }
                }
                else
                {
                    Console.WriteLine("Failed to detect site-packages path. Native DLLs may not be copied automatically.");
                    if (!string.IsNullOrWhiteSpace(siteErr)) Console.WriteLine(siteErr);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error while attempting to copy native DLLs: {ex.Message}");
            }
        }

        static int RunProcess(string executable, string arguments, out string standardOutput, out string standardError)
        {
            standardOutput = string.Empty;
            standardError = string.Empty;
            try
            {
                ProcessStartInfo startInfo = new ProcessStartInfo
                {
                    FileName = executable,
                    Arguments = arguments,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                };

                using (var process = new Process { StartInfo = startInfo })
                {
                    process.Start();
                    standardOutput = process.StandardOutput.ReadToEnd();
                    standardError = process.StandardError.ReadToEnd();
                    process.WaitForExit();
                    return process.ExitCode;
                }
            }
            catch (Exception ex)
            {
                standardError = ex.Message;
                return -1;
            }
        }

        static void DownloadAndExtractReleaseZip(string owner, string repo, string destDir, string tag = null)
        {
            // Uses GitHub Releases API to find the release by tag (if provided) or the latest release and download a .zip asset.
            string api;
            if (!string.IsNullOrWhiteSpace(tag))
                api = $"https://api.github.com/repos/{owner}/{repo}/releases/tags/{tag}";
            else
                api = $"https://api.github.com/repos/{owner}/{repo}/releases/latest";
            using (var http = new HttpClient())
            {
                http.DefaultRequestHeaders.UserAgent.ParseAdd("TS3Mod-Installer-Agent");
                string json = http.GetStringAsync(api).GetAwaiter().GetResult();
                if (string.IsNullOrWhiteSpace(json))
                {
                    Console.WriteLine("Could not read GitHub release information.");
                    return;
                }

                // Naive extraction of browser_download_url values to avoid adding a JSON dependency.
                var urls = new List<string>();
                int idx = 0;
                while (true)
                {
                    idx = json.IndexOf("browser_download_url", idx, StringComparison.OrdinalIgnoreCase);
                    if (idx == -1) break;
                    int start = json.IndexOf("http", idx);
                    if (start == -1) break;
                    int end = json.IndexOf('"', start);
                    if (end == -1) break;
                    string url = json.Substring(start, end - start);
                    urls.Add(url);
                    idx = end + 1;
                }

                if (urls.Count == 0)
                {
                    Console.WriteLine("No downloadable assets found in the latest release.");
                    return;
                }

                // Prefer a zip that likely contains 'DLL_Release_v'
                string chosen = urls.FirstOrDefault(u => u.EndsWith(".zip", StringComparison.OrdinalIgnoreCase) && u.ToLower().Contains("dll_release_v"))
                    ?? urls.FirstOrDefault(u => u.EndsWith(".zip", StringComparison.OrdinalIgnoreCase) && (u.ToLower().Contains("dll") || u.ToLower().Contains("worker") || u.ToLower().Contains("ts3mod")))
                    ?? urls.FirstOrDefault(u => u.EndsWith(".zip", StringComparison.OrdinalIgnoreCase));

                if (chosen == null)
                {
                    Console.WriteLine("No zip assets found in release assets.");
                    return;
                }

                Console.WriteLine($"Downloading release asset: {chosen}");
                byte[] data = http.GetByteArrayAsync(chosen).GetAwaiter().GetResult();
                if (data == null || data.Length == 0)
                {
                    Console.WriteLine("Downloaded asset is empty.");
                    return;
                }

                string tempZip = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString() + ".zip");
                File.WriteAllBytes(tempZip, data);

                string extractTemp = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
                Directory.CreateDirectory(extractTemp);
                try
                {
                    ZipFile.ExtractToDirectory(tempZip, extractTemp);

                    // Look for a folder named DLL_Release_vX (case-insensitive) anywhere in the extracted tree
                    string dllReleaseDir = Directory.EnumerateDirectories(extractTemp, "DLL_Release_v*", SearchOption.AllDirectories).FirstOrDefault()
                        ?? Directory.EnumerateDirectories(extractTemp, "DLL_Release_*", SearchOption.AllDirectories).FirstOrDefault();

                    int copied = 0;

                    if (!string.IsNullOrEmpty(dllReleaseDir) && Directory.Exists(dllReleaseDir))
                    {
                        Console.WriteLine($"Found DLL release folder: {dllReleaseDir}");

                        // Copy all DLLs in the release folder (except inside a worker folder) into destDir
                        var dlls = Directory.GetFiles(dllReleaseDir, "*.dll", SearchOption.AllDirectories);
                        foreach (var dll in dlls)
                        {
                            // skip DLLs inside a 'worker' subfolder if any
                            if (dll.IndexOf(Path.Combine("worker", ""), StringComparison.OrdinalIgnoreCase) >= 0)
                                continue;

                            string dest = Path.Combine(destDir, Path.GetFileName(dll));
                            try
                            {
                                File.Copy(dll, dest, true);
                                copied++;
                            }
                            catch (Exception ex)
                            {
                                Console.WriteLine($"Failed to copy {dll}: {ex.Message}");
                            }
                        }

                        // Locate worker.py inside the DLL release folder and copy it to a worker subfolder under destDir
                        var workerFile = Directory.GetFiles(dllReleaseDir, "worker.py", SearchOption.AllDirectories).FirstOrDefault();
                        if (!string.IsNullOrEmpty(workerFile) && File.Exists(workerFile))
                        {
                            string workerDestDir = Path.Combine(destDir, "worker");
                            Directory.CreateDirectory(workerDestDir);
                            string workerDest = Path.Combine(workerDestDir, Path.GetFileName(workerFile));
                            try
                            {
                                File.Copy(workerFile, workerDest, true);
                                Console.WriteLine($"Copied worker script to: {workerDest}");
                            }
                            catch (Exception ex)
                            {
                                Console.WriteLine($"Failed to copy worker.py: {ex.Message}");
                            }
                        }
                    }
                    else
                    {
                        // Fallback: copy any DLLs found in the entire archive
                        Console.WriteLine("DLL_Release_v* folder not found; falling back to copying all DLLs from archive.");
                        var allDlls = Directory.GetFiles(extractTemp, "*.dll", SearchOption.AllDirectories);
                        foreach (var dll in allDlls)
                        {
                            string dest = Path.Combine(destDir, Path.GetFileName(dll));
                            try
                            {
                                File.Copy(dll, dest, true);
                                copied++;
                            }
                            catch (Exception ex)
                            {
                                Console.WriteLine($"Failed to copy {dll}: {ex.Message}");
                            }
                        }
                    }

                    Console.WriteLine($"Extracted and copied {copied} DLL(s) to {destDir}");
                }
                finally
                {
                    try { File.Delete(tempZip); } catch { }
                    try { Directory.Delete(extractTemp, true); } catch { }
                }
            }
        }
    }
}
