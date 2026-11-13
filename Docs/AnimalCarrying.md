# Animal carrying: project implementation and animation setup

## What is implemented

This project uses Unity 6000.6, URP, a CharacterController, a Mixamo-style source
rig, and a separate visible Survivalist humanoid. PlayerBodyAnimation writes the
source pose at execution order 100; SurvivalistBody copies it to the visible rig
at order 2000. CarcassBackCarry runs at 2200, after that copy.

Controls:

1. Kill a ground animal and approach its carcass.
2. Tap E. Inventory accepts the carcass; a frozen mesh copy lifts to the shoulders
   over 1.15 seconds. Movement is blocked during the lift.
3. Walk with the load. Weapons are holstered, movement uses 75% of the existing
   weight-adjusted speed, and sprinting/jumping are disabled.
4. Press U. A 0.8-second lowering motion releases a physical carcass. It retains
   the same species, shot condition, spoilage time and inventory identity.
5. Tap E to retrieve it, hold E to field-skin it, or take it to the tanner.

Birds use the separate FallenBird/meat system and are not changed by this feature.
Existing inventory capacity still allows multiple carcasses: models stack and the
latest one receives hand IK and the drop command. A single load looks most natural.

Complete source:

- Assets/CarcassBackCarry.cs: snapshot ownership, pickup/drop phases, chest follow,
  sway, analytic arm IK, and physical release.
- Assets/Inventory.Goods.cs: pickup, reference-based removal, weight and skinning.
- Assets/Inventory.cs: weapon holstering while carrying.
- Assets/PlayerMove.cs: action lock, movement speed, sprint/jump restrictions.
- Assets/PlayerBodyAnimation.cs: blended lean and shortened procedural stride.
- Assets/Editor/PeltEconomyTests.cs: pickup/skin cleanup, drop/re-pickup mesh
  lifetime, and IK reach/bone-length regression tests.

## Why follow a bone instead of using a joint?

For this project, use a visual load driven by the visible chest, without animal
colliders or a Rigidbody while carried. Imported bone axes are calibrated once;
the script follows chest motion with a species-adjustable offset. Following in
LateUpdate avoids scaling the model by the chest's often tiny import scale and
ensures the attachment follows the final retargeted pose.

Parenting to a dedicated child socket on Chest/UpperChest is also appropriate
when the entire character hierarchy has uniform scale. Use a position/rotation
constraint if you need to blend between ground, hands and shoulder targets.
Animation Rigging provides a good authoring workflow for hand grips, but do not
run it before a later script overwrites those same bones.

A ragdoll attached to the chest with a joint is more expensive and harder to
stabilize. Use it for deliberate physical interaction; exclude collisions with
the player, limit joint motion, and avoid two independent systems driving the
same root. For this laptop the visual-copy approach has less simulation cost.

The current dropped animal uses one box collider and Rigidbody, not a limb ragdoll.
Its frozen limbs do not flop independently. A convincing floppy neck/legs requires
a retained animal skeleton with authored limp animation or constrained ragdoll bones.

## Editor verification and tuning

1. Let Unity import the scripts and confirm the Console has no compile errors.
2. Open Assets/Scenes/GetStarted_Scene.unity and enter Play mode.
3. Switch to third person with V. Kill and pick up one deer with E.
4. Under Player, select the runtime object named "Back carried ...".
5. In CarcassBackCarry, tune Back Offset (player-space metres), Carry Euler,
   Pickup Seconds, Drop Seconds and Hand Weight. Start with the supplied defaults.
   Different species require different offsets; the current automatic snapshot
   limits its longest dimension to 2.4 metres and does not preserve giant-animal size.
6. Create two empty children on that carried model, LeftGrip and RightGrip.
   Place them on the legs or shoulder contact points, then assign the component's
   Left Grip and Right Grip fields. Their forward/up orientation controls wrist
   rotation. Without assigned targets the code uses approximate shoulder grips.
