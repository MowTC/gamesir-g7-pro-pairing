# Changelog

## 0.2.0

- Replace the initial diagnostic interface with a WPF interface, guided setup, device selection, visible address summaries, clear result states, and expandable activity details.
- Separate USB transport, address encoding, transaction logic, and presentation into readable source files.
- Flush original-address backups to disk before writes; verify both acknowledgments and fresh readbacks.
- Skip writes when both devices already report the intended pairing.
- Add 25 automated protocol and transaction checks.
- Provide an MIT license, protocol documentation, reproducible build instructions, portable/source packages, and SHA-256 checksums.

## Initial hardware validation

Cross-pairing a GameSir G7 Pro with another G7 Pro's receiver succeeded. Wireless input and reconnection after turning the controller off and on were confirmed by the user. This result does not establish compatibility with every edition or firmware version.
