# Phone Messages Save/Load v1 implementation plan

**Goal:** Integrate the existing message snapshot into the existing save path, with one scene-owned runtime instance.

**Architecture:** A single `PhoneMessagesBehaviour` exposes one readonly `PhoneMessages` instance. `GameSaveController` receives that owner by serialized reference, captures it into `GameSaveData.Messages`, preflights the snapshot on a temporary unobserved domain instance, and restores the live owner after the other existing sections. No new save architecture or core changes.

**Constraints:** No UI, seed, navigation, Shop, scene/prefab changes, migrations or commit. Old save version 1 is rejected; game save version becomes 2. Snapshot's own version stays 1. Explicit Inspector setup is authorized by the user.

**Files:** `PhoneMessagesBehaviour.cs` (new production), `GameSaveData.cs`, `GameSaveController.cs`; new Editor integration tests and documentation.

## Execution checklist

- [x] Audit owners, save capture/validation/apply and all consumers; record existing work and file hashes.
- [x] Run baseline: 43 EditMode tests pass, Unity exit 0.
- [x] Write tests for real controller file capture/load using isolated EditMode fixtures and temporary files, not the user's saves.
- [x] Expose the minimal owner/snapshot/reference contract; run the capture test and observe missing snapshot failure before integration.
- [x] Add capture/restore, version 2, required owner and missing-section validation. Validate messages before any save state is applied.
- [x] Run full tests: history/read states/directions/times, multiple/empty contacts, duplicate protection after load, invalid snapshot atomic rejection, stable owner, old save and missing owner refusal.
- [x] Verify compile results, existing file hashes and no scene/prefab/core/UI changes. Document the exact Inspector assignment and remaining global-save limitations.

The owner has no callbacks or dependencies: a pure C# field initializer provides the same instance across enable/disable and avoids Awake ordering. Lifetime follows the scene component; no DontDestroyOnLoad/static state. Only the configured owner is authoritative; the temporary validation instance is discarded, never exposed or registered.

Tests explicitly initialize existing component backing state in EditMode to exercise the actual private TrySave/TryLoad path via reflection. They do not call Start and are not Play Mode/lifecycle claims. All test objects are temporary; no scene or prefab asset is saved.
