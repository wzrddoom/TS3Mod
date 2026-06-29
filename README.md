TS3 Engine Optimiser

A high-performance BepInEx plugin designed to resolve critical latency and CPU bottlenecks in Tower! Simulator 3 (TS3) by hijacking unoptimised AI execution modules.

Overview

Tower! Simulator 3 suffers from performance issues due to the way its internal AI modules (Whisper AI, Text-to-Speech) are packaged and executed. The base game uses inefficient background Python executables bundled via PyInstaller, which often lack the necessary CUDA libraries to utilise dedicated GPUs.

This mod intercept the game's process spawning logic to:
- GPU Acceleration: Force AI inference onto your dedicated NVIDIA GPU (tested on RTX 4050).
- Native Bypass: Redirect execution to locally installed Python environments, bypassing fragmented and missing dependencies in the game's original bundles.
- Network Stability: Implement strict TCP buffer limits and timeouts to prevent the Unity main thread from freezing when the AI backend encounters errors.
- Efficient Resource Management: Optimise thread usage and audio flush intervals for real-time responsiveness.

Dependencies
To run this mod, the following environment is required:
- Mod Loader: BepInEx 5.x (x64).

Python Runtime:
- Python 3.12: Required for recog.exe, cpm.exe, and rm.exe modules.
- Python 3.11: Required for the tts.exe module.

Ensure these are installed in standard locations (e.g., C:\Users\<user>\AppData\Local\Programs\Python\).

- CUDA Toolkit: NVIDIA CUDA Toolkit (v12.3 or v12.4) and cuDNN v9.0+ must be installed on the host system to enable hardware acceleration.

- Libraries: The local Python environments must have the required AI inference libraries (e.g., torch, faster-whisper) pre-installed via pip.

Installation

- Install BepInEx 5.x into your Tower! Simulator 3 directory.
- Copy the DLLs and dependencies into BepInEx/plugins/.
- Ensure your local Python environments are installed as listed in the Dependencies section.

Launch the game. The mod will automatically detect the native modules, inject CUDA paths at runtime, and reroute execution to your local Python interpreters.

Logging & Troubleshooting
The mod generates detailed runtime logs in the game folder. If you encounter issues, check the TS3Mod runtime logs located in your configured logging directory for:

- Preflight logs: Check if the correct Python path is being resolved.
- Crash logs: Contains tracebacks if the module execution fails during the bypass.
- Stdout/Stderr logs: Verify that the AI modules are successfully initialising with CUDA support.

Developed for the TS3 community to ensure smooth, lag-free air traffic control simulation.
