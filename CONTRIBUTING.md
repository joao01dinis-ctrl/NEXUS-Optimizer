# Contributing

NEXUS Optimizer is an early experimental project. Read README.md and VALIDATION.md
for implemented features and known limitations before proposing changes.

Use Windows x64 and the SDK version in global.json. Run `./build.ps1 -Publish`.
Submit focused pull requests with the problem, change and validation described.
Contributions are provided under the project's MIT licence.

Keep changes reversible. Do not introduce automatic registry tweaks, security
disablement, process termination, file deletion, telemetry or privileged services.
New system-changing operations need an explicit user flow and tested recovery.
Never include credentials, local databases, personal logs or private paths in commits.

Dependencies retain their respective licences. Code signing and Windows policy
approval are separate from compilation; never claim a successful GUI test merely
because a build passed.
