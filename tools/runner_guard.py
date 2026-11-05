"""Validate scope of the disposable GitHub-hosted demonstration runner."""
import os
import sys

EXPECTED_REF = "refs/heads/experiment/tfst-managed-ci-20261105"


def validate(env: dict[str, str]) -> bool:
    return (
        env.get("GITHUB_ACTIONS") == "true"
        and env.get("RUNNER_ENVIRONMENT") == "github-hosted"
        and env.get("RUNNER_OS") == "Linux"
        and env.get("GITHUB_REF") == EXPECTED_REF
        and env.get("TFST_SYNTHETIC_ONLY") == "true"
    )


if __name__ == "__main__":
    if not validate(os.environ):
        print("RUNNER_STOP unverified_host_or_branch", file=sys.stderr)
        sys.exit(1)
    print("RUNNER_PASS hosted_linux_synthetic_only")
