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
        # if we have a catastrophic failure, we want to create the fake test result with attached dump files and logs (if available)
        return
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
    log.info("Searching '{}' for log files".format(dir))
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
        return u"{}:\n  {}\n".format(name, url)

    lines = [line(name, url) for name, url in log_files]

    output = u"\n".join(lines)
    log.info("Generated log list: {}".format(output))
    return output


total_added_logs = 0

def add_logs(tr, log_list):
    global total_added_logs
    if tr is not None and tr.result != "Pass" and total_added_logs < 50:
        tr.attachments.append(TestResultAttachment(
            name=u"Logs.txt",
            text=log_list,
        ))
        total_added_logs += 1
    return tr

def read_results(dirs_to_check: List[str]) -> Iterable[TestResult]:

    log_files = list(get_log_files(os.path.join(get_env("HELIX_WORKITEM_ROOT"), "..")))
    log_list = construct_log_list(log_files)

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
                        file_results = (add_logs(tr, log_list) for tr in f.read_results(file_path))
                        for result in file_results:
                            yield result

    if not found:
        log.warn('No results file found in any of the following formats: {}'.format(', '.join((f.name for f in all_formats))))
        yield add_logs(__no_results_result(), log_list)
