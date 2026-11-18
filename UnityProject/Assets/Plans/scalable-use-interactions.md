# Project Overview
- Game Title: Existing project; title not established by the attached context. No branding changes in scope.
- High-Level Concept: Existing NPC/agent simulation with queued actions for movement, grabbing, giving and using props. Refactor use interactions into reusable, inspector-authored profiles that coordinate NPC, held-item and optional target animations without tying animation selection to scene object names.
- Players: Existing agent simulation; player count/network topology unchanged and outside this refactor.
- Inspiration / Reference Games: Not specified; preserve existing interaction presentation.
- Tone / Art Direction: Existing 3D hospital/medical-equipment assets and AnneFleur NPCs; reuse artwork and animation clips.
- Target Platform: StandaloneWindows64, Unity 6000.6.0f1.
- Screen Orientation / Resolution: Existing desktop configuration, unchanged.
- Render Pipeline: URP, current PC_RPAsset. No rendering or input changes.

# Game Mechanics
## Core Gameplay Loop
Continue issuing existing ActionData commands, approaching objects, grabbing/holding items, using them on targets, and chaining actions through runAfter. Keep the public `use` command with exactly one target-name parameter. Names remain command lookup identifiers, not animation selectors.

User selected **reusable interaction profiles** and **reusable target categories**. Alternative considered: explicit semantic animator states such as OpenDoor, shared across instances. That is a smaller refactor but still requires a new state for each animation type. Profiles with a generic clip slot require more initial migration but minimize ongoing setup and produce consistent feedback (MDA: predictable interaction mechanics yield readable simulation dynamics).

### Resolution rules
Resolve at actual action start, not when queued, because the held item may change between commands:
1. Resolve the target through the existing AgentSystem object/agent registry. Resolve the actual held object through NPC.Hand.Held/NPC.GetItem, not a second name lookup that could silently treat an unregistered held item as empty hands.
2. If a held object has a configured held-use profile and the target matches its accepted categories or a non-null existing ContextItem.usableOn reference, select held use: play the profile's NPC motion, trigger the held-item animation, and trigger an optional target response if configured/available.
3. Otherwise, if the target has a standalone-use profile, select standalone use: play that NPC motion and target response; do not trigger the incompatible held item, detach it, or change hand ownership.
4. Otherwise fail clearly without firing any animation triggers. Distinguish invalid target, unsupported interaction and broken required configuration.
5. A missing optional target Animator/response does not prevent a valid held use. Never discover the held-item Animator accidentally while searching an NPC target's child hierarchy.

Categories are reusable ScriptableObject references (e.g. NPC and Door), not object-name strings. A target can carry multiple categories. Empty category acceptance plus empty legacy usableOn means no compatible held-use targets, not a wildcard. A configured reusable category OR an explicit legacy reference grants compatibility. Keep legacy references intact to avoid discarding authored exceptions. Category matching is shared between runtime resolution and context reporting.

### Animation synchronization
- Use one generic `Use` clip state on the existing NPC ItemUse layer, overridden per NPC; use a generic `UseReach` mode for IK-only profiles such as Door, whose existing NPC Door state has no clip.
- Clip profiles define an NPC clip, playback speed, and contact time in normalized clip time. A once-only contact callback routes responses; held props can start with NPC playback (the current Use convention), while target responses can be immediate or contact-timed.
- Reach profiles reference the existing GrabProfile and GrabIKTimings. Reuse `GrabIK.ReachRoutine(Func<Pos>, GrabProfile, Action, GrabIKTimings)` and fire Door's `UseLater` at its actual onContact callback, not at action start or after a guessed delay.
- The action waits for its selected NPC clip/reach routine to finish and return to idle before runAfter dependents proceed. Do not wait for a target controller to return to idle: Door's open state is persistent and toggles on the next UseLater.
- All runtime state is per action/per NPC. Shared profiles contain configuration only. Reject overlapping uses on the same NPC and busy hand grab/release phases without redesigning the entire action scheduler.
- On completion/failure/disable, release the use lock, return only the owned use layer to Empty, and prevent late contact callbacks. Preserve existing grab/hold/release overrides and held-item parenting. Handle target/item destruction or loss of ownership during playback safely with bounded waits.

## Controls and Input Methods
Existing New Input System and command-driven AgentSystem remain unchanged. No new keyboard, mouse, UI or navigation controls. Maintain ActionData, AnimationLibrary registration and use target parameter compatibility.

# UI
No runtime menus/HUD changes. Improve the Inspector authoring workflow with clear headers, tooltips and conditional fields/validation, using the project's existing inspector conventions where suitable.

Inspector layout / authoring mockup:
```text
UseInteractable (on prop or NPC target root)
  Target Categories       [Door / NPC / ...]
  Standalone Use Profile  [OpenDoor / None]
  Held Use Profile        [UseTray / None]
  Response Animator       [Model Animator / None]
  Response                [Trigger: UseLater]
  Response Timing         [Contact / Start]
  Interaction Point       [GrabSpot reference / root fallback]

UseInteractionProfile (shared asset)
  NPC Motion              [Clip / Reach IK]
  Clip + Speed            [only for Clip]
  Contact Time            [only for Clip]
  Reach Profile + Timings [only for Reach IK]
  Accepted Categories     [only for held-use compatibility]
  Held Item Trigger       [Use]
  Held Item Timing        [Start / Contact]
```
Duplicating a configured prefab requires no new states or profile assets. Adding a genuinely new animation means create/duplicate one profile, assign its NPC clip or reach settings, and assign it to the prefab; configure the object's own response Animator only if needed.

