#!/bin/bash
git pull origin master
git submodule update --init --recursive
dotnet build DenizenMetaBot.sln --configuration Release -o ./bin/live_release
screen -dmS DenizenMetaBot dotnet bin/live_release/DenizenMetaBot.dll -- $1
