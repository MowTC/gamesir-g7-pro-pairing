# GameSir G7 Pro Pairing

An independent, open-source Windows utility for pairing a GameSir G7 Pro controller with an existing compatible 2.4 GHz receiver.

**Verified on one G7 Pro setup:** cross-pairing, wireless input, and reconnection after turning the controller off and on. Other editions and firmware versions have not been verified. This is a community project, not an official GameSir product.

[Download the Windows app](https://github.com/mowbraytc-cmyk/gamesir-g7-pro-pairing/releases/tag/v0.2.0) · [Browse the source](https://github.com/mowbraytc-cmyk/gamesir-g7-pro-pairing/tree/main/src)

The first public release is a preview: the initial implementation was hardware-tested successfully; the redesigned version has passed automated and visual checks but still needs a live service-mode hardware retest.

## Use the app

1. Extract the Windows release ZIP into a writable folder and run **GameSirPairing.exe**. There is no installer. Administrator access should not be necessary.
2. Plug in the receiver and disconnect other controllers.
3. Unplug the controller you want to pair. Set its rear switch to the **middle**. Hold **D-pad Up + Left before plugging in USB**, then release. Holding the buttons only after connecting does not enter this mode.
4. Choose **Scan devices**, then select the intended receiver and controller. Choose **Read addresses** if you want to inspect the current pairing first.
5. Choose **Pair devices**. The app saves a backup, writes both devices, and reads both again to verify the result. This replaces the receiver's previous controller association.
6. Disconnect the controller's USB cable, switch to **2.4 GHz**, and test wireless input. Turn the controller off and on to confirm reconnection.

The app cannot put a controller into USB pairing mode remotely. A controller that works normally over USB can still be absent from the pairing list until you use the connection sequence above.

Requires 64-bit Windows 10 or 11 with .NET Framework 4.8. The release executable is unsigned. No GameSir installation, custom USB driver, network connection, or third-party runtime package is needed.

## What it does

- Enumerates USB HID interfaces and checks a small allowlist of device IDs, usage page/usage, and report sizes.
- Reads the controller and receiver's six-byte pairing addresses.
- Saves the original address records under `data/` beside the executable, and flushes the backup to disk **before** sending any pairing write.
- Preserves each device's own address and updates its partner address.
- Checks both write acknowledgments and fresh readbacks. Devices that already report the correct pairing are not rewritten.

It does not flash firmware, replace drivers, download files, connect to a server, or send telemetry. The complete USB command format is documented in [docs/PROTOCOL.md](docs/PROTOCOL.md). Pairing transaction logic is in [src/PairingProtocol.cs](src/PairingProtocol.cs); all Windows API calls are in [src/NativeHid.cs](src/NativeHid.cs).

Pairing writes are two separate device operations, not an atomic transaction. If a disconnect or timeout happens after a write, one device may already have changed. Preserve the backup and activity log. There is no automatic rollback or built-in restore operation. Re-enter USB pairing mode, reconnect the same two devices, and read their addresses before deciding whether to retry. A backup records the original values; it is not a one-click recovery file.

## Build from source

No NuGet packages or downloaded build dependencies are used. On 64-bit Windows with .NET Framework 4.8 installed:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\build.ps1
```

This compiles the application and runs 25 checks for protocol encoding, device filtering, backup ordering, partial failures, acknowledgment verification, and readback verification. Outputs are in `bin/`. To also create the portable release ZIP, source ZIP, and SHA-256 checksums:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\build.ps1 -Package
```

`GameSirPairing.csproj` is also included for Visual Studio/MSBuild with the .NET Framework 4.8 targeting pack. The WPF interface is embedded as readable XAML and loaded locally at startup. The source is not obfuscated.

## Troubleshooting and privacy

- **Controller missing:** repeat the USB pairing-mode sequence, with the buttons held before connecting. Normal Xbox USB mode does not expose the required pairing interface.
- **Receiver missing:** reconnect it directly to the PC and scan again. Unsupported device interfaces are deliberately excluded.
- **Communication error:** close other pairing tools, reconnect the devices, then scan again.
- **Backup cannot be saved:** move the extracted app to a writable folder. Pairing will stop before writes if the backup cannot be saved.
- **Pairing verified, but wireless input fails:** readback confirms stored addresses, not the radio connection. Check the controller's 2.4 GHz switch position and report the exact hardware/firmware versions.

Expand **Technical details & activity** to inspect the actual USB reports. **Open backups & logs** opens `data/`. These local files contain device addresses and USB paths. Review and redact them before posting an issue. Never include captures of unrelated USB devices or vendor binaries in a contribution.

## License and provenance

Newly authored source is available under the [MIT license](LICENSE). You can inspect, modify, build, and redistribute it under that license. GameSir names identify compatible hardware; no endorsement is implied.

The protocol was investigated using an official pairing utility and USB traffic supplied for local research. This repository contains an independent implementation and protocol notes. It does not contain the official executable, extracted libraries, decompiled listings, raw captures, personal device logs, or original address backups.

See [CONTRIBUTING.md](CONTRIBUTING.md) for useful compatibility reports and development guidance.