# Key Asset & Context
## Findings
- `Assets/Scripts/Actions/Use.cs`: selects one participant, immediately sets Use, and plays an NPC ItemUse state hashed from useOn.name or held item's GetName. Does not check usableOn. Validate obtains held ContextItem by name, so missing context can appear as empty hands.
- `Assets/MoveAndGrab.cs`: Door-state StateMachineBehaviour finds a playing use action by agent name, resolves its target by name, reaches GrabSpot and sets UseLater in a separate NPC coroutine. The new use routine should own this directly, eliminating this fragile global lookup and independent completion.
- `Assets/Scripts/ContextItem.cs`: serialized Transform[] usableOn already exists. In TwoAgents, Tray and Syringe explicitly reference NPC1; other inspected entries are empty. Door is auto-discovered through EnvironmentItemTag during ContextLibrary.Awake.
- `Assets/Scripts/ContextLibrary.cs`: discovers EnvironmentItemTag objects and reports usableOn from the legacy list. Update reporting so category-based compatibility is visible to the command-generating context as well.
- `Assets/Scripts/NPC.cs`: GetItem returns Hand.Held.transform; scene NPC references initialize at runtime and must be verified in Play Mode rather than treating unset serialized runtime fields as broken wiring.
- `Assets/Scripts/Actions/Grabbing/GrabClipSwapper.cs`: creates a private AnimatorOverrideController per animator and captures it. Use playback must share that same per-NPC override controller; creating a second wrapper after this would strand the grab swapper on a stale controller.
- `Assets/Animations/MetaHuman.controller`: ItemUse has Empty, Syringe and Tray clip states (exit at 0.75), and Door with MoveAndGrab and no clip. ItemUse has weight 1 and no avatar mask. Retain existing other layers/masks/root-motion conventions.
- Door.controller: Use exists but transitions only depend on UseLater, toggling persistent Door state. Tray.controller: Use transition enters an Anim state whose motion is currently unassigned. Syringe_V2.controller has a real use clip and Use trigger, but no observed return transition; repeated-use/restart behavior needs a focused fix if required. Do not claim Tray prop movement works without an actual assigned compatible object clip.

## Proposed created assets
- `Assets/Scripts/Actions/Using/UseTargetCategory.cs`: reusable category asset.
- `Assets/Scripts/Actions/Using/UseInteractionProfile.cs`: configuration-only clip/reach motion, compatibility and synchronization settings.
- `Assets/Scripts/Actions/Using/UseInteractable.cs`: profiles, category identities, explicit response Animator and point references on target/item root.
- `Assets/Scripts/Actions/Using/UseInteractionResolver.cs`: deterministic resolution and shared eligibility/reporting logic.
- `Assets/Scripts/Actions/Using/NPCUseController.cs`: per-NPC playback/lock/cleanup and generic slot configuration.
- `Assets/Scripts/Animation/NPCAnimatorOverrides.cs`: common per-NPC controller ownership used by grab and use, if a small shared helper is needed rather than exposing the existing swapper.
- `Assets/Animations/UseSlot.anim` and a neutral reach placeholder if required for generic UseReach state timing; action-owned IK completion, not placeholder duration, determines reach completion.
- `Assets/InteractionProfiles/`: NPC/Door categories; Door standalone profile using existing grab settings and DoorGrabTiming; Syringe and Tray held-use profiles referencing existing NPC clips.
- `Assets/Documentation/UseInteractions.md`: concise setup, rules, timing and migration instructions.
- Focused tests under existing `Assets/Tests/EditMode/` and `Assets/Tests/PlayMode/`, respecting actual assembly references before authoring tests.

## Modified assets / impact
Use.cs; GrabClipSwapper.cs; ContextLibrary.cs; MetaHuman.controller; NPC, Door, Tray and Syringe prefabs under Assets/Resources/Prefabs; TwoAgents scene for scene overrides and NPC category assignments. Modify MoveAndGrab only if a compatibility shim is necessary; otherwise remove its attachment from the obsolete Door state and retain the unused script until reference checks prove deletion safe. Syringe object controller may need repeatability transitions. Tray object controller may be configured only if an existing compatible object-use clip can be found; missing artwork/clip is a reported limitation, not justification to fabricate animation.

Shared MetaHuman.controller and NPC prefab also affect UserTest, TraumaRoom, HospitalEnvironment, SampleScene, AnimationTestScene, Inverse_Kinematics, RemapTest, Test scene and the Proto_1 test scene. Inspect affected use-capable instances for migration needs; preserve unrelated scenes and recovery files. No mass rewrite of unrelated scene configuration.

