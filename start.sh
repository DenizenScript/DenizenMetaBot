#!/bin/bash
git pull origin master
git submodule update --init --recursive
rm -rf ./bin/live_release_old
mv ./bin/live_release ./bin/live_release_old
dotnet build DenizenMetaBot.sln --configuration Release -o ./bin/live_release
dotnet bin/live_release/DenizenMetaBot.dll $1

# Exit code 42 means restart, anything else = don't.
if [ $? == 42 ]; then
    source ./start.sh
fi
