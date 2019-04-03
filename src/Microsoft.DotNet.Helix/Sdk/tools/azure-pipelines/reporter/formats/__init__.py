from typing import List
from result_format import ResultFormat
from xunit import XUnitFormat
from junit import JUnitFormat


all_formats = [
    XUnitFormat(),
    JUnitFormat()
]  # type: List[ResultFormat]
