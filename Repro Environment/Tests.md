# Tests by components

## SecretService
- Error handling when key is not stored in KV/Configs/etc

## SnapshotService

### Create

- Begin create Snapshot
    - Paramater validation for creating snapshot request
- Create snapshot
    - Physical
    - Virtual Machine
    - VM Scaleset (Helix)
    - Type of disk, making sure we gather the neccesary information for each type.

### Update status of Snapshot
- Update when completed, error, deleting, pending

### Get/List
Request information from the queue, so validation here might not be worth it.

### Delete
- Validate status of the machine
- Validate parameters according to the type of snapshot (vm, vm from scaleset, or physical).

### Renew
- Validate status of the machine
- Validation of the parameter `additionalTime`.

### Check expiration
- Run on schedule
- Update status of snapshot

## UserService
### Get
- Validate parameters

### Create
- Validate users from Microsoft Org

### Update/Delete
Not neccesary as it is just queue management.

## VirtualMachineService
### Create

- Begin create
    - Paramater validation for creating vm request
- Create
    - Physical (physical-machine.sh)
    - Azure virtual machine
    - Helix virtual machine
    - Attach payload
    - download-payload.ps1
    - download-payload.sh

### Update status of Snapshot
- Update when completed, error, deleting, pending

### Connect physical
- Validate status of the machine
- Update vm information

### Get/List
Request information from the queue, so validation here might not be worth it.

### Delete
- Validate status of the machine
- Validate parameters according to the type of vm (Helix, Azure).

### Renew
- Validate status of the machine
- Validation of the parameter `additionalTime`.

### Check expiration
- Run on schedule
- Update status of vm

## Web API
### Controllers
- Parameter validation
### Physical Machine Connection Controller
- Connect
    - Capture machine
- Delete
    - Free machine
### Github auth
### JWT generator
### Impersonator

## Helix Repro
### Console app
- Validate parameters sent to Helix API
- Handle response form the API

### Python
- Disable Helix
    - Validate parameters
    - Validate renaming files
- Create payload
    - Enumerate needed folders/files
    - Validate absolute paths for repro.sh/cmd files
- Repro API
    - Validate parameters
- Enable Helix
    - Validate parameters
    - Validate renaming files

## Jenkins Plugin


