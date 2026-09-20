# Phone Messages Core v1 implementation plan

> Execution: superpowers:executing-plans, in the current project. No delegation or branch changes.

**Goal:** Add a small pure C# messages domain, with explicit incoming/outgoing commands and detached, validated snapshots.

**Architecture:** PhoneMessages owns conversation history. Public conversation/message projections cannot mutate it. GameTimeSnapshot is supplied by the caller; localization keys or literal text are resolved by presentation later. Snapshot DTOs have no Unity dependencies.

**Tech stack:** Unity 6000.6.0f1, existing C#, NUnit EditMode tests. No new package or assembly boundaries.

## Constraints

- Two new production files maximum; no new MonoBehaviour, manager, service or input layer.
- No changes to existing production files, scene, prefab, hierarchy, UI or localization assets.
- Preserve all existing dirty work. No commit/push.
- Save has no messages section or composition owner: provide snapshot API, document later integration; leave save schema/version unchanged.

## Audit findings

- PhoneSession owns screen navigation; PhoneBehaviour/PhonePointerBehaviour own the physical interaction.
- PhoneScreenView currently displays mock localized history and a presentation unread value. Leave it unchanged.
- GameClock.Current supplies GameTimeSnapshot.TotalSeconds; no wall clock or UI-owned timestamp.
- LocalizationContext resolves catalog keys. The domain must also accept literal outgoing content for future player replies.
- GameSaveController captures a fixed version-1 GameSaveData. No message owner or registration hook exists.
- No existing project test sources were found. Record a fresh compile and add focused PhoneSession baseline tests before changing production code.

## Files and responsibility

- `Assets/Game/Scripts/Phone/PhoneMessages.cs`: message/content/direction values, read-only conversation, authoritative collection and commands, Changed event after commit. Single-threaded, explicit instance ownership; no hidden initial messages.
- `Assets/Game/Scripts/Phone/PhoneMessagesSnapshot.cs`: serializable detached DTOs with stable IDs, content, seconds, direction and read state; schema 1 independent of the game's save version.
- `Assets/Game/Tests/Editor/PhoneMessagesTests.cs`: domain cases, snapshot round-trip/validation, unchanged PhoneSession regression checks.
- `docs/PhoneMessagesCore.md`: API usage, invariants, results and the deferred UI/save composition steps.

## Execution

- [x] Capture working tree and existing file hashes; run Unity baseline compilation.
- [x] Run PhoneSession baseline tests in the installed Unity Test Runner.
- [x] Add tests before implementation: unread/counts, read command, arbitrary outgoing, contact separation, append order, global duplicate IDs, detached snapshot and atomic restore validation.
- [x] Run the first incoming-message test against the minimal API and confirm the expected failure.
- [x] Implement commands. Incoming is unread, outgoing is read by the player; successful add creates its conversation if absent. Contact IDs use ordinal comparison. Duplicate IDs are global and never overwrite history.
- [x] Validate every restored record in temporary collections before replacing live state. No serialized cached counters or live collection references.
- [x] Run all EditMode tests including JsonUtility round-trip. Compile in Unity again; inspect logs and compare protected file hashes.
- [x] Document the exact results and remaining save/UI hookup. Do not claim Play Mode or player build verification.
