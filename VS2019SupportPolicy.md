# Customer Request
Provide a reasonable mechanism to scout new minor versions of VS 2019 (both preview and release) that will minimize impact to production queues

## Policy for supporting VS 2019 – Released version
DNCEng will create a temporary queue (buildpool.server.amd64.vs2019.scouting.open) that customers can use to valid the latest release of the product. 

This queue will be activated one week after the release of update and will be available for two weeks. After this period, we will update the existing queues (any helix queue containing *.vs2019) will be updated as part of a regularly scheduled Helix Machines release.

Patch updates will be made directly to the queues as required by corporate security or when requested by customers
## Policy for supporting VS 2019 – Public Preview Versions
DNCEng will implement the same rotation as we have for the released version of VS - a temporary queue (buildpool.server.vs2019.pre.scouting.open) that  will be activated within one week of release and will be available for two weeks. 

After this period, we will update the existing queues (any helix queue containing *.pre) will be updated as part of a regularly scheduled Helix Machines release
## Policy for supporting VS 2019 – Private Preview Versions
There is currently no Engineering Services support for providing VS private preview queues.
 
## **NOTE** As there is currently a policy to reduce core usage to support external responses to Covid-19, the core allocation for the above queues is being limited to 20 Cores (5 Systems) for the preview queue and 20 cores (5 Systems) for the release queue