Dependency tooling available during planning: AssetDatabase.GetDependencies and reverse dependency scan via read-only editor inspection. Repeat focused impact/reference checks before edits, including source call-site searches because serialized asset dependencies do not capture C# callers.

# Implementation Steps
1. **Confirm migration and test assembly details.** Inspect existing test asmdefs, NPC runtime initialization, use-capable objects in dependent scenes, imported Tray object clips and prefab overrides. Document existing timing/clip assignments. Dependencies: approval, gathered context. Do not add packages, compatibility defines or alter package sources.
2. **Implement authoring data and resolver.** Add category/profile/interactable/resolver files under Actions/Using. Use direct component/asset references. Implement compatible held use, standalone fallback and rejection, preserving explicit legacy usableOn. Validate clip vs IK mode, required NPC/held animation references and optional target responses. Dependencies: Step 1.
3. **Establish one per-NPC override controller.** Adapt GrabClipSwapper and add the small common ownership helper if needed; NPCUseController must use the same controller instance, regardless of initialization order. Preserve unrelated override entries and release runtime-owned controllers safely. Add generic Use clip slot and UseReach/Empty states to ItemUse; stop depending on object-name states. Clip playback completes at the intended end, not the previous arbitrary 0.75 exit. Dependencies: Step 2.
4. **Implement coordinated use execution.** Refactor Use.TryBuild to preserve API while invoking a per-action resolved session at start. Play NPC motion and held animation, route target response at configured timing, and yield the full clip/reach operation. Transfer reach ownership from MoveAndGrab to this session, preserving existing GrabSpot/root fallback and actual UseLater contact. Use AnimAction.end plus local cleanup for errors/disable; once-only callbacks, bounded state-entry waits, and per-NPC use locking. Ensure hand attachment is unchanged in standalone fallback. Dependencies: Steps 2–3.
5. **Keep context truthful.** Update ContextLibrary.GetItemData's usableOn reporting to enumerate currently compatible category targets plus explicit legacy references using the same resolver. Preserve public context shape where possible. Avoid granting Syringe/Tray permission on Door just to demonstrate three-participant animation; only NPC category permissions and explicit exceptions are migrated into production assets. Dependencies: Step 2.
6. **Migrate existing examples.** Assign Door profile/response Animator/GrabSpot and Door category to Door prefab; migrate Tray/Syringe profiles and Animator references; add NPC category and playback component to NPC prefab and scene overrides as necessary. Wire TwoAgents instances. Check other affected use-capable objects before retiring named states; migrate known existing interactions to profiles so shared-controller changes do not silently break dependent scenes. Resolve repeat-use Syringe transition behavior narrowly if needed; flag missing Tray prop clip if no compatible source exists. Dependencies: Steps 3–5.
7. **Finish authoring feedback and docs.** Add field tooltips/conditional validation for unsupported categories, missing NPC clips, trigger mismatches and invalid response Animator hierarchy. Explain creating a profile, assigning it once to a prefab, Start vs Contact timing, standalone fallback and legacy eligibility. No broad custom UI framework. Dependencies: Steps 2–6.
8. **Focused verification and cleanup.** Run branch-resolution tests and a small Play Mode scenario covering Door empty-handed, Door while holding an incompatible Tray/Syringe, a compatible held item on NPC, and a temporary configured item-to-environment interaction with all three animations. Confirm two NPCs independently swap different clips and grab/release still work. Remove temporary testing objects/tools and report the Tray clip limitation if applicable. Dependencies: Steps 4–7.

# Verification & Testing
- Compile with no new errors. Follow existing test assembly boundaries; do not move unrelated source into assemblies merely to ease testing.
- Resolution checks: category match; explicit legacy match; empty rules reject; incompatible held item selects target standalone; neither usable rejects with no side effects; destroyed/null references handled; actual held object works even without a ContextItem registration.
- Door in TwoAgents: empty hands performs full reach, exactly one UseLater at contact; subsequent use toggles the door again. Queued runAfter action starts only after reach/return completes.
- Door while holding incompatible Tray or Syringe: same standalone reach/target behavior; held prop receives no Use, remains attached and returns to its hold presentation.
- Compatible held use on NPC target: NPC animation and held-item animation occur together; lack of a target response does not trigger the target NPC's ordinary body Animator or prevent use.
- Temporary configured compatible item/environment test: NPC plus held item plus environment response; contact-timed response fires once. Remove fixtures after checking; no artificial compatibility permissions committed to production profiles.
- Duplicate/rename Door and duplicate a held item: profile behavior survives renaming; duplicate target works through category membership without adding animator states. Command lookup still requires distinct registered object names, as before.
- Two NPCs use different profiles concurrently: no cross-agent override/trigger leakage. Subsequent grab, hold and release retains correct clips and parenting.
- Essential lifecycle cases: repeated uses restart, busy hand/use rejects predictably, destroyed target/lost held ownership stops pending effects, missing required configuration ends with an actionable error rather than hanging.
- Inspect serialized assignments and animation trigger types. Missing optional environment Animator is a no-op; missing Tray prop clip is reported explicitly if it cannot be supplied from existing assets.
- Keep verification proportionate: stop once requested behavior and essential grab/queue regressions pass; no unnecessary build/profiling campaign.
