## Rcon.cs
This is a wrapper class for interacting with Valve's Source Engine servers via TCP/IP. The specification for the Source RCON Protocol can be seen at: https://developer.valvesoftware.com/wiki/Source_RCON_Protocol. This class makes is easy to connect to a server via RCON and send commands/receive responses.

## Program.cs
Example console app that creates a new RCON connection and then sends the "status" command to the server every 5 seconds.
