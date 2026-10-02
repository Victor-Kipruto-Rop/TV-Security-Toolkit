# Device adapter contract
Subclass `DeviceAdapter`; each method returns a dict. Required: `provision() identity() now()`.
Used by tests: apply_entitlement, state, advance, set_clock, go_offline, reconnect, factory_reset,
apply_update(package, source, interrupt), info, firmware_hash, installed_signature, secure_boot_state,
verify_boot_chain, validate_version, check_compatibility, boot, rollback, usb_discover, tls_connect,
api_call, send_message, storage_audit, credential_storage_audit, log_audit, debug_probe,
diagnostic_access_probe. Methods whose names are in `security/policy.py: MUTATING` are blocked by
profiles with `allow_state_changing: false`.

## USB wire protocol (TVSP v1)
Frame: `b"TVSP"` | version u8 | length u32 | JSON payload | CRC32 u32 (big-endian). Request
`{"id","cmd","args"}`, response `{"id","ok","result"|"error"}`; bytes as `{"$b64":...}`. The device must
implement the commands in `protocol.COMMANDS` (a `provision` test-reset command is optional).
`ProtocolServer` in protocol.py is a reference device-side implementation.
