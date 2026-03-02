# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [3.0.4](https://github.com/csharp-compound-engineering/csharp-compound-engineering/compare/v3.0.3...v3.0.4) (2026-03-02)

### Bug Fixes

* **infra:** create security groups inline in Crossplane workspace modules ([#59](https://github.com/csharp-compound-engineering/csharp-compound-engineering/issues/59)) ([f9b2042](https://github.com/csharp-compound-engineering/csharp-compound-engineering/commit/f9b20421cdd7891cae16bbc2100f3cb75cdd128c))

## [3.0.3](https://github.com/csharp-compound-engineering/csharp-compound-engineering/compare/v3.0.2...v3.0.3) (2026-03-02)

### Bug Fixes

* **infra:** upgrade ArgoCD chart to 9.4.6 and switch EKS nodes to ARM64 Graviton ([#58](https://github.com/csharp-compound-engineering/csharp-compound-engineering/issues/58)) ([d878b13](https://github.com/csharp-compound-engineering/csharp-compound-engineering/commit/d878b133a28151b0c3c9432f244966328547b6be))

## [3.0.2](https://github.com/csharp-compound-engineering/csharp-compound-engineering/compare/v3.0.1...v3.0.2) (2026-03-02)

### Bug Fixes

* **infra:** add ArgoCD Crossplane sync fix, VPN-to-EKS access, and password handling improvements ([#57](https://github.com/csharp-compound-engineering/csharp-compound-engineering/issues/57)) ([df733c4](https://github.com/csharp-compound-engineering/csharp-compound-engineering/commit/df733c496937e473567e9eed1cb2eed69f582aff))

## [3.0.1](https://github.com/csharp-compound-engineering/csharp-compound-engineering/compare/v3.0.0...v3.0.1) (2026-03-02)

### Bug Fixes

* **infra:** move ExternalDNS IAM from prereqs to network phase ([#56](https://github.com/csharp-compound-engineering/csharp-compound-engineering/issues/56)) ([48dbef4](https://github.com/csharp-compound-engineering/csharp-compound-engineering/commit/48dbef49f47846d4c15bdc508895833bec1e78aa))

## [3.0.0](https://github.com/csharp-compound-engineering/csharp-compound-engineering/compare/v2.1.0...v3.0.0) (2026-03-02)

### ⚠ BREAKING CHANGES

* Neptune configuration changes from serverless NCU settings to instance class.
GitSync is no longer a background service in the MCP server.

Co-Authored-By: Claude Opus 4.6 <noreply@anthropic.com>

### Features

* **mcp:** consolidate worker into mcp server as background service ([#48](https://github.com/csharp-compound-engineering/csharp-compound-engineering/issues/48)) ([0ae18ee](https://github.com/csharp-compound-engineering/csharp-compound-engineering/commit/0ae18ee2caa4d67a74b7cac381ec9bcf6e678e46))
* extract interfaces for all concrete-only DI registrations ([fd7dd28](https://github.com/csharp-compound-engineering/csharp-compound-engineering/commit/fd7dd28c4c700ea9f7d13df4691ba3b5f61b6716))
* **infra:** restructure OpenTofu into k8s/ subdirectory, add serverless config + image digest pinning ([#54](https://github.com/csharp-compound-engineering/csharp-compound-engineering/issues/54)) ([4635f89](https://github.com/csharp-compound-engineering/csharp-compound-engineering/commit/4635f89efa6858801ced48a57b8d2d682ad277ad))

### Documentation

* **community:** add community health files for open source release ([ce1648f](https://github.com/csharp-compound-engineering/csharp-compound-engineering/commit/ce1648f0f30f25287e7422f99453fdac264e8873))
* add conventional commits reference and community file links ([34b30c7](https://github.com/csharp-compound-engineering/csharp-compound-engineering/commit/34b30c79d71e1e1024ad22192745f1a56acdd1e2))
* **docker:** fix stale docker references, oci label, and port across docs ([#46](https://github.com/csharp-compound-engineering/csharp-compound-engineering/issues/46)) ([7066051](https://github.com/csharp-compound-engineering/csharp-compound-engineering/commit/7066051b20c498952a39bdae9c66a987845f90d2))
* remove non-architecture sections from architecture page ([7bf4ed6](https://github.com/csharp-compound-engineering/csharp-compound-engineering/commit/7bf4ed6fcbe53591fe278dd8ec66da2c9dbe2ce1))
* update docs site dependencies to fix security vulnerabilities ([#53](https://github.com/csharp-compound-engineering/csharp-compound-engineering/issues/53)) ([8d759ad](https://github.com/csharp-compound-engineering/csharp-compound-engineering/commit/8d759add1f7b30032d7cb085105f519d46604ee5))

### Code Refactoring

* dual-mode MCP server (k8s + lambda), .NET 10, provisioned Neptune, GitSync separation ([6ee131b](https://github.com/csharp-compound-engineering/csharp-compound-engineering/commit/6ee131b15c5cc6b52e56009e68ca8ecc2a8abb23))

## [2.1.0](https://github.com/michaelmccord/csharp-compound-engineering/compare/v2.0.0...v2.1.0) (2026-02-15)

### Features

* **infra:** restructure Helm chart and OpenTofu for VPC endpoints, pod identity, and resource ordering ([2998542](https://github.com/michaelmccord/csharp-compound-engineering/commit/299854217f2f5224696675f241efd88f3e177af2))

## [2.0.0](https://github.com/michaelmccord/csharp-compound-engineering/compare/v1.0.10...v2.0.0) (2026-02-14)

### ⚠ BREAKING CHANGES

* **tests:** Removes CompoundDocs.AppHost, CompoundDocs.ServiceDefaults,
and CompoundDocs.Tests.AppHost projects from the solution.

### Features

* **tests:** remove .NET Aspire and add real integration/E2E test coverage ([c05a866](https://github.com/michaelmccord/csharp-compound-engineering/commit/c05a8663917a039883fa9e796bc2093336caf577))

## [1.0.10](https://github.com/michaelmccord/csharp-compound-engineering/compare/v1.0.9...v1.0.10) (2026-02-14)

### Bug Fixes

* **release:** configure git credentials for gh-pages deployment ([341d90c](https://github.com/michaelmccord/csharp-compound-engineering/commit/341d90cb8c842860d664f15ebc8308e54e9db9a5))

## [1.0.9](https://github.com/michaelmccord/csharp-compound-engineering/compare/v1.0.8...v1.0.9) (2026-02-14)

### Bug Fixes

* **tests:** consolidate test projects and unify code coverage pipeline ([d42069d](https://github.com/michaelmccord/csharp-compound-engineering/commit/d42069d7d3d2dcd863787ef5e4cf0f0fcafb7a9e))

## [1.0.8](https://github.com/michaelmccord/csharp-compound-engineering/compare/v1.0.7...v1.0.8) (2026-02-13)

### Bug Fixes

* **scripts/release-prepare.sh:** Ensure tests are run during preparation ([a250968](https://github.com/michaelmccord/csharp-compound-engineering/commit/a250968be2d016ed6580f8e3496b6a68065dff45))

## [1.0.7](https://github.com/michaelmccord/csharp-compound-engineering/compare/v1.0.6...v1.0.7) (2026-02-13)

### Bug Fixes

* **release:** add docs/out placeholder for gh-pages plugin verifyConditions ([d2360ac](https://github.com/michaelmccord/csharp-compound-engineering/commit/d2360ac042c6c8fb3ab1927ff368307f49f3813f))

### Code Refactoring

* **release:** consolidate workflow and harden build pipeline ([7c4865b](https://github.com/michaelmccord/csharp-compound-engineering/commit/7c4865b58922e97b4cec1675cc18abccb9a77208))

## [1.0.6](https://github.com/michaelmccord/csharp-compound-engineering/compare/v1.0.5...v1.0.6) (2026-02-13)

### Bug Fixes

* Innocuous change ([1b8b1ba](https://github.com/michaelmccord/csharp-compound-engineering/commit/1b8b1ba2c9b723d43d568aedec7f8ced0f1e2949))

## [1.0.5](https://github.com/michaelmccord/csharp-compound-engineering/compare/v1.0.4...v1.0.5) (2026-02-09)

### Bug Fixes

* **deps:** update remaining outdated dependencies ([3f6fe95](https://github.com/michaelmccord/csharp-compound-engineering/commit/3f6fe95822b6f3901fb289bd7d8822ba47256170))

## [1.0.4](https://github.com/michaelmccord/csharp-compound-engineering/compare/v1.0.3...v1.0.4) (2026-02-09)


### Bug Fixes

* **docs:** test release pipeline ([e831dab](https://github.com/michaelmccord/csharp-compound-engineering/commit/e831dab6836bed7dc617548698d196db1b8ffb2f))

## [1.0.3](https://github.com/michaelmccord/csharp-compound-engineering/compare/v1.0.2...v1.0.3) (2026-02-09)


### Bug Fixes

* **deps:** re-add Dependabot with improved config and update outdated packages ([bc07d0e](https://github.com/michaelmccord/csharp-compound-engineering/commit/bc07d0ec44bca57e8fa13dab5d9db1faaeb673bf))

## [1.0.2](https://github.com/michaelmccord/csharp-compound-engineering/compare/v1.0.1...v1.0.2) (2026-02-09)


### Bug Fixes

* **docker:** suppress SourceLink warnings and remove Dependabot ([fb0d56e](https://github.com/michaelmccord/csharp-compound-engineering/commit/fb0d56e4b6157e65e4f5c9c493868bb1f7f1198e))

## [1.0.1](https://github.com/michaelmccord/csharp-compound-engineering/compare/v1.0.0...v1.0.1) (2026-02-09)


### Bug Fixes

* **release:** remove NuGet package production and publishing ([407a389](https://github.com/michaelmccord/csharp-compound-engineering/commit/407a3894d4c79266602ac26fd2cff5f5ffa97a4e))

## 1.0.0 (2026-02-09)


### Features

* Initial codebase ([7800692](https://github.com/michaelmccord/csharp-compound-engineering/commit/78006922a062648184530c11256a0a078dcb1307))
