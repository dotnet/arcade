import yaml

from .result_format import ResultFormat
from helix.public import TestResult, TestResultAttachment

class YAMLFormat(ResultFormat):

    def __init__(self):
        super(YAMLFormat, self).__init__()
        pass

    @property
    def name(self):
        return 'yaml'

    @property
    def acceptable_file_suffixes(self):
        yield 'testResults.yml'
        yield 'test-results.yml'
        yield 'test_results.yml'

    # Work in Progress :)
    def read_results(self, path):
        contents = None
        with open(path) as fh:
            contents = yaml.safe_load(fh)

        results = contents.get("assemblies").get("assembly").get("collection").get("tests")

        for test in results:
            name = test.get("name")
            type_name = test.get("type")
            method = test.get("method")
            time = float(test.get("time"))
            result = test.get("result")
            exception_type = None
            failure_message = None
            stack_trace = None
            skip_reason = None
            attachments = []

        failure_element = test.find("failure")

        if failure_element is not None:
            exception_type = failure_element.get("exception-type")
            failure_message = failure_element.find("message")
            stack_trace = failure_element.find("stack-trace")

        output_element = test.find("output")

        if output_element is not None:
            attachments.append(TestResultAttachment(
                name=u"Console_Output.log",
                text=output_element,
            ))

        skip_reason = test.find("reason")
        res = TestResult(name, u'yaml', type_name, method, time, result, exception_type,
                         failure_message, stack_trace, skip_reason, attachments)
        yield res
