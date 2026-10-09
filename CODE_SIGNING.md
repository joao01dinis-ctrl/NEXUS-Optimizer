# Code signing policy

## Current status

NEXUS Optimizer is an experimental, unsigned open-source application.
SignPath Foundation rejected the application on 7 October 2026 because the project
has not yet demonstrated established public trust/adoption. No certificate or
successful signing is claimed. Unsigned binaries may be blocked by Windows
application-control policies. Making the source public does not remove that block.

## Project responsibility

- Maintainer, reviewer and prospective release approver: `joao01dinis-ctrl`.
- External contributions require maintainer review before release.
- Release signing must require a manual maintainer approval.
- Maintainers must enable multi-factor authentication before onboarding to signing.
  The repository does not claim that account-level configuration has been verified.

## Distribution process

1. Build and test from an identified commit using the Windows CI workflow.
2. Preserve the build provenance and review the release contents.
3. Microsoft Store is the current preparation route: the reserved product and
   submission draft exist, but no package is certified or Store-signed yet. Store
   signing applies to the certified MSIX, not automatically to portable EXE/ZIP files.
   SignPath sponsorship may be reconsidered after verifiable public adoption.
4. Keep third-party components under their own licences and signatures.
5. Publish only after the maintainer approves the release and verifies signatures.

No signing secrets belong in this repository. Sponsorship attribution will be
added only once it is actually granted. The current CI publishes **unsigned**
development artifacts and does not attempt to bypass Windows security.

See [privacy policy](PRIVACY.md) and [SignPath requirements](https://signpath.org/terms.html).
