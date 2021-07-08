from typing import List


class TestResult:
    def __init__(self, name, kind, type_name, method, duration, result, exception_type, failure_message, stack_trace,
                 skip_reason, attachments):
        """

        :type name: unicode
        :type kind: unicode
        :type type_name: unicode
        :type method: unicode
        :type duration: float
        :type result: unicode
        :type exception_type: unicode
        :type failure_message: unicode
        :type stack_trace: unicode
        :type skip_reason: unicode
        :type attachments: List[TestResultAttachment]
        """
        self._name = name
        self._kind = kind
        self._type = type_name
        self._method = method
        self._duration_seconds = duration
        self._result = result
        self._exception_type = exception_type
        self._failure_message = failure_message
        self._stack_trace = stack_trace
        self._skip_reason = skip_reason
        self._attachments = attachments

    @property
    def name(self):
        return self._name

    @property
    def kind(self):
        return self._kind

    @property
    def type(self):
        return self._type

    @property
    def method(self):
        return self._method

    @property
    def duration_seconds(self):
        return self._duration_seconds

    @property
    def result(self):
        return self._result

    @property
    def exception_type(self):
        return self._exception_type

    @property
    def failure_message(self):
        return self._failure_message

    @property
    def stack_trace(self):
        return self._stack_trace

    @property
    def skip_reason(self):
        return self._skip_reason

    @property
    def output(self):
        return self._output

    @property
    def attachments(self):
        return self._attachments


class TestResultAttachment:
    def __init__(self, name, text):
        """

        :type name: unicode
        :type text: unicode
        """
        self._name = name
        self._text = text

    @property
    def name(self):
        return self._name

    @property
    def text(self):
        return self._text
