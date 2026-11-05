from pathlib import Path
import unittest

WF = Path(__file__).resolve().parents[1] / '.github/workflows/tfst-managed-ci-synthetic.yml'


class WorkflowContractTests(unittest.TestCase):
    def setUp(self):
        self.txt = WF.read_text(encoding='utf-8')

    def test_read_only_token(self):
        self.assertIn('permissions:\n  contents: read\n', self.txt)
        self.assertNotIn('contents: write', self.txt)

    def test_hosted_linux_only(self):
        self.assertEqual(self.txt.count('runs-on: ubuntu-24.04'), 2)
        self.assertNotIn('self-hosted', self.txt)

    def test_no_deployment_resources(self):
        for forbidden in ['secrets.', 'ssh ', 'scp ', 'rsync ', 'deployment:', 'environment:', 'curl ', 'wget ', 'sudo ', 'pip install', 'npm install']:
            self.assertNotIn(forbidden, self.txt)

    def test_pinned_checkout_and_nonpersistent_token(self):
        self.assertEqual(self.txt.count('uses: actions/checkout@11d5960a326750d5838078e36cf38b85af677262'), 2)
        self.assertEqual(self.txt.count('persist-credentials: false'), 2)

    def test_no_automatic_production_branch(self):
        self.assertIn('- experiment/tfst-managed-ci-20261105', self.txt)
        self.assertNotIn('pull_request_target:', self.txt)
        self.assertNotIn('schedule:', self.txt)

    def test_two_jobs_sequential(self):
        self.assertIn('build_and_test:', self.txt)
        self.assertIn('independent_verify:', self.txt)
        self.assertIn('needs: build_and_test', self.txt)

    def test_same_commit_both_jobs(self):
        self.assertEqual(self.txt.count('ref: ${{ github.sha }}'), 2)


if __name__ == '__main__': unittest.main()
