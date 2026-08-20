#!/bin/bash

ASSETPATH=../../../Assets
WORKSPACE=..
LUBAN_DLL=$WORKSPACE/Luban/Luban.dll
CONF_ROOT=.
OUTJSON_DIR=$ASSETPATH/Game/Res/Configs
OUTCODE_DIR=$ASSETPATH/Game/Scripts/Runtime/Generated/Configs



dotnet $LUBAN_DLL \
    -t all \
    -d json \
    --conf $CONF_ROOT/luban.conf \
    -x outputDataDir=$OUTJSON_DIR \
    -x outputCodeDir=$OUTCODE_DIR \
    -c cs-simple-json \
