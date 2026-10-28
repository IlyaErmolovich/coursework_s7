# BlueUnity

Source: https://github.com/bentalebahmed/BlueUnity
Release: https://github.com/bentalebahmed/BlueUnity/releases/tag/Kotline-v0.0.1
Inspected upstream source commit: b42373b7af6bcc940b5e501a6a99fdbd9d9702d9
License: MIT (included beside this file).

Bundled Android binaries were extracted unchanged from BlueUnity-Kotline-v0.0.1.unitypackage:
SHA-256 of package: 41265a73437bf2d6ed3a0b4d91c66e71bd17ff9ae93ad73a12ebe1e98cbf7e69
- Assets/Plugins/Android/blueunity-release.aar
- Assets/Plugins/Android/kotlin-stdlib-1.9.24.jar
- Assets/Plugins/Android/gson-2.10.1.jar

The upstream release supports Bluetooth Classic RFCOMM secure pairing, discovery, server/client roles and byte callbacks. Its example targets Unity 6000.3.0f1. This project uses installed Unity 6000.3.10f1, Activity entry point, min API 27 and target API 35.

The sample scene/UI and upstream C# wrapper were not imported. FrostDuel.BluetoothConnection is a project-specific adapter: it waits for runtime permissions before accessing Bluetooth, requests only the permission group applicable to the OS version, queues callbacks onto Unity's main thread, frames fragmented UTF-8 byte streams, bounds message size, and detects inactivity with a heartbeat. Bluetooth traffic is real RFCOMM, not TCP or a cloud relay.

Limits of upstream implementation found in source inspection: read EOF can terminate its worker without a clean disconnect callback; heartbeat detects this within 12 seconds. The plugin does not offer robust cancellation of an outstanding client connect attempt. Avoid overlapping connect attempts. Reconnect is manual, to the previous peer, while both game processes remain alive. Physical-device compatibility remains subject to testing.

Android permission reference: https://developer.android.com/develop/connectivity/bluetooth/bt-permissions
