# VSTS Windows Azure VM agent connection instructions

1. Go to https://resources.azure.com/subscriptions/84a65c9a-787d-45da-b10a-3a1cefce8060/resourceGroups/dnceng-build-agents/providers/Microsoft.Network/loadBalancers/LB-helixage/inboundNatRules

2. Machines are named as “helixage_[virtual machine #]” so look at the end of the `"name"` property which references the virtual machine # you care about.

    Example:
    ```JSON
    {
      "name": "LoadBalancerBEAddressNatPool.0",
    }
    ```

    This entry represents “helixage_0”

3. In that item, find the "port #" from the `"value.properties.frontendPort"` value

4. Connect to the machine: `mstsc /v: dnceng-helix.westus2.cloudapp.azure.com:[port #]`

    a. Username: dotnet-bot

    b. Password is available from **HelixProdKV** as *HelixVMAdminPassword*
