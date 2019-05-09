DenizenMetaBot
--------------

A Discord bot for Denizen 1.x (Bukkit) meta documentation.

This is a C#/.NETCore based Discord bot.

Created by mcmonkey and the DenizenScript team.

## Setup

Gathering the files note:
- In addition to a `git clone` of this repository, you need to clone the sub-repository, using a command like `git submodule update --init --recursive`.

The `start.sh` file is used by the `restart` command and should be maintained as correct to the environment to launch a new bot program instance... points of note:
- It starts with a `git pull` command to self-update. If this is not wanted, remove it. Be careful what repository this will pull from (a fork you own vs. the original repository vs. some other one...)
- It uses a `screen` command to launch the bot quietly into a background screen. The `screen` program must be installed for that to work. Alternately, replace it with some other equivalent background terminal program.
- The restart command will run this script equivalently to the following terminal command: `bash ./start.sh 12345` where `12345` is the ID number for the channel that issued a restart command.

To configure the bot:
- Create directory `config` within this bot's directory.
- Within the `config` directory, create file `token.txt` containing only the Discord bot token without newlines or anything else.
    - To create a bot token, refer to official Discord documentation. Alternately, you can follow the bot-user-creation parts of https://discord.foxbot.me/docs/guides/getting_started/intro.html (ignore the coding parts, just follow the first bits about creating a bot user on the Discord application system, and getting the token).
- Within the `config` directory, create file `config.fds` (a FreneticDataSyntax file) with the following options (See also the full file text sample below):
    - `valid_channels` set to a whitelist of channels the bot responds in (blank = responds anywhere).
    - `info_replies` set to a submapping of info commands and their replies, which allows comma-separated list keys. The first listed name on each line is the primary name. Type `\n` to add a line break.

`config.fds` sample text content (the channel IDs are the actual ones on the Denizen Discord group):
```
valid_channels:
# denizen-lobby
- 315163488085475337
# bot-spam
- 315616018846318593
info_replies:
    new,newb,noob,newbie: Welcome new user! Please read the rules at the bottom of the #info channel, and feel free after that to ask for help in the most relevant channel!
```

To start the bot up:
- Run `./start.sh` while in the bot's directory (You made need to run `chmod +x ./start.sh` first).

To view the bot's terminal:
- Connect to the screen - with an unaltered `start.sh` file, the way to connect to that is by running `screen -r DenizenMetaBot`.

## Copyright/Legal Info

The MIT License (MIT)

Copyright (c) 2019 The DenizenScript Team

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
SOFTWARE.