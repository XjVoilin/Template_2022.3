# Template 2022.3

Unity 2022.3 seed project and composition root for July Framework.

- Production July packages resolve from GitHub URLs pinned to the immutable `v0.1.0` framework tag.
- The committed manifest contains only the template's runtime dependency closure and explicitly selected build, Spine, and UI authoring tools.
- Platform and analytics contracts are installed because the template composes project-owned SDK adapters on top of them.
- Activity, diagnostics, experiments, guide, networking, red-dot, and task packages are optional installations.
- Framework contributors may temporarily use local `file:` references, but those overrides must not be committed.
- UniTask, DOTween, YooAsset, HybridCLR, and Luban are embedded at fixed versions.
- Project-specific configuration, providers, generated tables, input, scenes, and assets stay in this composition layer.

## Framework validation

The production manifest does not enable package test assemblies. Close the project in Unity, then run:

```powershell
powershell -ExecutionPolicy Bypass -File .\Tools\CI\Invoke-JulyValidation.ps1
```

The script temporarily installs all 28 July packages, adds the nine package names that expose tests to `testables`, runs compilation plus EditMode and PlayMode tests, and restores the production manifest and lock file in a `finally` block.

Before changing a framework revision, update every selected July Git URL to one compatible immutable tag and regenerate `Packages/packages-lock.json` through Unity.

## SDK integrations

The template includes macro-isolated adapters for WeChat 0.1.33, Douyin TTSDK 6.7.4, and ThinkingData 3.4.2, but it does not vendor those SDKs. See [SDK integration setup](Docs/SDK_INTEGRATIONS.md) for installation paths, version gates, macros, configuration, and deliberately excluded mini-game-box features.

## Build and release

Reusable full-build and content-update pipelines compose `July.Build`, HybridCLR, YooAsset, release manifests, and optional COS upload without importing GooseMarket's mini-game-box workflow. See [build and release](Docs/BUILD_AND_RELEASE.md).
