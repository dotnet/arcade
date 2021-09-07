import getopt
import json
import os
import sys

from helix.appinsights import app_insights

opts, args = getopt.gnu_getopt(sys.argv[1:], 'd:', ['diagnostics-data='])
opt_dict = dict(opts)

diagnostics_file = None

if '--data' in opt_dict:
    diagnostics_file = opt_dict['--data']
elif '-d' in opt_dict:
    diagnostics_file = opt_dict['-d']
else:
    diagnostics_file = os.getenv('XHARNESS_DIAGNOSTICS_PATH')

if not diagnostics_file:
    print('ERROR: Expected path to the diagnostics JSON file generated by XHarness')
    exit(1)

if not os.path.isfile(diagnostics_file):
    print(f"WARNING: Diagnostics file not found at `{diagnostics_file}`")
    exit(2)

# The JSON should be an array of objects (one per each executed XHarness command)
operations = json.load(open(diagnostics_file))

for operation in operations:
    custom_dimensions = dict()

    custom_dimensions['command'] = operation['command']
    custom_dimensions['platform'] = operation['platform']

    if 'target' in operation:
        if 'targetOS' in operation:
            custom_dimensions['target'] = operation['target'] + '_' + operation['targetOS']
        else:
            custom_dimensions['target'] = operation['target']

    app_insights.send_metric('XHarnessOperation', operation['exitCode'], properties=custom_dimensions)
    app_insights.send_metric('XHarnessOperationDuration', operation['duration'], properties=custom_dimensions)
