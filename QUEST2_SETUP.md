# TimeCraft VR — Meta Quest 2

This project now keeps its desktop test mode and adds a standalone Quest 2 mode.
The implementation follows the course `app/XR-activities-2026` OpenXR workflow:
Unity OpenXR + XR Interaction Toolkit packages only (no Meta XR SDK).

## Quest 2 controls

- Point with either Touch controller. The ray turns green over a clip/effect.
- Press **Trigger** to select a media clip.
- Hold **Grip** to squeeze the selected clip into a ball and hold it in your hand.
- Swing your hand and release **Grip** to throw the ball into the red trash bin.
- Press **A/X** to trim the selected media clip.
- Move the **right thumbstick left/right** to decrease/increase clip speed.
- Press **B/Y** to pause or resume the evaluator timer.
- Short controller vibration confirms selection and edits.

## Configured for the headset

- Android, OpenXR, Meta Quest Support, Oculus Touch Controller Profile
- Vulkan, ARM64, IL2CPP, Android 12L / API 32 minimum
- Single-pass instanced OpenXR rendering
- 72 FPS target, 4x MSAA, reduced mobile shadows, floor-level tracking origin
- Application ID: `com.yuyanggong.timecraftvr`

## Build

1. Let Unity finish importing/compiling after the project regains focus.
2. Open **File > Build Profiles**, select **Android**, and click **Switch Platform**.
3. Set texture compression to **ASTC**.
4. In **XR Plug-in Management > Project Validation**, use **Fix All** for any remaining
   machine-specific warnings.
5. Connect and authorise the Quest 2 over USB, then choose **Build And Run**.

The build scene is already `Assets/Scenes/TimeCraft_IP1_Editable_Prototype.unity`.
Use the existing mouse/keyboard workflow in the Editor when no XR device is active.
Select a media clip and press **G** to crumple it, then drag/flick it into the red bin.
