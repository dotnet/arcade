import os
from defs import TestResult
from typing import Iterable
from formats import all_formats
from helpers import get_env


def __no_results_result():
    work_item_name = get_env("HELIX_WORKITEM_FRIENDLYNAME")
    yield TestResult(
        name=u'{}.WorkItemExecution'.format(work_item_name),
        kind=u'unknown',
        type_name=u'{}'.format(work_item_name),
        method=u'WorkItemExecution',
        duration=1,
        result=u'Fail',
        exception_type=None,
        failure_message=u'The work item failed to produce any test results.',
        stack_trace=None,
        skip_reason=None,
        attachments=None,
    )


def read_results(dir):
    # type: (str) -> Iterable[TestResult]

    print "Searching '{}' for test results files".format(dir)

    for root, dirs, files in os.walk(dir):
        for file_name in files:
            for f in all_formats:
                if file_name in f.acceptable_file_names:
                    file_path = os.path.join(root, file_name)
                    print 'Found results file {} with format {}'.format(file_path, f.name)
                    return f.read_results(file_path)

    print 'No results file found in any of the following formats: {}'.format(', '.join((f.name for f in all_formats)))
    return __no_results_result()
