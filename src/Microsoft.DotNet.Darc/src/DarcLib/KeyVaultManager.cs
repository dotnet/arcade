using System;
using System.Threading.Tasks;
using Microsoft.Azure.KeyVault;
using Microsoft.Azure.KeyVault.Models;
using Microsoft.Azure.Services.AppAuthentication;

/*
 Prototype class. We'll need to: 
    *  Replace KeyVaultManager with the logic we currently use in Helix to get stuff off of KV or make this configurable
*/
namespace Microsoft.DotNet.Darc
{
    static class KeyVaultManager
    {
        public static async Task<string> GetSecretAsync(string secretName)
        {
            try
            {
                AzureServiceTokenProvider azureServiceTokenProvider = new AzureServiceTokenProvider();
                KeyVaultClient kv = new KeyVaultClient(new KeyVaultClient.AuthenticationCallback(azureServiceTokenProvider.KeyVaultTokenCallback));
                SecretBundle bundle = await kv.GetSecretAsync("https://helixstagingkv.vault.azure.net", secretName);
                return bundle.Value;
            }
            catch (Exception exc)
            {
                Console.WriteLine($"There was an error while fetching secret for secretName '{secretName}'. Exception: {exc.Message}.");
                throw;
            }
        }
    }
}
