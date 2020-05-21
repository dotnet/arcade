#!/usr/bin/env python

import re
import os
import urllib
import helix.azure_storage
import helix.event
import helix.settings
import helix.logs

log = helix.logs.get_logger()

acceptableXUnitFileNames = [
    "testResults.xml",
    "test-results.xml",
    "test_results.xml",
    "TestResults.xUnit.xml"
]

class HelixHelper:
    def __init__(self, settings):
        self.settings = settings
        self.event_client = helix.event.create_from_uri(settings.event_uri)
        self.upload_client = helix.azure_storage.get_upload_client(settings)

    def error(self, error_type, message, log_uri=None):
        self.event_client.error(self.settings, error_type, message, log_uri)

    def xunit_event(self, results_uri, test_count, file_name):
        self.event_client.send(
            {
                'Type': 'XUnitTestResult',
                'WorkItemId': self.settings.workitem_id,
                'WorkItemFriendlyName': self.settings.workitem_friendly_name,
                'CorrelationId': self.settings.correlation_id,
                'ResultsXmlUri': results_uri,
                'TestCount': test_count,
            }
        )

    def file_event(self, results_uri, file_name):
        self.event_client.send(
            {
                'Type': 'File',
                'Uri': results_uri,
                'FileName': file_name,
                'WorkItemId': self.settings.workitem_id,
                'WorkItemFriendlyName': self.settings.workitem_friendly_name,
                'CorrelationId': self.settings.correlation_id,
            }
        )

    def upload_file_to_storage(self, file_path):
        """ Copy file specified to azure storage account using Helix infrastructure
        :param file_path: Path to file to be copied to Azure storage
        :type file_path:string
        """
        try:
            return self.upload_client.upload(file_path, os.path.basename(file_path))
        except ValueError:
            self.error("FailedUpload", "Failed to upload "+file_path+"after retry")

def findXUnitResults(search_dir):
    for root, dirs, files in os.walk(search_dir):
        for file_name in files:
            if file_name in acceptableXUnitFileNames:
                return os.path.join(root, file_name)
    return None

def main():
    settings = helix.settings.settings_from_env()

    if settings.output_uri is None or settings.event_uri is None:
        log.error("Unable to report xunit results: output_uri and/or event_uri are not set.")
        return 1

    helper = HelixHelper(settings)
    working_dir = settings.workitem_working_dir
    upload_dir = settings.workitem_upload_dir
    need_to_upload_results = True

    results_path = findXUnitResults(working_dir)

    if results_path is None:
        log.info("Didn't find test results in working directory, trying upload folder")
        results_path = findXUnitResults(upload_dir)
        need_to_upload_results = False

    if results_path is None:
        log.error("Unable to report xunit results: no test results xml file found.")
        return 2

    log.info("Uploading results from {}".format(results_path))

    with open(results_path, encoding="utf-8") as result_file:
        test_count = 0
        total_regex = re.compile(r'total="(\d+)"')
        for line in result_file:
            if '<assembly ' in line:
                match = total_regex.search(line)
                if match is not None:
                    test_count = int(match.groups()[0])
                break

    log.info("Sending XUnit test result events")
    if need_to_upload_results:
        result_url = helper.upload_file_to_storage(results_path)
        helper.file_event(result_url, os.path.basename(results_path))
    else:
        # Will be uploaded, but we still need to send events.
        # This prevents duplicate uploads and errors from them in logs
        result_url = '{container}/{file}{sas}'.format(
            container=settings.output_uri,
            file=os.path.basename(results_path),
            sas=settings.output_read_token)
    
    helper.xunit_event(result_url, test_count, os.path.basename(results_path))

    return 0


if __name__ == '__main__':
    import sys
    sys.exit(main())
