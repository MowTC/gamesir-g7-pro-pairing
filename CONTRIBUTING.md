# Contributing

Build with `powershell -NoProfile -ExecutionPolicy Bypass -File .\build.ps1`. Run the checks before submitting changes. Keep the app dependency-free and keep USB behavior easy to audit.

A useful compatibility report includes the exact controller/receiver edition, firmware versions when known, detected VID/PID and usage, whether USB pairing-mode entry works, whether readbacks match, and whether wireless input/reconnection works. Distinguish observations from guesses.

Activity and backup files contain device addresses and USB paths. Redact personal identifiers and unrelated USB information. Do not upload vendor executables, extracted libraries, decompiled code, or raw USB captures containing unrelated devices.

For protocol changes, document the evidence and add meaningful failure-path checks. Do not broaden the write allowlist merely because a device has a GameSir vendor ID. New firmware commands, restore support, and additional hardware families require separate investigation and hardware validation.

Pull requests should explain the problem, the resulting behavior, and validation. Source contributions use the repository's MIT license.
