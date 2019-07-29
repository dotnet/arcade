import base64
import os
import logging
from typing import Iterable, Mapping, List, Dict
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
        results_with_attachments = {r.name: r for r in results if r.attachments}

        test_case_results = self.convert_results(results)

        self.publish_results(test_case_results, results_with_attachments)

    def publish_results(self, test_case_results: Iterable[TestCaseResult], results_with_attachments: Mapping[str, TestResult]) -> None:
        connection = self.get_connection()
        test_client = connection.get_client("azure.devops.v5_1.test.TestClient")  # type: TestClient

        published_results = test_client.add_test_results_to_test_run(list(test_case_results), self.team_project, self.test_run_id)  # type: List[TestCaseResult]

        log.info("Published Results count: {0}".format(len(published_results)))

        try:
            for r in published_results:
                log.debug(r.automated_test_name)
                if r.sub_results is not None:
                    for rs in r.sub_results:
                        log.debug(rs.display_name)

            log.info("Attachments count: {0}".format(len(results_with_attachments)))
            for (k,v) in results_with_attachments.items():
                log.debug("{0}: {1}".format(k, v.name))
        except Exception as e:
            log.debug(e)

        for published_result in published_results:
            if published_result.automated_test_name in results_with_attachments:
                log.debug("Result {0} has an attachment".format(published_result.automated_test_name))
                result = results_with_attachments.get(published_result.automated_test_name)
                for attachment in result.attachments:
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
            elif published_result.sub_results is not None:
                log.debug("Checking {0} subresults...".format(len(published_result.sub_results)))
                for published_sub_result in published_result.sub_results:
                    log.debug("Checkign subresult {0}".format(published_sub_result.display_name))
                    if published_sub_result.display_name in results_with_attachments:
                        log.debug("Subresult {0} has an attachment".format(published_sub_result.display_name))
                        result = results_with_attachments.get(published_result.automated_test_name)
                        for attachment in result.attachments:
                            try:
                                # Python 3 will throw a TypeError exception because b64encode expects bytes
                                stream=base64.b64encode(text(attachment.text))
                            except TypeError:
                                # stream has to be a string but b64encode takes and returns bytes on Python 3
                                stream=base64.b64encode(bytes(attachment.text, "utf-8")).decode("utf-8")
                            test_client.create_test_sub_result_attachment(
                                TestAttachmentRequestModel(
                                    file_name=text(attachment.name),
                                    stream=stream,
                                ), self.team_project, self.test_run_id, published_result.id)

    def convert_results(self, results: Iterable[TestResult]) -> Iterable[TestCaseResult]:
        comment = "{{ \"HelixJobId\": \"{}\", \"HelixWorkItemName\": \"{}\" }}".format(
            os.getenv("HELIX_CORRELATION_ID"),
            os.getenv("HELIX_WORKITEM_FRIENDLYNAME"),
        )

        def is_data_driven_test(r: TestResult) -> bool:
            return r.name.endswith(")")

        def get_ddt_base_name(r: TestResult) -> str:
            return r.name.split('(',1)[0]

        def convert_to_sub_test(r: TestResult) -> TestSubResult:
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

        def convert_result(r: TestResult) -> TestCaseResult:
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

        unconverted_results = list(results) # type: List[TestResult]
        log.debug("Count of unconverted_results: {0}".format(len(unconverted_results)))

        # Find all DDTs, determine parent, and add to dictionary
        data_driven_tests = {}  # type: Dict[str, TestCaseResult]
        non_data_driven_tests = [] # type: TestCaseResult

        for r in unconverted_results:
            if r is None:
                continue
            if is_data_driven_test(r):
                base_name = get_ddt_base_name(r)
                if base_name in data_driven_tests:
                    sub_test = convert_to_sub_test(r)
                    data_driven_tests[base_name].sub_results.append(sub_test)
                    if sub_test.outcome == "Failed":
                        data_driven_tests[base_name].outcome = "Failed"

                else:
                    data_driven_tests[base_name] = convert_result(r)
                    data_driven_tests[base_name].automated_test_name = base_name
                    data_driven_tests[base_name].result_group_type = "dataDriven"
                    data_driven_tests[base_name].sub_results = [convert_to_sub_test(r)]
            else:
                non_data_driven_tests.append(convert_result(r))

        return list(data_driven_tests.values()) + non_data_driven_tests

    def get_connection(self) -> Connection:
        credentials = self.get_credentials()
        return Connection(self.collection_uri, credentials)

    def get_credentials(self) -> BasicTokenAuthentication:
        if self.access_token:
            return BasicTokenAuthentication({'access_token': self.access_token})

        token = get_env("VSTS_PAT")
        return BasicAuthentication("ignored", token)
