# Changelog

All notable changes to `PromptHelm.Sdk` are documented in this file.
The format follows [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/).

## [0.2.0] - 2026-06-06

### Changed
- **BREAKING:** error response fields aligned to the real gateway envelope
  (`errorCode`, `requestId`, `timestamp`); the previous `code` / `correlationId`
  names were removed.
- User-Agent standardized to `prompt-helm-sdk-dotnet/<version>`.

## [0.1.0] - 2026-05-13

### Added

- `PromptHelmClient` with `ExecuteAsync` and `StreamAsync`.
- Multi-target `net8.0` and `netstandard2.0` (Unity, .NET Framework 4.6.1+, Xamarin).
- `IPromptHelmClient` interface and `AddPromptHelm` DI extension.
- Typed exceptions: `AuthenticationException`, `AuthorizationException`,
  `NotFoundException`, `RateLimitException`, `ApiException`, `PromptHelmTimeoutException`.
- Exponential-backoff retry for transient (5xx, network) failures.
- Server-Sent Events parser with `ChunkEvent`, `DoneEvent`, `ErrorEvent`.
- API key validation (`phk_` + 32 hex chars).
- Symbol package (`.snupkg`) and SourceLink for debugging consumers.
