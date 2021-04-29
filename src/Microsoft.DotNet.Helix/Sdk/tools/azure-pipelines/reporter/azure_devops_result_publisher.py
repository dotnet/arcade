import base64
import os
import logging
from typing import Iterable, Mapping, List, Dict, Optional, Tuple
from builtins import str as text
from azure.devops.connection import Connection
from msrest.authentication import BasicTokenAuthentication, BasicAuthentication
from azure.devops.v5_1.test import TestClient
from azure.devops.v5_1.test.models import TestCaseResult, TestAttachmentRequestModel, TestSubResult

from helpers import get_env
from defs import TestResult

log = logging.getLogger(__name__)

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

    def upload_batch(self, results: Iterable[TestResult]):
        results_with_attachments = {r.name: r for r in results if r is not None and r.attachments}

        (test_case_results, test_name_order) = self.convert_results(results)

        self.publish_results(test_case_results, test_name_order, results_with_attachments)

    def is_data_driven_test(self, r: str) -> bool:
        return r.endswith(")")

    def get_ddt_base_name(self, r: str) -> str:
        return r.split('(',1)[0]

    def send_attachment(self, test_client, attachment, published_result):
        try:
            # Python 3 will throw a TypeError exception because b64encode expects bytes
            stream=base64.b64encode(text(attachment.text))
        except TypeError:
            # stream has to be a string but b64encode takes and returns bytes on Python 3
            stream=base64.b64encode(bytes(attachment.text, "utf-8")).decode("utf-8")

        test_client.create_test_result_attachment(
            TestAttachmentRequestModel(
                file_name=text(attachment.name),
                stream=stream,
            ), self.team_project, self.test_run_id, published_result.id)

    def send_sub_attachment(self, test_client, attachment, published_result, sub_result_id):
        stream=base64.b64encode(bytes(attachment.text, "utf-8")).decode("utf-8")

        test_client.create_test_sub_result_attachment(
            TestAttachmentRequestModel(
                file_name=text(attachment.name),
                stream=stream,
            ), self.team_project, self.test_run_id, published_result.id, sub_result_id)

    def publish_results(self, test_case_results: Iterable[TestCaseResult], test_result_order: Dict[str, List[str]], results_with_attachments: Mapping[str, TestResult]) -> None:
        connection = self.get_connection()
        test_client = connection.get_client("azure.devops.v5_1.test.TestClient")  # type: TestClient

        published_results = test_client.add_test_results_to_test_run(list(test_case_results), self.team_project, self.test_run_id)  # type: List[TestCaseResult]

        for published_result in published_results:

            # Don't send attachments if the result was not accepted.
            if published_result.id == -1:
                continue

            # Does the test result have an attachment with an exact matching name?
            if published_result.automated_test_name in results_with_attachments:
                log.debug("Result {0} has an attachment".format(published_result.automated_test_name))
                result = results_with_attachments.get(published_result.automated_test_name)

                for attachment in result.attachments:
                    self.send_attachment(test_client, attachment, published_result)

            # Does the test result have an attachment with a sub-result matching name?
            # The data structure returned from AzDO does not contain a subresult's name, only an 
            # index. The order of results is meant to be the same as was posted. This assumes that 
            # is true , and uses the order of test names recorded earlier to look-up the attachments.
            elif published_result.sub_results is not None:
                sub_results_order = test_result_order[published_result.automated_test_name]
                
                # Sanity check
                if len(sub_results_order) != len(published_result.sub_results):
                    log.warning("Returned subresults list length does not match expected. Attachments may not pair correctly.")
                
                for (name, sub_result) in zip(sub_results_order, published_result.sub_results):
                    if name in results_with_attachments:
                        result = results_with_attachments.get(name)
                        for attachment in result.attachments:
                            self.send_sub_attachment(test_client, attachment, published_result, sub_result.id)

    def convert_results(self, results: Iterable[TestResult]) -> Tuple[Iterable[TestCaseResult], Dict[str, List[str]]]:
        comment = "{{ \"HelixJobId\": \"{}\", \"HelixWorkItemName\": \"{}\" }}".format(
            os.getenv("HELIX_CORRELATION_ID"),
            os.getenv("HELIX_WORKITEM_FRIENDLYNAME"),
        )

        def convert_to_sub_test(r: TestResult) -> Optional[TestSubResult]:
            if r.result == "Pass":
                return TestSubResult(
                    comment=comment,
                    display_name=text(r.name),
                    duration_in_ms=r.duration_seconds*1000,
                    outcome="Passed"
                    )
            if r.result == "Fail":
                return TestSubResult(
                    comment=comment,
                    display_name=text(r.name),
                    duration_in_ms=r.duration_seconds*1000,
                    outcome="Failed",
                    stack_trace=text(r.stack_trace) if r.stack_trace is not None else None,
                    error_message=text(r.failure_message)
                    )
            if r.result == "Skip":
                return TestSubResult(
                    comment=comment,
                    display_name=text(r.name),
                    duration_in_ms=r.duration_seconds*1000,
                    outcome="NotExecuted"
                    )
            log.warning("Unexpected result value {} for {}".format(r.result, r.name))
            return None

        def convert_result(r: TestResult) -> Optional[TestCaseResult]:
            if r.result == "Pass":
                return TestCaseResult(
                    test_case_title=text(r.name),
                    automated_test_name=text(r.name),
                    automated_test_type=text(r.kind),
                    automated_test_storage=self.work_item_name,
                    priority=1,
                    duration_in_ms=r.duration_seconds*1000,
                    outcome="Passed",
                    state="Completed",
                    comment=comment,
                )
            if r.result == "Fail":
                return TestCaseResult(
                    test_case_title=text(r.name),
                    automated_test_name=text(r.name),
                    automated_test_type=text(r.kind),
                    automated_test_storage=self.work_item_name,
                    priority=1,
                    duration_in_ms=r.duration_seconds*1000,
                    outcome="Failed",
                    state="Completed",
                    error_message=text(r.failure_message),
                    stack_trace=text(r.stack_trace) if r.stack_trace is not None else None,
                    comment=comment,
                )

            if r.result == "Skip":
                return TestCaseResult(
                    test_case_title=text(r.name),
                    automated_test_name=text(r.name),
                    automated_test_type=text(r.kind),
                    automated_test_storage=self.work_item_name,
                    priority=1,
                    duration_in_ms=r.duration_seconds*1000,
                    outcome="NotExecuted",
                    state="Completed",
                    error_message=text(r.skip_reason),
                    comment=comment,
                )

            log.warning("Unexpected result value {} for {}".format(r.result, r.name))
            return None

        unconverted_results = list(results) # type: List[TestResult]
        log.debug("Count of unconverted_results: {0}".format(len(unconverted_results)))

        # Find all DDTs, determine parent, and add to dictionary
        data_driven_tests = {}  # type: Dict[str, TestCaseResult]
        non_data_driven_tests = [] # type: List[TestCaseResult]
        test_name_ordering = {} # type: Dict[str, List[str]]

        for r in unconverted_results:
            if r is None:
                continue

            if not self.is_data_driven_test(r.name):
                non_data_driven_tests.append(convert_result(r))
                test_name_ordering[r.name] = []
                continue

            # Must be a DDT
            base_name = self.get_ddt_base_name(r.name)

            if base_name in data_driven_tests:
                sub_test = convert_to_sub_test(r)
                if sub_test is None:
                    continue

                data_driven_tests[base_name].sub_results.append(sub_test)
                test_name_ordering[base_name].append(r.name)

                # Mark parent test as Failed if any subresult is Failed
                if sub_test.outcome == "Failed":
                    data_driven_tests[base_name].outcome = "Failed"

            else:
                cr = convert_result(r)
                csr = convert_to_sub_test(r)

                if cr is None or csr is None:
                    continue

                data_driven_tests[base_name] = cr
                data_driven_tests[base_name].automated_test_name = base_name
                data_driven_tests[base_name].result_group_type = "dataDriven"
                data_driven_tests[base_name].sub_results = [csr]
                test_name_ordering[base_name] = [r.name]

        return (list(data_driven_tests.values()) + non_data_driven_tests, test_name_ordering)

    def get_connection(self) -> Connection:
        credentials = self.get_credentials()
        return Connection(self.collection_uri, credentials)

    def get_credentials(self) -> BasicTokenAuthentication:
        if self.access_token:
            return BasicTokenAuthentication({'access_token': self.access_token})

        token = get_env("VSTS_PAT")
        return BasicAuthentication("ignored", token)
