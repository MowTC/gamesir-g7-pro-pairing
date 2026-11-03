# GameSir G7 Pro Pairing v0.2.0

Independent, MIT-licensed USB pairing utility for matching a G7 Pro controller with an existing compatible G7 Pro receiver.

Download **GameSirPairing-0.2.0-win-x64.zip**, extract it into a writable folder, and run **GameSirPairing.exe**. Requires 64-bit Windows 10/11 with .NET Framework 4.8. No installer, custom driver, or network connection is needed. The executable is unsigned.

The **source.zip** contains the complete buildable source. **SHA256SUMS.txt** contains the SHA-256 checksums of both ZIPs. GitHub Actions builds these downloads from the tagged public source and runs the automated checks before publishing.

## Setup

1. Connect the receiver. Disconnect other controllers.
2. Unplug the target controller and set its rear switch to the middle.
3. Hold D-pad Up + Left BEFORE reconnecting USB, then release.
4. Scan, select the intended devices, and choose Pair devices.
5. After verification, unplug the controller cable, switch to 2.4 GHz, and test input and reconnection.

Original pairing addresses are backed up under data/ before any writes. The application verifies both acknowledgments and fresh address reads. Technical details show the actual USB traffic.

## Validation and scope

The initial implementation successfully cross-paired a G7 Pro with another G7 Pro's receiver. Wireless input and reconnection after turning the controller off and on were confirmed. The redesigned version passes 25 automated protocol/transaction checks and has been visually inspected; its refactored transport has not yet been retested with a controller in USB pairing mode. This initial public release is therefore marked as a preview.

Compatibility with every G7 Pro edition and firmware is unproven. Pairing is a two-device operation; a disconnect may leave a partial change. Preserve the backup if an operation fails. No automatic rollback or restore function is included.

No official GameSir binaries, extracted libraries, raw USB captures, private device logs, or original address backups are included. This project is not affiliated with GameSir.
