import base64
from typing import Iterable, Mapping
from vsts.vss_connection import VssConnection
from msrest.authentication import BasicTokenAuthentication, BasicAuthentication
from vsts.test.v4_1.test_client import TestClient
from vsts.test.v4_1.models import TestCaseResult, TestAttachmentRequestModel

from helpers import get_env
from defs import TestResult


class AzureDevOpsTestResultPublisher:
    def __init__(self, collection_uri, access_token, team_project, test_run_id=None):
        """

        :type collection_uri: str The team project collection uri
        :type access_token: str The value of SYSTEM_ACCESSTOKEN from the azure pipelines build
        """
        self.collection_uri = collection_uri
        self.access_token = access_token
        self.team_project = team_project
        self.test_run_id = test_run_id
        self.work_item_name = get_env("HELIX_WORKITEM_FRIENDLYNAME")
        pass

    def upload_batch(self, results):
        """

        :type results: Iterable[TestResult]
        """
        results_with_attachments = {r.name: r for r in results if r.attachments}

        test_case_results = self.convert_results(results)

        self.publish_results(test_case_results, results_with_attachments)

    def publish_results(self, test_case_results, results_with_attachments):
        """

        :type test_case_results: Iterable[TestCaseResult]
        :param test_case_results:
        :type results_with_attachments: Mapping[str, TestResult]
        :param results_with_attachments:
        """
        connection = self.get_connection()
        test_client = connection.get_client("vsts.test.v4_1.test_client.TestClient")  # type: TestClient

        published_results = test_client.add_test_results_to_test_run(list(test_case_results), self.team_project, self.test_run_id)  # type: List[TestCaseResult]

        for published_result in published_results:
            if published_result.automated_test_name in results_with_attachments:
                result = results_with_attachments.get(published_result.automated_test_name)
                for attachment in result.attachments:
                    test_client.create_test_result_attachment(TestAttachmentRequestModel(
                        file_name=attachment.name.encode('utf-8'),
                        stream=base64.b64encode(attachment.text.encode('utf-8')),
                    ), self.team_project, self.test_run_id, published_result.id)

    def convert_results(self, results):
        # type: (Iterable[TestResult]) -> Iterable[TestCaseResult]
        def convert_result(r):
            # type: (TestResult) -> TestCaseResult

            if r.result == "Pass":
                return TestCaseResult(
                    test_case_title=r.name.encode('utf-8'),
                    automated_test_name=r.name.encode('utf-8'),
                    automated_test_type=r.kind.encode('utf-8'),
                    automated_test_storage=self.work_item_name,
                    priority=1,
                    duration_in_ms=r.duration_seconds*1000,
                    outcome="Passed",
                    state="Completed",
                )
            if r.result == "Fail":
                return TestCaseResult(
                    test_case_title=r.name.encode('utf-8'),
                    automated_test_name=r.name.encode('utf-8'),
                    automated_test_type=r.kind.encode('utf-8'),
                    automated_test_storage=self.work_item_name,
                    priority=1,
                    duration_in_ms=r.duration_seconds*1000,
                    outcome="Failed",
                    state="Completed",
                    error_message=r.failure_message.encode('utf-8'),
                    stack_trace=r.stack_trace.encode('utf-8') if r.stack_trace is not None else None,
                )

            if r.result == "Skip":
                return TestCaseResult(
                    test_case_title=r.name.encode('utf-8'),
                    automated_test_name=r.name.encode('utf-8'),
                    automated_test_type=r.kind.encode('utf-8'),
                    automated_test_storage=self.work_item_name,
                    priority=1,
                    duration_in_ms=r.duration_seconds*1000,
                    outcome="NotExecuted",
                    state="Completed",
                    error_message=r.skip_reason.encode('utf-8'),
                )

            print "Unexpected result value {} for {}".format(r.result, r.name)

        return (convert_result(r) for r in results if r is not None)

    def get_connection(self):
        credentials = self.get_credentials()
        return VssConnection(self.collection_uri, credentials)

    def get_credentials(self):
        if self.access_token:
            return BasicTokenAuthentication({'access_token': self.access_token})

        token = get_env("VSTS_PAT")
        return BasicAuthentication("ignored", token)
