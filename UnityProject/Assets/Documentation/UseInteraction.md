# Use Interaction System Documentation

## 1. How the System Works (Overview & Flow)

The `use` action is split into **data assets** (what can be used, on what, and how it animates), **scene components** (what each object in the world can do), and **runtime execution** (resolving the interaction and playing animations safely).

### Core Pieces
| Component / Asset        | Role                                                                                                                                                                                                            | Where it lives                   |
| ------------------------ | --------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- | -------------------------------- |
| `Use` (`IAgentAction`)   | Entry point for `use <target>`. Resolves the target and actually held item at action start, calls `UseInteractionResolver.Resolve`, and starts `NPCUseController`.                                              | Action system                    |
| `UseInteractionResolver` | Decides which interaction mode applies (**held-on-target**, **self-use while held**, or **standalone**), validates motion and triggers, and exposes valid targets to `ContextLibrary`.                          | Static helper                    |
| `UseInteractionProfile`  | ScriptableObject defining NPC motion (`Clip` or `ReachIK`), speed, contact time, `CanUseOnSelf`, accepted target categories, and held-item trigger/timing.                                                      | `Resources/InteractionProfiles/` |
| `UseTargetCategory`      | ScriptableObject tag asset (e.g., `NPC`, `Door`) used to match held items to valid targets.                                                                                                                     | `Resources/InteractionProfiles/` |
| `UseInteractable`        | Component placed on the root of NPCs, held props, and target objects to declare categories, profiles, and response animation settings.                                                                          | Prefab / GameObject roots        |
| `NPCUseController`       | Per-NPC runtime player. Rejects busy hands/reaches, overrides the `ItemUse` animator slot or drives `GrabIK`, fires `Start` and `Contact` responses, and resets cleanly on completion, interruption, or cancel. | NPC root                         |

### Runtime Execution Flow
1. **Command starts (`Use.start`):** Looks up the target `Transform` and reads `agent.GetItem()` (the object actually in hand at that moment).
2. **Resolution (`UseInteractionResolver.Resolve`):**
   - Calls `CanUseHeldOn(heldInteraction, target, legacyItem)`:
     - **Self-use:** If `target == heldObject`, it matches **if and only if** `heldInteraction.HeldProfile.CanUseOnSelf` is `true`. In this case `TargetInteraction` in `ResolvedUseInteraction` is set to `null` so only the held-item response fires (avoiding duplicate/missing `TargetInteraction` calls on the same object).
     - **Held-on-target:** If `target != heldObject`, it matches if `legacyItem.usableOn` contains `target` or if `target`'s `UseInteractable` has any category in `HeldProfile.AcceptedCategories`.
     - If either matches, `Resolve` validates the profile motion (`ValidateMotion()`) and verifies that the held item's `ResponseAnimator` has `HeldItemTrigger`.
   - **Standalone fallback:** Otherwise, if `target` has a `StandaloneProfile`, it validates motion and resolves with `HeldInteraction = null`.
   - Otherwise, it returns an error.
3. **Validation (`NPCUseController.Begin`):** Rejects the use if the NPC is already using something, if `HandController.State` is `Grabbing` or `Releasing`, or if `GrabIK.IsReaching` is already true.
4. **Playback (`NPCUseController.RunInner`):**
   - **`UseMotion.Clip`:** Swaps the placeholder `UseSlot` clip on the shared `AnimatorOverrideController` (`NPCAnimatorOverrides`) with `profile.NpcClip`, sets `UseSpeed`, plays the `Use` state on `NPC_AnimLayer.ItemUse`, fires `Start` responses, watches `normalizedTime` to fire `Contact` responses at `profile.ContactTime`, and cleans up when `normalizedTime >= 1f`. (Also fails cleanly if the target is destroyed, the held item is lost, or the animation is interrupted.)
   - **`UseMotion.ReachIK`:** Plays `UseReach` on `ItemUse`, fires `Start` responses, and runs `GrabIK.ReachRoutine(...)` toward `InteractionPoint` (or target root), passing `OnContact` as the contact callback.
