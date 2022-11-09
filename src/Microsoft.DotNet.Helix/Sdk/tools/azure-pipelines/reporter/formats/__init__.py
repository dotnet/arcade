from typing import List
from .result_format import ResultFormat
from .xunit import XUnitFormat
from .junit import JUnitFormat
from .trx import TRXFormat
from .yaml import YAMLFormat


all_formats = [
    XUnitFormat(),
    JUnitFormat(),
    TRXFormat(),
    YAMLFormat()
]  # type: List[ResultFormat]
