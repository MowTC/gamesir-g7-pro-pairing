# USB pairing protocol

This document describes the address-pairing flow implemented by this project. It is not a general G7 Pro firmware protocol specification.

## Transport and device discovery

Windows HID, 64-byte input and output reports, including the report ID. Usage page `FFF0`, usage `0040`. Each command is zero-padded to 64 bytes.

| Role | VID:PID | Evidence |
| --- | --- | --- |
| Receiver | `3537:0575` | Read/write and wireless pairing tested |
| Controller in USB pairing mode | `3537:5437` | Read/write and wireless pairing tested |
| Alternate receiver | `2563:0575` | Accepted by original tool; untested here |
| Alternate controller | `2563:028E`, `0283:028E` | Accepted by original tool; untested here |

The tested controller appears as `3537:109B` in normal Xbox USB mode. Its physical USB interface uses vendor class `FF`, subclass `47`, protocol `D0` rather than HID. A synthetic Windows gamepad HID interface is not the service interface. Adding the gaming PID to a HID filter cannot expose the pairing transport.

To enter the tested pairing mode, disconnect USB, set the rear switch to the middle, hold D-pad Up + Left, plug USB in while holding, then release.

## Reports

| Offset | Read request | Read response | Write request | Write acknowledgment |
| --- | --- | --- | --- | --- |
| 0 | `0F` | `10` | `0F` | `10` |
| 1–2 | `42 54` | `42 54` | `42 54` | `42 54` |
| 3 | `01` | `01` | `02` | `02` |
| 4–9 | zero | controller address | controller address | controller address |
| 10–15 | zero | receiver address | receiver address | receiver address |
| 16–63 | zero | unused | zero | unused |

Address bytes are little endian on the wire. For example, wire bytes `01 02 03 04 05 06` display as `06-05-04-03-02-01`. The two six-byte slots always have the same meaning on either device. There is no checksum in these recovered commands.

## Transaction

1. Read receiver state `(old controller, receiver self)`.
2. Read controller state `(controller self, old receiver)`.
3. Validate both self addresses (six bytes, not all zero or all FF).
4. Form the target tuple `(controller self, receiver self)`.
5. If both devices already report that tuple, return without writes.
6. Save both complete original tuples and USB paths to a new backup file. Flush to disk.
7. Write the target tuple to the receiver, checking its acknowledgment.
8. Write the same target tuple to the controller, checking its acknowledgment.
9. Read both devices again and compare every address byte with the target tuple.

A matching opcode alone is not considered successful: addresses must match too. Radio input and persistence after power cycling are separate manual checks. They were confirmed by the user on the initial hardware pair.

There is no atomic commit across the two devices. A timeout does not prove that a write was ignored. Failures after writing starts are reported as potentially partial, with the backup location preserved. Automatic rollback is intentionally not implemented because it has not been tested on hardware.

## I/O implementation

The application posts an asynchronous input read and performs an overlapped native `WriteFile` on the same HID handle. The write timeout is two seconds; a matching reply must arrive within three seconds. Unrelated reports are ignored within that deadline. A failed link is closed rather than reused with stale pending input.

This preserves the transport behavior that worked in live testing. Initial attempts to send through a FileStream writer or a separate write handle timed out. Native buffers stay allocated until an overlapped write completes or its cancellation finishes.

## Research provenance

The initial supplied USB capture contained a 64-byte `0F 42 54 01` receiver request at frame 8637 and a matching `10 42 54 01` response at frame 8639. The official application's address methods established the tuple layout and receiver-first write order. Live reads reproduced the captured response; subsequent writes and fresh reads verified cross-pairing. Raw data and proprietary binaries are excluded from this repository.

Official product support: [GameSir G7 Pro FAQ](https://gamesir.com/pages/faq-questions-about-g7-pro). Connection-order troubleshooting was corroborated by a [firsthand report](https://www.reddit.com/r/Gamesir/comments/1o1r632/pair_tool_detects_dongle_but_nothing_for_the/).
