# TimeCraft VR — no-headset testing

This prototype includes two test views and does not require a VR headset.

## Run

1. Open the project with Unity 2022.3 LTS.
2. Open `Assets/Scenes/TimeCraft_IP1_Editable_Prototype.unity`.
3. Before pressing Play, use the Scene view and Hierarchy to inspect or reposition the camera, lighting, workspace, clips, effects, labels, and UI.
4. Press Play to test the saved interaction logic.
5. Press `F1` to switch between the desktop overview and the simulated-headset view.

## Simulated-headset controls

- `F1`: switch test view
- Hold right mouse button and move the mouse: turn the simulated head
- `WASD` or arrow keys: walk at fixed 1.65 m eye height
- Centre reticle + left mouse drag: select, grab, and move a clip/effect
- `T`: trim the selected clip
- `Q` / `E`: reduce/increase selected clip speed
- `F`: apply a selected effect
- `P`: record an evaluator prompt
- `R`: reset the session
- `Space`: pause/resume the session timer

The HUD records successful selections and missed selections. Use these counts with participant observations to identify targets that are too small, unclear, or hard to aim at.

## Important limitation

This mode supports interaction-flow and basic spatial-layout evaluation. A headset is still required before final submission to validate controller pose, physical reach, perceived scale, motion comfort, tracking, and device performance.
