Design Spec for API to generate and maintain SnapShots and VM for Repro Tool:
Each API is self-contained and will be maintained as micro-services available to be called by Jenkins Plugin, Repro Tool UI or any other service that wishes to take a snapshot / create a VM. Below is the structure of how the API definition looks like. 

Snapshot the current state of Jenkins VM:
```
<snapshot_id> createSnapShot(<VMInfo>)
{
}
```
Where 
VMInfo:
  -	Group Name
  -	Jenkins VM Name
  -	Subscription
  -	Target 
      o	Account
      o	Container
      o	Name
      o	Subscription
  -	Payload Id / Link
  -	Metadata
      o	Data from Jenkins for eg., failed Test, Command last used , failure logs etc.
      
Snapshot_id:
ID of the snapshot created by createSnapShot, the link to the snapshot is stored in DB. The snapshot can be a VHD link or resource id incase of managed disks or link to MAC machine for repro.

```
<SnapShotInfo> getSnapShot(<snapshot_id>){
{
}
```
Where
SnapShotInfo:
  -	Status : Pending/Completed
  - Owner
  -	Metadata
  -	TTL (How long the snapshot will be alive)
  -	Payload Id / Link
  -	VHD Link/Resource Id/link to MAC Machine 
Snapshot_id:
ID for the snapshot that needs to retrieved
```
Bool deleteSnapShot(<snapshot_id>)
{
}
```
Snapshot_id:
ID for the snapshot that needs to deleted.
```
<SnapShotInfo> renewSnapShot(<snapshot_id>)
{
}
```
Where
SnapShotInfo contains the renewed Time To Live / Old Time to Live incase renewal resulted in failure or no more permissible renewals for the user

```
List<SnapShotInfo> getSnapShotsForUser(UserName){
}
```
Where
List<SnapShotInfo> : List of shapshots alive for a user.
UserName : user name of the dev whose snapshots are returned.

```
List<SnapShotInfo> getAllSnapShots (){
}
```
List<SnapShotInfo> : List of all shapshots with statuses, for admin purposes only.
Create and maintain VM from a snapshot:

```
<LinkToCheckVMProgress> createVM(<snapshot_id>, <username>, <password>)
{
}
```
Where 
Snapshot_id:
Snapshot_id for which the VM is to be created
Username, password : of the dev requesting for creating a VM

```
<VMInfo> get VM(<VM_id>){
{
}
```
Where
VMInfo:
  -	Status : Pending/Completed
  - Owner
  -	Link to connect to VM 
  -	TTL (How long the VM will be alive)
VM_id:
ID for the VM that needs to retrieved
```
Bool deleteVM(<VM_id>)
{
}
```
VM_id:
ID for the VM that needs to deleted.
```
<VMInfo> renewVM(<VM_id>)
{
}
```
Where
VMInfo contains the renewed Time To Live / Old Time to Live incase renewal resulted in failure or no more permissible renewals for the user

```
List<VMInfo> getVMsForUser(UserName){
}
```
Where
List< VMInfo > : List of VMs alive for a user.
UserName : user name of the dev whose VMs are returned.

```
List<VMInfo> getAllVMs (){
}
```
List<VMInfo> : List of all VMs with statuses, for admin purposes only.
