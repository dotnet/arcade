import glob
import xml.etree.ElementTree
from result_format import ResultFormat
from defs import TestResult, TestResultAttachment


class TRXFormat(ResultFormat):

    def __init__(self):
        super(TRXFormat, self).__init__()
        pass

    @property
    def name(self):
        return 'trx'

    @property
    def acceptable_file_names(self):
        trx_files = glob.glob('*.trx')
        for trx_file in trx_files:
            yield trx


    def parse_duration(duration):
        # durations in trx format are hour:minute:second.millisecond
        hour, minute, second = duration.split(':')
        return float(hour)*60*60 + float(minute)*60 + float(second)

    def read_results(self, path):
        test_classes = {}

        # class names are stored separately from the results. Gather testName->className information
        # and store it away first, since it is smaller than storing the results
        for (_, element) in xml.etree.ElementTree.iterparse(path, events=['end']):
            if element.tag == "{http://microsoft.com/schemas/VisualStudio/TeamTest/2010}UnitTest":
                testMethod_element = element.find("{http://microsoft.com/schemas/VisualStudio/TeamTest/2010}TestMethod")
                if testMethod_element is not None:
                    test_classes[testMethod_element.get("name")] = testMethod_element.get("className")
                element.clear()

        for (_, element) in xml.etree.ElementTree.iterparse(path, events=['end']):
            if element.tag == "{http://microsoft.com/schemas/VisualStudio/TeamTest/2010}UnitTestResult":
                test_name = element.get("testName")

                # Find the class name from the dictionary we created earlier, and then remove that element from the dictionary
                classname = test_classes[test_name]
                del test_classes[test_name]

                name = classname + '.' + test_name
                type_name = classname
                method = test_name
                duration = 0.0
                result = "Pass"
                outcome = element.get("outcome")
                exception_type = None
                failure_message = None
                stack_trace = None
                skip_reason = None
                attachments = []

                if element.get("duration") is not None:
                    duration = parse_duration(element.get("duration"))

                if outcome == "NotExecuted":
                    result = "Skip"
                    skip_reason = u""

                elif outcome is "Failed":
                    result = "Fail"

                    elif outcome == "Failed":
                    result = "Fail"
                    output_element = element.find("{http://microsoft.com/schemas/VisualStudio/TeamTest/2010}Output")
                    if output_element is not None:
                        error_element = output_element.find("{http://microsoft.com/schemas/VisualStudio/TeamTest/2010}ErrorInfo")
                        if error_element is not None:
                            message_element = error_element.find("{http://microsoft.com/schemas/VisualStudio/TeamTest/2010}Message")
                            failure_message = message_element.text
                            stacktrace_element = error_element.find("{http://microsoft.com/schemas/VisualStudio/TeamTest/2010}StackTrace")
                            stack_trace = stacktrace_element.text

                        stdout_element = output_element.find("{http://microsoft.com/schemas/VisualStudio/TeamTest/2010}StdOut")
                        if stdout_element is not None:
                            attachments.append(TestResultAttachement(
                                name: u"Console_Output.log",
                                text=stdout_element.text,
                            ))

                        stderr_element = output_element.find("{http://microsoft.com/schemas/VisualStudio/TeamTest/2010}StdErr")
                        if stderr_element is not None:
                            attachments.append(TestResultAttachement(
                                name: u"Error_Output.log",
                                text=stderr_element.text,
                            ))
                else:
                    result = "Pass"

                res = TestResult(name, u'trx', type_name, method, duration, result, exception_type, failure_message, stack_trace,
                                 skip_reason, attachments)
                yield res

                # remove the element's content so we don't keep it around too long.
                element.clear()
