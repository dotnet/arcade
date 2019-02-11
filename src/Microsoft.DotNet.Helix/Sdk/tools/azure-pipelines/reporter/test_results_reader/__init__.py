import os
from defs import TestResult, TestResultAttachment
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


def construct_log_uri(name):
    uri = get_env("HELIX_RESULTS_CONTAINER_URI")
    read_sas = get_env("HELIX_RESULTS_CONTAINER_RSAS")

    if read_sas[0] == '?':  # SAS tokens should not start with ? but are generally passed in this way.
        read_sas = read_sas[1:]

    return uri + '/' + name + '?' + read_sas


def get_log_files(dir):
    print "Searching '{}' for log files".format(dir)
    for name in os.listdir(dir):
        path = os.path.join(dir, name)
        root, ext = os.path.splitext(path)
        if ext == ".log":
            print "Found log '{}'".format(path)
            uri = construct_log_uri(name)
            print "Uri '{}'".format(uri)
            yield name, uri


def construct_log_list(log_files):
    def line(name, url):
        return u"<li><a href='{}' target='_blank'>{}</a></li>".format(url, name)

    lines = [line(name, url) for name, url in log_files]

    output = u"<ul>" + u"".join(lines) + u"</ul>"
    print "Generated log list: {}".format(output)
    return output


def add_logs(tr, log_list):
    if tr.result != "Pass":
        tr.attachments.append(TestResultAttachment(
            name=u"Logs.html",
            text=log_list,
        ))
    return tr

def read_results(dir):
    # type: (str) -> Iterable[TestResult]

    print "Searching '{}' for test results files".format(dir)

    log_files = list(get_log_files(os.path.join(get_env("HELIX_WORKITEM_ROOT"), "..")))

    log_list = construct_log_list(log_files)

    for root, dirs, files in os.walk(dir):
        for file_name in files:
            for f in all_formats:
                if file_name in f.acceptable_file_names:
                    file_path = os.path.join(root, file_name)
                    print 'Found results file {} with format {}'.format(file_path, f.name)
                    return (add_logs(tr, log_list) for tr in f.read_results(file_path))

    print 'No results file found in any of the following formats: {}'.format(', '.join((f.name for f in all_formats)))
    return __no_results_result()
