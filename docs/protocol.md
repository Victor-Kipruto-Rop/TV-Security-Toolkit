# Device protocol (TVSP v1) - an assumption, adapt to your device
Frame: `"TVSP"` | version u8 | length u32 BE | JSON payload | CRC32 u32 BE.
Request `{"id","cmd","args"}`; response `{"id","ok","result"|"error"}`. Bytes travel as `{"$b64":"..."}`.
Commands: see `ProtocolConstants.Commands`. `provision` (test reset) is optional. State-changing commands are listed in
`SecurityConstants.MutatingCommands`. `ProtocolServer` is a reference device-side implementation.
