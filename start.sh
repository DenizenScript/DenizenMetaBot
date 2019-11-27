#!/bin/bash
git pull origin master
git submodule update --init --recursive
screen -dmS DenizenMetaBot dotnet run -- $1