7. Walk, turn and crouch. Inspect from the side and behind. Adjust the offset so
   the torso clears the back and the hands are within arm reach; IK clamps reach.
8. Stop, press U, and verify it lands on the ground, cannot push the player,
   can be retrieved, and still skins at the tanner. Check first person too.
9. Skin/discard the carried inventory item and verify its visual disappears.
10. Changes to runtime objects disappear on leaving Play mode. Copy tuned values
    into the defaults in CarcassBackCarry.cs for now; for several species, move
    these values into per-species prefab/socket profiles before final art polish.

The automatic rig lookup requires a valid humanoid Avatar. A Generic character
needs explicit bone references added to CacheRig rather than GetBoneTransform.

## Animator Controller route for authored clips

The current implementation is self-contained procedural animation; it does not
install new motion-capture clips or modify the Animator Controller. For production
quality, author the pickup and drop with the actual animal as a reference prop.
Do not simply add an Animator layer to the visible body: this project's late
pose-copy pass would overwrite its upper body.

To migrate, first consolidate upper-body ownership into the visible Animator, or
make PlayerBodyAnimation and SurvivalistBody explicitly yield upper-body control
while authored carry states play. Keep the final socket/IK update after that pass.
Then set up the following in a duplicate controller:

1. Add Carrying (bool), CarrySpeed (float), Pickup (trigger), and Drop (trigger).
2. Base layer: keep normal locomotion, add full-body Pickup and Drop states, and
   add a CarryLocomotion sub-state machine with Idle/Walk/Run in a 1D blend tree.
3. Use actual planar speed in metres/second for CarrySpeed. Example thresholds:
   0, 1.8 and 3.2; match them to the clips' measured stride speeds. Current gameplay
   blocks sprinting, so a run clip is optional until that design choice changes.
4. Normal locomotion -> Pickup on Pickup trigger; enter CarryLocomotion after
   pickup finishes. CarryLocomotion -> Drop on Drop trigger; Drop -> normal
   locomotion after completion. Avoid broad Any State transitions that restart actions.
5. Use roughly 0.1-0.2 second blends as a starting point. Pickup and Drop do not
   loop; carry idle/walk/run do. Interruptions need explicit cleanup paths.
6. Alternatively, use an Override Carrying layer for a held upper-body pose with
   an Avatar Mask including torso/arms/hands and excluding root/legs. A masked
   layer alone cannot supply a heavier leg gait; use the carry locomotion tree too.
7. With this CharacterController, leave Apply Root Motion off and use in-place
   locomotion clips. Bake unwanted root XZ/rotation into pose on import. Do not
   apply animator displacement in addition to CharacterController.Move.
8. Replace the script's timer-based attachment/release with animation events at
   actual hand-contact and release frames. Keep a completion/interruption fallback;
   events alone must not leave movement locked if a state is interrupted.
9. Blend hand weights up after contact and down before release. Use elbow hints
   and avoid full IK weights while hands are still reaching toward the animal.

These are migration instructions, not an already-wired second animation mode.
The current C# state timings continue to drive pickup and release until migrated.

## Hand IK options

The installed code uses an analytic two-bone solver after retargeting. It preserves
arm segment lengths, clamps unreachable targets, and accepts wrist rotation from
the optional grip transforms. Two Bone IK constraints in Animation Rigging are an
alternative when pose ownership is consolidated. Place a RigBuilder on the rig,
create a Rig, and assign upper arm/lower arm/hand plus targets and elbow hints for
each constraint. Keep grip targets outside the hand hierarchy to avoid feedback.

For a conventional Humanoid Animator with no later bone overwrite, this optional
script is the standard OnAnimatorIK equivalent (not used by the current project):

