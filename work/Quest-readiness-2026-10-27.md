# MarsPath / Quest readiness — 27 October 2026

The current deliverable is the Windows presentation build. It is not yet a tested Quest application, and the Windows EXE cannot be installed directly on Quest.

## Verified locally

- Unity 6000.6.5f1 has Android Build Support, Android SDK, NDK and OpenJDK folders installed.
- The project manifest contains Unity's base XR module, but no OpenXR provider package and no XR Management package.
- The current navigation interface is desktop IMGUI. Headset-facing panels, controller interaction and an XR camera rig have not been implemented or validated.
- No tested Quest APK is present. Device connection/account/developer-mode status has not been inspected.

Raw checks: quest-readiness-check.json.

## Remaining before a headset delivery can be called ready

1. Add and configure a compatible Unity OpenXR loader / Quest interaction profiles and Android player configuration.
2. Implement tracked XR camera origin, suitable world-space or head-relative HUD, controller selection and comfortable navigation. Desktop cinematic time jumps must not be assumed comfortable in VR.
3. Produce an Android ARM64 device build and resolve platform/shader/UI issues.
4. Test installation, headset tracking, controllers, text readability, stereo rendering and sustained on-device performance on Quest 3.

The local simulator and editor can assist development, but they do not verify the device's graphics hardware or Android runtime. An early borrowed-device session before 7 November reduces the risk of leaving required integration work until the user's exams. No date or hardware-free guarantee is made for these remaining items.

Official references:

- Meta software/Android setup: https://developers.meta.com/vr/documentation/unity/unity-software-setup/
- Meta deployment and real-device testing: https://developers.meta.com/vr/documentation/unity/unity-deploy-and-test/
- Simulator boundaries: https://developers.meta.com/vr/documentation/unity/xrsim-intro/
