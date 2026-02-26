import logging
import os
from helix.public import TestResult, TestResultAttachment
from typing import Iterable, List
from formats import all_formats
from helpers import get_env


log = logging.getLogger(__name__)

def __no_results_result():
    exitCode = get_env("_commandExitCode")
    work_item_name = get_env("HELIX_WORKITEM_FRIENDLYNAME")
    
    if exitCode != "0":
        # Work item crashed / exited non-zero without producing result files.
        # Try to capture useful error information from available sources.
        failure_message = "Work item exited with code {}. No test result files were produced.".format(exitCode)
        attachments = []

        # Try to capture console output from the test process
        # The stdout/stderr of the test command is available in the work item's console log
        # but we can also check for any error output files
        upload_root = get_env("HELIX_WORKITEM_UPLOAD_ROOT")
        workitem_root = get_env("HELIX_WORKITEM_ROOT")

        # Look for blame sequence files that indicate which test was running when crash occurred
        error_details = []
        for search_dir in [os.getcwd(), upload_root, workitem_root]:
            if not search_dir or not os.path.isdir(search_dir):
                continue
            for root, dirs, files in os.walk(search_dir):
                for f in files:
                    fpath = os.path.join(root, f)
                    try:
                        # Blame sequence file tells us which test was running
                        if f == "Sequence.xml" or f.endswith("_Sequence.xml"):
                            with open(fpath, 'r', errors='replace') as seq_file:
                                content = seq_file.read()
                                error_details.append("Blame sequence (last test running at crash):\n" + content[-2000:])
                                attachments.append(TestResultAttachment(
                                    name=u"Sequence.xml",
                                    text=content,
                                ))
                        # Capture any .log files that might have error details
                        elif f.endswith("TestLog.log") or f == "dotnetTestLog.log":
                            with open(fpath, 'r', errors='replace') as log_file:
                                content = log_file.read()
                                # Get last 3000 chars which likely contain the error
                                tail = content[-3000:] if len(content) > 3000 else content
                                error_details.append("Test log tail:\n" + tail)
                                attachments.append(TestResultAttachment(
                                    name=f,
                                    text=content,
                                ))
                    except Exception as e:
                        log.warning("Failed to read {}: {}".format(fpath, e))

        if error_details:
            failure_message += "\n\n" + "\n\n".join(error_details)

        return TestResult(
            name=u'{}.WorkItemExecution'.format(work_item_name),
            kind=u'unknown',
            type_name=u'{}'.format(work_item_name),
            method=u'WorkItemExecution',
            duration=1,
            result=u'Fail',
            exception_type=None,
            failure_message=failure_message,
            stack_trace=None,
            skip_reason=None,
            attachments=attachments,
        )
    else:
        result = 'Pass'
        failure_message = None

    return TestResult(
        name=u'{}.WorkItemExecution'.format(work_item_name),
        kind=u'unknown',
        type_name=u'{}'.format(work_item_name),
        method=u'WorkItemExecution',
        duration=1,
        result=u'{}'.format(result),
        exception_type=None,
        failure_message=u'{}'.format(failure_message),
        stack_trace=None,
        skip_reason=None,
        attachments=[],
    )


def read_results(dirs_to_check: List[str]) -> Iterable[TestResult]:

    found = False

    for dir in dirs_to_check:
        log.info("Searching '{}' for test results files".format(dir))
        for root, dirs, files in os.walk(dir):
            for file_name in files:
                for f in all_formats:
                    if file_name.endswith(tuple(f.acceptable_file_suffixes)):
                        file_path = os.path.join(root, file_name)
                        log.info('Found results file {} with format {}'.format(file_path, f.name))
                        found = True
                        file_results = f.read_results(file_path)
                        for result in file_results:
                            yield result

    if not found:
        log.warn('No results file found in any of the following formats: {}'.format(', '.join((f.name for f in all_formats))))
        result = __no_results_result()
        if result is not None:
            yield result
