# Synthetic console identity and worker-drain test

Single GitHub-hosted Windows laboratory on the existing synthetic branch.
The test builds on the already passing pseudoconsole reproduction and adds
managed OS process observations: direct-child parentage from a Windows process
snapshot, running process start time, process image path and repeated identity.

Negative cases modify **synthetic observations only** to reject an altered
process start time, fake image path, incorrect parent and extra child. A real
test-owned running worker must also prevent simulated completion.

No printed Windows account identifiers, process IDs, paths or secrets.
No files outside this synthetic branch, no TFST original keeper, no Portal
or VPS contact. A PASS is NOT evidence of actual keeper recovery.

LIMITATION: Process name, path, parentage and timestamps are useful checks
but do not cryptographically bind a conhost process to a specific pseudoconsole.
These tests do not certify a future live exclusion rule.
