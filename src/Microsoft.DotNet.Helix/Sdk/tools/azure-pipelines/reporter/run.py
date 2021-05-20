import os
import re
import sys
import traceback
import logging
from queue import Queue
from threading import Thread, Lock
from typing import Tuple, Optional, List

from test_results_reader import read_results
from helpers import batch, get_env
from azure_devops_result_publisher import AzureDevOpsTestResultPublisher

workerFailedLock = Lock()
workerFailed = False


class UploadWorker(Thread):
    def __init__(self, queue, idx, collection_uri, team_project, test_run_id, access_token):
        super(UploadWorker, self).__init__()
        self.queue = queue
        self.idx = idx
        self.publisher = AzureDevOpsTestResultPublisher(
            collection_uri=collection_uri,
            access_token=access_token,
            team_project=team_project,
            test_run_id=test_run_id,
        )
        self.total_uploaded = 0
  
    def __print(self, msg):
        sys.stdout.write('Worker {}: {}\n'.format(self.idx, msg))
        sys.stdout.flush()

    def __process(self, batch):
        self.publisher.upload_batch(batch)
        self.total_uploaded = self.total_uploaded + len(batch)
        self.__print('uploaded {} results'.format(self.total_uploaded))

    def run(self):
        global workerFailed, workerFailedLock
        self.__print("starting...")
        while True:
            try:
                item = self.queue.get()
                self.__process(item)
            except:
                self.__print("got error: {}".format(traceback.format_exc()))
                with workerFailedLock:
                    workerFailed = True
            finally:
                self.queue.task_done()


def process_args() -> Tuple[str, str, str, Optional[str]]:
    if len(sys.argv) < 4 or len(sys.argv) > 5:
        print("Usage:")
        print("run.py <collection URI> <team project> <test run ID>")
        print("run.py <collection URI> <team project> <test run ID> <access token>")
        sys.exit("Expected 3 or 4 arguments")

    # argv[0] is the script name
    collection_uri = sys.argv[1]
    team_project = sys.argv[2]
    test_run_id = sys.argv[3]
    if len(sys.argv) == 5:
        access_token = sys.argv[4] # type: Optional[str]
    else:
        access_token = None

    return collection_uri, team_project, test_run_id, access_token


# This reporter will be phased out soon, but until then we need to deal with ADO outages and failures from client lib
# Currently only understands XUnit TestResults.xml (should not be around long enough to need more)
# See https://github.com/dotnet/arcade/issues/7371 for details
def check_passed_to_workaround_ado_api_failure(dirs_to_check: List[str]) -> bool:
    print("Reporting has failed.  Running mitigation for https://github.com/dotnet/arcade/issues/7371")
    found_a_result = False
    acceptable_xunit_file_names = [
        "testResults.xml",
        "test-results.xml",
        "test_results.xml",
        "TestResults.xUnit.xml"
    ]

    failure_count_found = 0

    for dir_name in dirs_to_check:
        print("Searching '{}' for test results files".format(dir_name))
        for root, dirs, files in os.walk(dir_name):
            for file_name in files:
                if file_name in acceptable_xunit_file_names:
                    file_path = os.path.join(root, file_name)
                    print('Found results file {} '.format(file_path))
                    found_a_result = True
                    failure_count_found += get_failure_count(file_path)

    if found_a_result:
        if failure_count_found == 0:
            print("Reporter script has failed, but XUnit test results show no failures.")
            return True
        else:
            print("Reporter script has failed, and we were able to find XUnit test results with failures ({})"
                  .format(str(failure_count_found)))
    else:
        print("Tried to mitigate but no results files found.")
    return False


def get_failure_count(test_results_path: str):
    fail_count = 0
    with open(test_results_path, encoding="utf-8") as result_file:
        total_regex = re.compile(r'failed="(\d+)"')
        for line in result_file:
            if '<assembly ' in line:
                match = total_regex.search(line)
                if match is not None:
                    fail_count += int(match.groups()[0])
                break
    return fail_count


def main():
    global workerFailed, workerFailedLock

    try:
        logging.basicConfig(
            format='%(asctime)s: %(levelname)s: %(thread)d: %(module)s(%(lineno)d): %(funcName)s: %(message)s',
            level=logging.INFO,
            handlers=[
                logging.StreamHandler()
            ]
        )
        log = logging.getLogger(__name__)

        collection_uri, team_project, test_run_id, access_token = process_args()

        worker_count = 10
        q = Queue()

        log.info("Main thread starting {0} workers".format(worker_count))

        for i in range(worker_count):
            worker = UploadWorker(q, i, collection_uri, team_project, test_run_id, access_token)
            worker.daemon = True
            worker.start()

        log.info("Beginning to read test results...")

        # In case the user puts the results in HELIX_WORKITEM_UPLOAD_ROOT for upload, check there too.
        all_results = read_results([os.getcwd(),
                                    get_env("HELIX_WORKITEM_UPLOAD_ROOT")])

        batch_size = 1000
        batches = batch(all_results, batch_size)

        log.info("Uploading results in batches of size {}".format(batch_size))

        for b in batches:
            q.put(b)

        log.info("Main thread finished queueing batches")

        q.join()

        log.info("Main thread exiting")

        with workerFailedLock:
            if workerFailed:
                if check_passed_to_workaround_ado_api_failure([os.getcwd(), get_env("HELIX_WORKITEM_UPLOAD_ROOT")]):
                    sys.exit(0)
                else:
                    sys.exit(1337)
    except Exception as anything:
        log.warning("Unhandled exception trying to report to ADO: {}".format(str(anything)))
        log.warning("We'll attempt to count the XUnit results and if XML is present and no failures, return 0")
        if check_passed_to_workaround_ado_api_failure([os.getcwd(), get_env("HELIX_WORKITEM_UPLOAD_ROOT")]):
            sys.exit(0)
        else:
            sys.exit(1138)


if __name__ == '__main__':
    main()


