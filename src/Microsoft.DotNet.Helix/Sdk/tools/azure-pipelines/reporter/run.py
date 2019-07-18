import os
import sys
import traceback
import helix.logs
import helix.settings
from queue import Queue
from threading import Thread

from test_results_reader import read_results
from helpers import batch
from azure_devops_result_publisher import AzureDevOpsTestResultPublisher

log = helix.logs.get_logger()

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
        self.__print("starting...")
        while True:            
            try:
                item = self.queue.get()
                self.__process(item)
            except:
                self.__print("got error: {}".format(traceback.format_exc()))
            finally:
                self.queue.task_done()


def process_args():
    if len(sys.argv) < 4 or len(sys.argv) > 5:
        sys.exit("Expected 3 or 4 arguments")

    collection_uri = sys.argv[1]
    team_project = sys.argv[2]
    test_run_id = sys.argv[3]
    if len(sys.argv) == 5:
        access_token = sys.argv[4]
    else:
        access_token = None

    return collection_uri, team_project, test_run_id, access_token


def main():
    collection_uri, team_project, test_run_id, access_token = process_args()

    worker_count = 10
    q = Queue()

    print("Main thread starting workers")
    log.info("Main thread starting workers")

    for i in range(worker_count):
        worker = UploadWorker(q, i, collection_uri, team_project, test_run_id, access_token)
        worker.daemon = True
        worker.start()

    print("Beginning reading of test results.")
    log.info("Beginning reading of test results.")

    all_results = read_results(os.getcwd())
    batch_size = 1000
    batches = batch(all_results, batch_size)

    print("Uploading results in batches of size {}".format(batch_size))
    log.info("Uploading results in batches of size {}".format(batch_size))

    for b in batches:
        q.put(b)

    print("Main thread finished queueing batches")
    log.info("Main thread finished queueing batches")

    q.join()

    print("Main thread exiting")
    log.info("Main thread exiting")


if __name__ == '__main__':
    main()