5. **Cleanup (`NPCUseController.Finish`):** Restores the `ItemUse` layer to `Empty`, clears the `UseSlot` override back to the placeholder, resets `UseSpeed` to `1`, cancels any active `GrabIK` reach if interrupted, and marks `UseSession.Done = true`.

---

## 2. What Are `UseTargetCategory` Assets?

`UseTargetCategory` assets (created via **Create > Interactions > Target Category**) are **reusable semantic tag assets**—for example, `NPC` or `Door`.

Instead of hardcoding object names or C# types in scripts:
- A **target** lists which categories it belongs to on its `UseInteractable` (e.g., an NPC has `[NPC]`).
- A **held item's `UseInteractionProfile`** lists which categories it can be used on in `Accepted Categories` (e.g., `Syringe_Use` accepts `[NPC]`). Empty `Accepted Categories` means **no category matches** (not "any target").
- For **self-use items** (`Can Use On Self = true`), no category is needed—matching happens directly via `held.transform == target`.

At runtime—and when `ContextLibrary` generates `usableOn` via `UseInteractionResolver.GetUsableTargets`—the resolver checks both `CanUseOnSelf` (self-use) and `AcceptedCategories` / legacy `usableOn` (held-on-target).

---

## 3. Fields of `UseInteractable`

`UseInteractable` sits on the **root GameObject** of an object (the same GameObject as `NPC`, `GrabbableItem`, or the registered `ContextItem` transform, because `UseInteractionResolver` calls `GetComponent<UseInteractable>()`).

