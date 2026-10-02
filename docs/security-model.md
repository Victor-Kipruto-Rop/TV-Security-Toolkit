# Security model
Purpose: verify that a device enforces its protections. Signatures use HMAC-SHA256 with a lab key as a stand-in for the vendor's
asymmetric scheme; real-device verification should use the vendor public key. Evidence redacts `sig`, `key`, `secret`,
`password`, `mac` and summarises binary payloads. Read-only environments block mutating commands before any device I/O.
The toolkit generates lab-signed test inputs only; it contains no tooling to bypass a real vendor's protections.
