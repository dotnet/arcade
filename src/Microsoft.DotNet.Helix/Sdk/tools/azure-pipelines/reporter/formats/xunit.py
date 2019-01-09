import xml.etree.ElementTree
from result_format import ResultFormat
from defs import TestResult, TestResultAttachment


class XUnitFormat(ResultFormat):

    def __init__(self):
        super(XUnitFormat, self).__init__()
        pass

    @property
    def name(self):
        return 'xunit'

    @property
    def acceptable_file_names(self):
        yield 'testResults.xml'
        yield 'test-results.xml'
        yield 'test_results.xml'

    def read_results(self, path):
        for (_, element) in xml.etree.ElementTree.iterparse(path, events=['end']):
            if element.tag == 'test':
                name = element.get("name")
                type_name = element.get("type")
                method = element.get("method")
                duration = float(element.get("time"))
                result = element.get("result")
                exception_type = None
                failure_message = None
                stack_trace = None
                skip_reason = None
                attachments = []

                failure_element = element.find("failure")
                if failure_element is not None:
                    exception_type = failure_element.get("exception-type")
                    message_element = failure_element.find("message")
                    if message_element is not None:
                        failure_message = message_element.text
                    stack_trace_element = failure_element.find("stack-trace")
                    if stack_trace_element is not None:
                        stack_trace = stack_trace_element.text

                    output_element = element.find("output")
                    if output_element is not None:
                        attachments.append(TestResultAttachment(
                            name=u"Console_Output.log",
                            text=output_element.text,
                        ))

                reason_element = element.find("reason")
                if reason_element is not None:
                    skip_reason = reason_element.text

                res = TestResult(name, u'xunit', type_name, method, duration, result, exception_type, failure_message, stack_trace,
                                 skip_reason, attachments)
                yield res
                # remove the element's content so we don't keep it around too long.
                element.clear()