```csharp
using UnityEngine;

[RequireComponent(typeof(Animator))]
public sealed class CarryHandIK : MonoBehaviour
{
    public Transform leftGrip, rightGrip;
    [Range(0, 1)] public float weight;
    public int ikLayer;
    Animator animator;
    void Awake() => animator = GetComponent<Animator>();
    void OnAnimatorIK(int layerIndex)
    {
        if (layerIndex != ikLayer || !animator.isHuman) return;
        Apply(AvatarIKGoal.LeftHand, leftGrip);
        Apply(AvatarIKGoal.RightHand, rightGrip);
    }
    void Apply(AvatarIKGoal goal, Transform grip)
    {
        float w = grip != null ? weight : 0f;
        animator.SetIKPositionWeight(goal, w);
        animator.SetIKRotationWeight(goal, w);
        if (grip == null) return;
        animator.SetIKPosition(goal, grip.position);
        animator.SetIKRotation(goal, grip.rotation);
    }
}
```

Put that script on the same GameObject as its enabled Humanoid Animator, enable
IK Pass on the chosen controller layer, and set ikLayer to its index. Do not run
it alongside the installed final-pose solver on the same arms.

## Secondary motion and animation sourcing

The implemented motion uses planar travel to advance a gait phase, a small
sideways/bounce target, and SmoothDamp for spring-like smoothing. It stops advancing
while paused. Keep displacement to centimetres; large root oscillations cause
body clipping. For floppy legs, animate the animal's bones or simulate a small
subset of bones instead of moving its entire torso more aggressively.

On Mixamo, try search terms such as "carrying", "heavy walk", "lifting",
"pick up", and "put down". These are search suggestions, not confirmed names of
matching shoulder-carry clips. Preview the silhouette and contact timing. Mixamo
is for bipedal humanoids, not auto-rigging the deer. For an exact shoulder carry,
edit or create the animation in Blender/Maya with both character and animal visible.

Import character/animation rigs as Humanoid, validate the Avatar/T-pose, and use
Copy From Other Avatar only when the clip shares that source skeleton. Different
humanoid skeletons need their own correctly mapped Avatars for retargeting. Use
in-place clips, then tune speed/stride and arm spacing against the actual animal.
Imported FBX clips are read-only; duplicate/extract an editable clip for Unity
Animation-window adjustments, or edit in the source DCC and reimport.

## Common problems

- Back clipping: tune a species-specific offset/socket and folded animal pose;
  physics cannot fix an unsuitable held pose. Check crouching and turning too.
- Wrong pivot: position by visual centre/socket, not arbitrary model origin. The
  current snapshot computes a centre correction after orientation and scale.
- Scale distortion: use uniform import scale and avoid nonuniformly scaled bone
  parents. The code follows world-space pose to avoid inheriting chest scale.
- Player pushed or launched: no animal colliders/physics during carrying; ignore
  the player's colliders when enabling drop physics.
- Jitter: update after final animation/retargeting; do not let animation, constraints
  and physics all control the same transform. Current order is deliberately 2200.
- Hands detach: place grip targets on the load, ensure reachable positions, and
  blend weights. Do not make grip targets children of the hands they drive.
- Feet slide: match clip stride speed to movement speed; reducing playback speed
  alone is not a substitute for a shorter, heavier stride.
- Root motion double movement: choose either script-driven movement or consumed
  root motion, and route all actual movement through the controller.
- Different corpse after dropping: this implementation preserves the exact frozen
  appearance and goods metadata, but does not restore AI or a limb ragdoll.

## Validation status

The runtime and Editor assemblies compile with zero errors (three pre-existing
unused-field warnings). Regression tests were added; they have not been executed
in Unity in this session. Pickup/drop appearance, exact bone offsets, ground
collision and camera clearance still require Play mode visual verification.

References:

- https://docs.unity3d.com/ja/current/ScriptReference/MonoBehaviour.OnAnimatorIK.html
- https://docs.unity3d.com/es/current/Manual/ConfiguringtheAvatar.html
- https://docs.unity3d.com/cn/2018.3/Manual/class-AvatarMask.html
- https://helpx.adobe.com/creative-cloud/help/animate-characters-mixamo.html
- https://helpx.adobe.com/creative-cloud/faq/mixamo-faq.html
