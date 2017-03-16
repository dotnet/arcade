# Aquiring machines
There are two main methods to aquire machines for a reproduction environment: 1) Dev Test Lab (DTL) for VMs and 2) Asset Explorer (AE) for physical hardware like Macs, X64, etc.

### DTL
- VM image management happens here (would love to find a way to share with CI and VSTS)
- Special artifacts selectable

### Asset Explorer (AE)
AE is a tool (originally from Office) which manages inventory in "pools", allowing for check out and check in.  It does not manage the machines directly, but is simply a database of sort to keep track what's available and who has what.  The good news is that it has a web API.

- Our pool is \STB\DevDiv\DotNet
- Web services url is http://aee/ws
- To install the AE client is http://aee/installae.  IE is needed to install the client.
- Contact: Dale Hirt in DDIT

### Requirements
- Common interface to "check out" a machine (VM or otherwise)
- Checking out a machine comes with sufficient data to connect and configure
- The machine should be delivered in a known state which can reasonably be setup to repro

### Implementation Notes
