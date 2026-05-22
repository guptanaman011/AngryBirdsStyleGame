# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project

Unity **6000.4.1f1** (Unity 6) 2D physics game — an Angry Birds clone. The whole game lives in a single scene: `Assets/Scenes/game.unity`. Gameplay code is C# under `Assets/Scripts/` (the MonoBehaviours `Bird`/`Pig`/`Brick`/`GameManager`/`SlingShot` sit in the global namespace; shared types like `Enums`/`Constants` use namespace `Assets.Scripts`). The ad-mediation code lives in a separate embedded package — see **Ad mediation** below.

> **Long-term direction:** the game is the host app for prototyping an ad-mediation SDK. That SDK has been extracted into its own repo (`unity-ad-mediation-sdk`) and is consumed here as an embedded Unity package via **git submodule** at `Packages/com.guptanaman.ad-mediation/` (assembly `GuptaNaman.AdMediation`). It is still **Phase 0** — actively developed in place. The vision, gap analysis, and phased roadmap are in `Packages/com.guptanaman.ad-mediation/Documentation~/SDK_GOAL.md` — read this before suggesting structural changes to the ad code. The detailed mechanism doc is `Packages/com.guptanaman.ad-mediation/Documentation~/AD_MEDIATION_PRICE_FLOOR.md`.

There is no build/test CLI — open the project in Unity 6000.4.1f1 and use the editor. Play the `game` scene to run; build via **File → Build Settings**. The `build/` folder in the repo is a previous macOS build artifact.

## Architecture

### Game-state machine
`GameManager.Update()` is a switch over `GameState` (`Enums.cs`): `Start → BirdMovingToSlingshot → Playing → Won|Lost`. `SlingShot.Update()` is a parallel switch over `SlingshotState` (`Idle → UserPulling → BirdFlying`). The two state machines are coupled — `GameManager` enables/disables `slingshot` and reads `slingshot.slingshotState` to decide when the shot is over.

A shot is considered "over" when either everything has stopped moving (`BricksBirdsPigsStoppedMoving` checks `Rigidbody2D.linearVelocity.sqrMagnitude > Constants.MinVelocity` across all `Brick`/`Bird`/`Pig` tagged objects) **or** 5 seconds have elapsed since throw. `GameManager` then DOTween-animates the camera back, decides Won/Lost/next-bird, and shows an interstitial.

Object discovery is by **Unity tag** at `Start()` — `Brick`, `Bird`, `Pig`. Adding gameplay entities means tagging them correctly, not registering them in code.

### Slingshot physics
`SlingShot.cs` handles input directly (no Input System package — uses legacy `Input.GetMouseButton*`). It clamps the bird's pull to 1.5 units from the slingshot midpoint, then sets `Rigidbody2D.linearVelocity` directly on release (note: the API name `linearVelocity` is the Unity 6 rename of the old `velocity`). The trajectory preview (`DisplayTrajectoryLineRenderer2`) integrates `Physics2D.gravity` forward over 15 segments — if you change `ThrowSpeed` or gravity, the preview tracks automatically.

### Damage model
`Brick` and `Pig` both compute `damage = collidingRigidbody.linearVelocity.magnitude * 10` in `OnCollisionEnter2D`. `Pig` has a special case: any collision tagged `Bird` is an instant kill. `Pig` swaps to a "hurt" sprite when `Health` drops below `initialHealth - 30`. The 10× multiplier and the `damage >= 10` audio threshold are magic numbers in those scripts — adjust there, not in `Constants.cs` (which only holds velocity/collider-radius constants).

### Ad mediation
**The ad code is an embedded package** at `Packages/com.guptanaman.ad-mediation/` — a git submodule of the `unity-ad-mediation-sdk` repo (assembly `GuptaNaman.AdMediation`, auto-referenced so callers need no asmdef wiring). Edit the package in place, but commit/push those changes to the **SDK repo** (`cd` into the submodule), not the game repo; the game repo only records the submodule's commit pointer.

`AdManager` (static facade in namespace `Assets.Scripts`, shipped in that package) is the only thing gameplay code should call. It delegates to two persistent MonoBehaviour singletons created lazily via the `EnsureExists()` pattern (`FindObjectOfType` → otherwise `new GameObject` + `DontDestroyOnLoad`):

- `LevelPlayInterstitialController` — runs a **parallel auction** between ironSource/LevelPlay and Google Ad Manager interstitials. Both legs load concurrently; on show, whichever is ready with the higher estimated eCPM wins (`TryFinalizeAuction`). A fail-safe `auctionTimeoutSeconds` coroutine prevents gameplay from blocking forever. Gameplay continuation is threaded through an `Action onContinue` callback that fires on ad-close, display-fail, or no-fill.
- `GamBannerController` — standalone GAM banner.

GameManager triggers interstitials at three points in `AnimateCameraToStartPosition`'s `OnComplete`: on Won (no callback), on Lost/last-bird (no callback), or between birds (callback advances `currentBirdIndex` and animates the next bird in). **The continuation callback pattern matters** — if you bypass `AdManager.TryShowInterstitial(onContinue)` and call gameplay advance directly, the game will advance before/regardless of the ad.

The package ships ad unit IDs / app keys **blank** (`[SerializeField]` defaults) — an SDK must not carry a publisher's credentials. The host game supplies them on a scene GameObject: an `Ads` GameObject in `game.unity` carries `LevelPlayInterstitialController` + `GamBannerController` components with the real IDs set in the Inspector, and `EnsureExists()` finds that instance via `FindObjectOfType`. With no such GameObject the controllers are created blank at runtime and fall back to the ad SDKs' public sample units.

### Third-party packages in `Assets/`
- `Plugins/Demigiant/` — **DOTween**, used for all tween animations (`transform.DOMove(...).OnComplete(...)`). DOTween's `OnComplete` callbacks are how `GameManager` chains state transitions.
- `GoogleMobileAds/`, `ExternalDependencyManager/`, `LevelPlay/`, `Plugins/Android/`, `Plugins/iOS/` — vendored ad SDKs (Google Mobile Ads, EDM4U, Unity LevelPlay). These are committed to the repo, not pulled via UPM. The UPM manifest (`Packages/manifest.json`) only lists Unity-first-party packages plus `com.unity.purchasing`; the ad-mediation SDK is an **embedded package** under `Packages/com.guptanaman.ad-mediation/` (a git submodule), not a manifest dependency.

## Conventions worth knowing

- `Rigidbody2D.linearVelocity` (Unity 6 API) is used throughout; do not "fix" it to `velocity`.
- Game objects are looked up by **tag**, not by name or reference — new birds/bricks/pigs must be tagged at authoring time.
- Camera follow is x-axis only and clamped to `[0, 13]` in `CameraFollow.cs` — that range is the level's playable horizontal extent.
- `OnGUI` in `GameManager` uses the legacy IMGUI system with a manual `AutoResize(800, 480)` matrix; UI Toolkit / uGUI is not used.
- Sorting layer `Foreground` is set in code on `TrailRenderer` and slingshot/trajectory `LineRenderer`s — the layer must exist in the scene's Tags & Layers settings.
