# Test Reporting Queries

The following is a list of default queries to use to look up information about failed tests. Feel free to change them for your own usages. 

Caveats (Jan 7, 2022): 
- Test data is only recently populated, thus, we only have about 2.5 weeks worth of data. 
- There is a [known issue](https://github.com/dotnet/core-eng/issues/14708) with how we're capturing this data that is currently being worked on, thus, some of the data we have may not be complete. 

## Tests That Have Failed X% of the Time in the Recent Timespan

This query will return a list of tests that have failed a certain percentage in the recent provided timespan. The default example in this query provides a list of tests in the dotnet/runtime repo that have failed 10% of the time in the last 7 days. 

Variables: 
- `ts`: [Kusto timespan format](https://docs.microsoft.com/en-us/azure/data-explorer/kusto/query/scalar-data-types/timespan). Default is `7d`.
- `repo`: Repository to filter on. Set to empty string to inclue all repositories. Default is `dotnet/runtime`.
- `failureThreshold`: Double value denoting failure rate percentage. Default is `0.1` or 10%. 
- `excludeAlwaysFailing`: Set to true to filter out tests that are always failing to get a list of tests that are "flakey". Default is `true`.

:part_alternation_mark: [Link](https://dataexplorer.azure.com/clusters/engsrvprod/databases/engineeringdata?query=H4sIAAAAAAAAA51SzW7bMAy+5ymInGzUaNrTgHkukC4YdtkypHkBpqZjbbJkSFRTF3v4UTLitGkOxXwwbIr8/kRNDOyhgk91CRefxQK2qiPfo4En1IFmWmYc9Vam5rVlQ7xwwbA0zctpZt2zsgb1Z/CRwgJ1PQ/g2SmzB9XAYAMc0EQoH7RoaJztALVO2F6xdYp8ImtQ6eBo20pra3UtxDfXt+VJ4MqGnaZRHtRkLEcSbgm0PZAX/viKMOCQKcpxxMGZBE/PjzrUtNQHHPw3aYrDFbALVCb4e2s1obmGh9FKPEmWxsER/WiDW2RAR2Il4iVWAUxMPuwYo9IKli/iaEVP695vZdw/hK5DN8z+wqElmd5ICI5XUe1dBbi3Gfsc0NSSXZON8Uv+8yJ1priGWDn9nZ/EmVzwfWJSLwQmdCR5WFdJLYvOv1q5yLxIGXap+gu9T9Wr6fwq1tZmQ+yG4wD6dRPFVh0+ZyftOewGuA9K1ytqlFFxJ35iRwVE0+PX0u1Fh+Hv6NsyxYSvKj+w/9+03ngVHa+JTrS+nB2vZYJL9uEOblLgGds6LVg2BZYvplrqzfPI+25Pj9d1acOKD+J+gdsibVz0o639E3rIzhLKwZo39qS1d/Y3PfJH0/cFRGGbeIeXhMECzqWNE2kDqqlRsC2j3gQzHqTe8h/Tt/8tZwQAAA==) to query