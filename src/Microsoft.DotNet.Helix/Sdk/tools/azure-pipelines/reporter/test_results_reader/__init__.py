import os
from defs import TestResult, TestResultAttachment
from typing import Iterable
from formats import all_formats
from helpers import get_env


def __no_results_result():
    exitCode = get_env("_commandExitCode")
    work_item_name = get_env("HELIX_WORKITEM_FRIENDLYNAME")
    
    if exitCode != "0":
        result = 'Fail'
        failure_message = 'The work item failed to produce any test results.'
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


def construct_log_uri(name):
    uri = get_env("HELIX_RESULTS_CONTAINER_URI")
    read_sas = get_env("HELIX_RESULTS_CONTAINER_RSAS")

    if read_sas[0] == '?':  # SAS tokens should not start with ? but are generally passed in this way.
        read_sas = read_sas[1:]

    return uri + '/' + name + '?' + read_sas


def get_log_files(dir):
    print("Searching '{}' for log files".format(dir))
    for name in os.listdir(dir):
        path = os.path.join(dir, name)
        root, ext = os.path.splitext(path)
        if ext == ".log":
            print("Found log '{}'".format(path))
            uri = construct_log_uri(name)
            print("Uri '{}'".format(uri))
            yield name, uri


def construct_log_list(log_files):
    def line(name, url):
        return u"<li><a href='{}' target='_blank'>{}</a></li>".format(url, name)

    lines = [line(name, url) for name, url in log_files]

    output = u"<ul>" + u"".join(lines) + u"</ul>"
    print("Generated log list: {}".format(output))
    return output


total_added_logs = 0

def add_logs(tr, log_list):
    global total_added_logs
    if tr.result != "Pass" and total_added_logs < 20:
        tr.attachments.append(TestResultAttachment(
            name=u"Logs.html",
            text=log_list,
        ))
    total_added_logs += 1
    return tr

def read_results(dir: str) -> Iterable[TestResult]:

    log_files = list(get_log_files(os.path.join(get_env("HELIX_WORKITEM_ROOT"), "..")))
    log_list = construct_log_list(log_files)

    print("Searching '{}' for test results files".format(dir))

    found = False

    for root, dirs, files in os.walk(dir):
        for file_name in files:
            for f in all_formats:
                if file_name.endswith(tuple(f.acceptable_file_suffixes)):
                    file_path = os.path.join(root, file_name)
                    print('Found results file {} with format {}'.format(file_path, f.name))
                    found = True
                    file_results = (add_logs(tr, log_list) for tr in f.read_results(file_path))
                    for result in file_results:
                        yield result

    if not found:
        print('No results file found in any of the following formats: {}'.format(', '.join((f.name for f in all_formats))))
        yield add_logs(__no_results_result(), log_list)
