# infinitas_statfetcher

Probes memory for information for each played chart, from obvious stuff like ex score, note judging, dj level and clear to gauge percent, play settings (random/sudden+/autoscratch/gauge) and if the song played to the end or not.

This stuff is then append to a .tsv session file where the data saved is configurable.
It can also send the data to a server configurable in the config file, either run locally or remotely.

Memory offsets used to find the relevant information is stored in a file along with the build version it works with. On start the binary will probe the executable for build version by brute force and compare with what's listed in the offset file.
If no match it will check for an applicable offset file on a configurable server.
This behaviour can be turned off if unwanted.

# Todo
- Auto-update encoding fixes-list
- Keyboard hook for manual memdumps
