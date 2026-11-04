# Disposable Windows Job Object experiment

Public, standalone synthetic test. No TFST source, production identifiers, credentials, client information, VPS connection or original Windows keeper interaction.

Scope: a GitHub-hosted Windows 2025 runner creates an anonymous Job Object and two local synthetic subprocesses. One test checks that a failed Job Object assignment never resumes a suspended worker. The other checks that a worker launched suspended is assigned before resumption, is accounted for while active and removed from the active-process count after normal exit.

This laboratory is not a test of the original keeper, console-host parentage, recovery, WordPress, Portal installation or live login. A pass only validates the specified synthetic native checks. The workflow runs on pushes to this experiment branch only, with read-only repository permissions and no secrets.
