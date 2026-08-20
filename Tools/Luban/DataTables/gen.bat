@echo off

set ASSETPATH=../../../Assets
set WORKSPACE=..
set LUBAN_DLL=%WORKSPACE%\Luban\Luban.dll
set CONF_ROOT=.

set OUTJSON_DIR=%ASSETPATH%/Game/Res/Configs
set OUTCODE_DIR=%ASSETPATH%/Game/Scripts/Runtime/Generated/Configs

dotnet %LUBAN_DLL% ^
    -t all ^
    -d json ^
    --conf %CONF_ROOT%/luban.conf ^
    -x outputDataDir=%OUTJSON_DIR% ^
    -x outputCodeDir=%OUTCODE_DIR% ^
    -c cs-simple-json

pause
