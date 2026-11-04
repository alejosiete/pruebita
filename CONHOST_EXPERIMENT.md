# Synthetic ConPTY / console-child test

Purpose: Test a **generic** Windows console-child deadlock hypothesis, not the TFST keeper. This public experiment contains no TFST source, client information, credentials, registry modifications, real mutexes or journals.

Scope: One GitHub-hosted Windows 2025 run on the isolated experiment branch. The synthetic .NET process verifies hosted-runner identity, snapshots **only its own direct-child relationship** (it emits no host identifiers), creates a true Win32 pseudoconsole with anonymous pipes, and checks whether Windows exposes a direct \`conhost.exe\` child. It launches and waits for a single synthetic worker. If only a console child remains after worker completion, the generic zero-direct-child gate remains blocked even though no synthetic worker remains. Closing the pseudoconsole should then retire the console child.

Results: LAB_PASS only when the genuine kernel-observed direct-child relationship and shutdown conditions are satisfied. LAB_INCONCLUSIVE if the runner gives different parentage or retention behaviour. LAB_FAIL for a test exception. No result is proof that the original protected Windows keeper can be recovered.

No mutations to TFST repositories, VPS or original Windows device. No workflow secrets or external services.
