from abc import ABCMeta, abstractmethod, abstractproperty
from defs import TestResult
from typing import Iterable


class ResultFormat:
    __metaclass__ = ABCMeta

    @abstractproperty
    def name(self):
        pass

    @abstractproperty
    def acceptable_file_names(self):
        # type: () -> Iterable[str]
        pass

    @abstractmethod
    def read_results(self, path):
        # type: (str) -> Iterable[TestResult]
        pass
