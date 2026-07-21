# Template 2022.3

Unity 2022.3 seed project and composition root for July Framework.

- July packages resolve from GitHub URLs pinned to the immutable 0.1.0 framework tag.
- Framework contributors may temporarily use local ile: references, but those overrides must not be committed.
- UniTask, DOTween, YooAsset, HybridCLR, and Luban are embedded at fixed versions.
- Project-specific configuration, providers, generated tables, input, scenes, and assets stay in this composition layer.
- The project also provides integration, launch, and build verification for the framework package combination.
- The manifest exposes package-level tests from the nine packages that currently provide Tests~ assemblies.
- Before distributing the template independently, replace local July package paths with a scoped-registry version or Git URLs pinned to one immutable framework revision.
