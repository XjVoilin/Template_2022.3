# Template 2022.3

Unity 2022.3 seed project and composition root for July Framework.

- Every production July package is pinned to its own immutable `com.july.<name>@<version>` Git tag.
- The committed manifest contains only the template's runtime dependency closure and explicitly selected build, Spine, and UI authoring tools.
- Platform and analytics implementations come from `com.july.platform` and `com.july.analytics.thinkingdata`; the template owns only provider selection and project configuration.
- Activity, diagnostics, experiments, guide, networking, red-dot, and task packages are optional installations.
- Framework contributors may temporarily use local `file:` references, but those overrides must not be committed.
- UniTask, DOTween, YooAsset, HybridCLR, and Luban are embedded at fixed versions.
- Project-specific configuration, provider selection, generated tables, scenes, and assets stay in this composition layer.

## Framework validation

The production manifest does not enable package test assemblies. Close the project in Unity, then run:

```powershell
powershell -ExecutionPolicy Bypass -File .\Tools\CI\Invoke-JulyValidation.ps1
```

The script temporarily installs all 32 July packages, adds the 12 package names that expose tests to `testables`, runs compilation plus EditMode and PlayMode tests, and restores the production manifest and lock file in a `finally` block.

When a package changes, update only that package's Git tag and regenerate `Packages/packages-lock.json` through Unity. If an interface change requires dependent package updates, release and update those dependent packages together.

## SDK integrations

The template selects the framework-provided WeChat, Douyin, and ThinkingData adapters. The host project still supplies WeChat 0.1.33 and Douyin TTSDK 6.7.4; the ThinkingData adapter package bundles SDK 3.4.2. See [SDK integration setup](Docs/SDK_INTEGRATIONS.md).

## Build and release

Reusable full-build and content-update pipelines compose `July.Build`, `July.Build.HybridCLR`, YooAsset, release manifests, and optional COS upload without importing GooseMarket's mini-game-box workflow. See [build and release](Docs/BUILD_AND_RELEASE.md).
