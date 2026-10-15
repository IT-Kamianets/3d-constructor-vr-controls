# IT-Kamianets 3D Engine: VR Constructor Controls

`3d-constructor-vr-controls` turns XR controllers into character commands for virtual reality games built on the **IT-Kamianets 3D Engine**. It is the VR counterpart of `3d-constructor-character-view-controls`: the same `CharacterCommand`, so the motor, the network and bots don't care which device made it, and VR players play with flat-screen players.

### Logic

This package belongs to the constructor layer. It uses `3d-unity-input` (actions, poses), `3d-unity-character` (the command) and `3d-unity-xr` (the rig that carries the view); it never reads a device itself and holds no game rules.

* The head looks; the body faces where the head looks.
* The left stick walks relative to where the head looks (or where the off hand points).
* The right stick turns the rig (snap or smooth, chosen by the player in `XRComfort`).
* Stepping in the room walks the body after the head while the stick is idle, so walls still block.
* Buttons: jump, sprint and crouch (toggles); crouching for real also crouches.
* The casting hand (`HandAim`) is an `IAimSource`, so gameplay aims from the hand without knowing about XR.

### Status

Version 0.1.

* `VRControls`: XR input → `CharacterCommand` (move relative to the head or the off hand, yaw from the head, jump, sprint and crouch toggles, crouch below 75% of standing eye height, body follows the head in the room with a 5 cm dead zone). Sets the rig's `BodyFollowsHead` and the vignette's motion. While enabled, the Player map listens to XR controllers only, so keys used by an XR simulator don't also walk the character.
* `HandAim`: the aim ray of the dominant hand (`EngineInput.DominantHand`, or a fixed hand), as an `IAimSource`; `OffHand` for a wrist menu.

Checked in Play mode with simulated headset and controllers (Temnorid Arena): walking with the stick goes where the head looks, a room step is followed, real crouching crouches, the hand ray aims spells.

```csharp
player.AddComponent<XRCharacterRig>();
player.AddComponent<VRControls>();
player.AddComponent<HandAim>();     // gameplay reads it as IAimSource
```

### Roadmap

* Teleport locomotion as a comfort option
* Climbing and grabbing ledges
* Hand tracking (no controllers)
* Two-handed gestures for casting
