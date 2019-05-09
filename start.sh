#!/bin/bash
git pull origin master
screen -dmS DenizenMetaBot dotnet run -- $1
