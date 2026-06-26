@echo off
echo Deploying TS3Mod DLLs to BepInEx...

:: Set the target BepInEx plugins directory
set TARGET_DIR=C:\Program Files (x86)\Steam\steamapps\common\Tower! Simulator 3\BepInEx\plugins

:: Create the folder if it does not already exist
if not exist "%TARGET_DIR%" (
    mkdir "%TARGET_DIR%"
)

:: Copy the compiled DLLs from each project's Debug folder
:: Note: Update the 'net472' or 'netstandard2.1' folder path if your target framework is different
copy /Y ".\TS3Mod\bin\Debug\TS3Mod.dll" "%TARGET_DIR%\"
copy /Y ".\TS3Mod.AI\bin\Debug\TS3Mod.AI.dll" "%TARGET_DIR%\"
copy /Y ".\TS3Mod.MemoryPatch\bin\Debug\TS3Mod.MemoryPatch.dll" "%TARGET_DIR%\"
copy /Y ".\TS3Mod.Networking\bin\Debug\TS3Mod.Networking.dll" "%TARGET_DIR%\"

echo.
echo Deployment complete!
pause