# VSTS Windows Azure VM agent connection instructions

1. Go to https://resources.azure.com/subscriptions/84a65c9a-787d-45da-b10a-3a1cefce8060/resourceGroups/dotnet-eng-build-agents-2/providers/Microsoft.Network/loadBalancers/LB-helixage/inboundNatRules

2. Machines are named as “helixage_[virtual machine #]” so look for the value.properties.backendIPConfiguration.id which references the virtual machine # you care about.

    Example:
    ```JSON
    {
      "backendIPConfiguration": {
        "id": "/subscriptions/84a65c9a-787d-45da-b10a-3a1cefce8060/resourceGroups/dotnet-eng-build-agents-2/providers/Microsoft.Compute/virtualMachineScaleSets/helixage/virtualMachines/0/networkInterfaces/NIC-helixage/ipConfigurations/NIC-helixage"
      }
    }
    ```

    This entry represents “helixage_0”

3. In that item, find the "port #" from the “value.properties.frontendPort” value

4. Connect to the machine: Mstsc /v: dotnet-eng-build-2.westus2.cloudapp.azure.com:[port #]

    a. Username: dotnet-bot

    b. Password is available from HelixProdKV as HelixVMAdminPassword