| Field | What it does |
|---|---|
| **Categories** | List of `UseTargetCategory` assets describing *what kind of target* this object is (e.g., `[NPC]`, `[Door]`). Checked when another held item is used *on* this object. |
| **Held Profile** | The `UseInteractionProfile` used when an NPC is **holding this object** and runs `use <Target>` (either on another target or on itself if `CanUseOnSelf` is checked). |
| **Standalone Profile** | The `UseInteractionProfile` used when an NPC runs `use <ThisObject>` directly (when empty-handed or holding something that doesn't apply to this object). |
| **Response Animator** | The `Animator` on this object that receives a trigger when it reacts to a use. Required for held items (verified against `HeldItemTrigger`). Leave empty if this object has no self-animation (like an NPC being targeted). |
| **Response Trigger** | Name of the `Trigger` parameter on **Response Animator** fired when this object is the **target** of a use (e.g., `UseLater` on Door). *(Leave empty for held-only or self-use items—those use the profile's `HeldItemTrigger` instead.)* |
| **Timing** (`ResponseTiming`) | When this object's **Response Trigger** fires (`Start` or `Contact`) when acting as a **target**. |
| **Interaction Point** | Optional child `Transform` (e.g., the Door's `GrabSpot`) that `ReachIK` aims for. If empty, `NPCUseController` aims at the object's root `Transform`. |

---

## 4. What Is `ResponseTiming`? What Does `Contact` Mean?

`UseTiming` controls **when** an object's animation trigger fires relative to the NPC's motion:

- **`Start`:** Fires right as the use begins (immediately after the `ItemUse` layer enters `Use` in Clip mode, or immediately when `ReachRoutine` begins in ReachIK mode).
  - *Example:* `Syringe_Use` and `Tray_Use` use `Held Item Timing = Start`, so the prop animates in sync with the NPC's body animation.
- **`Contact`:** Fires at the moment the NPC's hand or tool makes contact with the target.
  - **In `ReachIK` mode:** `GrabIK.ReachRoutine` invokes `onContact` when the hand reaches `InteractionPoint` according to `GrabIKTimings`.
  - **In `Clip` mode:** There is no physical IK target callback, so `Contact` means **reaching `Profile.ContactTime`**—a normalized timestamp from `0` to `1` on `NpcClip` (e.g., `0.5` = halfway through the clip). `NPCUseController` fires `Contact` once `normalizedTime >= profile.ContactTime` (and guarantees it fires once on completion even if a frame jumps past `ContactTime`).
  - *Example:* The Door sets `Timing = Contact`, so its `UseLater` trigger fires when the NPC's hand reaches `GrabSpot`.

Held-item timing and target timing are configured separately:
- **Held item's timing** lives on **`UseInteractionProfile`** (`Held Item Timing`), because it belongs to the held-tool animation.
- **Target's timing** lives on the **target's `UseInteractable`** (`Timing` / `ResponseTiming`), because it belongs to that target's reaction.

---

## 5. Why Do Certain Interactables Have Categories and Others Don't?

Categories answer only one question: **"Can other held tools be used ON me?"**

- **NPCs have `[NPC]`:** Because held items like `Syringe` are meant to be used *on* an NPC.
- **Doors have `[Door]`:** Even though `Door_Use` is currently a standalone interaction (no held tool needed), tagging the Door with `[Door]` makes it targetable if you ever add a held item like a `Key` or `Keycard` whose `HeldProfile` accepts `[Door]`.
- **Syringe and Tray have NO categories:**
  - You don't use *other* items on a `Syringe` or `Tray`.
  - Even for **self-use** items like `Tray`, categories are unnecessary because `CanUseHeldOn` checks `held.transform == target` against `CanUseOnSelf` directly—and keeping `Categories` empty prevents another instance (e.g., `Tray_2`) from being treated as a valid target for `Tray_1`.

---

## 6. `HeldProfile` vs. `StandaloneProfile` (and Why Self-Use Uses `HeldProfile`)

These two fields represent whether an interaction requires holding the object or targets it directly in the world:

| Role | Setup | Example | Why |
|---|---|---|---|
| **Held tool used on a target** | `HeldProfile` assigned, `StandaloneProfile` `null` | `Syringe` (`Syringe_Use`, `AcceptedCategories = [NPC]`) | Must be held and used on an NPC (`use NPC1`), not activated empty-handed on a table. |
| **Self-use held tool** | `HeldProfile` assigned (`Can Use On Self = true`), `StandaloneProfile` `null` | `Tray` (`Tray_Use`, `Can Use On Self = true`) | Must be held in the NPC's hand to run `use Tray`. Empty-handed `use Tray` is rejected, and `NPCUseController` enforces `_npc.GetItem() == Tray` during playback. |
| **World object used directly** | `StandaloneProfile` assigned, `HeldProfile` `null` | `Door` (`Door_Use`) | Cannot be picked up; used directly (`use Door`) empty-handed or while holding an unrelated item. |
| **Pure target** | Both `null` | `NPC` (`Categories = [NPC]`) | Cannot be held or used standalone; only acts as a target for held items. |

**Why not put a self-use item's profile in `StandaloneProfile`?**
- `StandaloneProfile` allows an NPC with **empty hands** to run `use Tray` on the table without picking it up (and `ContextLibrary` would advertise `usableDirectly: true`).
- `NPCUseController` only checks that the item stays in hand (`_npc.GetItem() != use.HeldObject`) when `HeldInteraction != null`, which is only set for `HeldProfile` interactions.

**What happens if you hold a `HeldProfile` item and `use` a `StandaloneProfile` object (e.g., holding `Syringe` or `Tray` and running `use Door`)?**
Because neither `Syringe_Use` nor `Tray_Use` accepts `[Door]`, `CanUseHeldOn` returns `false`, and `Resolve` falls back to the Door's `StandaloneProfile` (`Door_Use`). The NPC reaches for the Door, `UseLater` fires on the Door, and the held prop receives no trigger and stays in hand.

---

## 7. How to Add a New Usable Item

### Pattern A: Held Item Used on a Target (3-Way Animation: NPC + Held Item + Target)
*Example: Using a `Keycard` on a `Scanner`, where the NPC, Keycard, and Scanner all animate.*

1. **Create a Target Category (if needed):**
   - **Create > Interactions > Target Category** (e.g., `Scanner`) in `Assets/InteractionProfiles/`.
2. **Create the `UseInteractionProfile` (e.g., `Keycard_Use`):**
   - **Motion:** `Clip` (assign `Npc Clip`, `Speed`, `Contact Time`) or `ReachIK` (assign `Reach Profile`, `Reach Timings`).
   - **Can Use On Self:** unchecked (`false`).
   - **Accepted Categories:** `[Scanner]`.
   - **Held Item Trigger:** Trigger name on the held item's Animator (e.g., `Use`).
   - **Held Item Timing:** `Start` or `Contact`.
3. **Configure the Held Item Prefab (`Keycard`):**
   - Give its `AnimatorController` a Trigger matching `Held Item Trigger` (e.g., `Use`) and an exit-time transition back to Idle if reusable.
   - Ensure it has `GrabbableItem` / `ContextItem`.
   - On the **item root**, add **`UseInteractable`**:
     - **Held Profile:** `Keycard_Use`
     - **Response Animator:** the item's own `Animator`
     - Leave `Categories`, `Standalone Profile`, `Response Trigger`, and `Interaction Point` empty.
4. **Configure the Target Object Prefab (`Scanner`):**
   - Give its `AnimatorController` a Trigger parameter (e.g., `Activate`).
   - On the **target root**, add **`UseInteractable`**:
     - **Categories:** `[Scanner]`
     - **Response Animator:** the target's `Animator`
     - **Response Trigger:** `Activate`
     - **Timing:** `Contact` (or `Start`)
     - **Interaction Point:** child `Transform` (e.g., `GrabSpot`) if using `ReachIK`, else empty.
     - Leave `Held Profile` and `Standalone Profile` empty (unless it can also be used empty-handed).

### Pattern B: Self-Use Held Item (2-Way Animation: NPC + Held Item in Hands)
*Example: `Tray` used by itself while held (`use Tray` while holding `Tray`).*

1. **Create the `UseInteractionProfile` (e.g., `Tray_Use`):**
   - **Motion:** `Clip` (assign `Npc Clip`, `Speed`, `Contact Time`).
   - **Can Use On Self:** checked (`true`).
   - **Accepted Categories:** leave empty (unless it should *also* be usable on another target category like `[NPC]`).
   - **Held Item Trigger:** Trigger name on the prop's Animator (e.g., `Use`).
   - **Held Item Timing:** `Start` or `Contact`.
2. **Configure the Held Item Prefab (`Tray`):**
   - Give its `AnimatorController` a Trigger matching `Held Item Trigger` (e.g., `Use`), ensure that state has an actual `AnimationClip` assigned, and add an exit-time transition back to Idle if repeatable.
   - On the **item root** (same GameObject as `GrabbableItem`), add **`UseInteractable`**:
     - **Held Profile:** `Tray_Use`
     - **Response Animator:** the prop's own `Animator`
     - Leave `Categories`, `Standalone Profile`, `Response Trigger`, and `Interaction Point` empty.
   - *(Note: When `Resolve` matches self-use, it sets `TargetInteraction = null`, so only `PlayHeldResponse` runs on the item and `PlayResponse` is skipped.)*
3. **Context & Play Mode Verification:**
   - `ContextLibrary` (`GetUsableTargets`) will report `usable: true` and `usableOn: [Tray]` (for Pattern B) or `usableOn: [Scanner]` (for Pattern A).
   - Running `use Tray` empty-handed will fail with `"no standalone use profile"`; grabbing `Tray` and running `use Tray` will play both the NPC clip and `Tray`'s `Use` trigger.