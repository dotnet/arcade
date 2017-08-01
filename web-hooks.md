# Maestro GitHub web hooks

Some GitHub repositories have commit web hooks pointing at Maestro. The dotnet/versions commit hook is used for build-info change notifications, and individual repos can use commit hooks to trigger CI builds.

For example, Core-Setup release/1.0.0 and release/1.1.0 pipebuilds. GitHub sends a commit callback to Maestro, then Maestro queues the VSTS build.

## Finding web hook settings

Go to **Settings** and click **Webhooks**. If you don't see **Settings**, you don't have permission.

Example url: https://github.com/dotnet/versions/settings/hooks/

## Web hook configuration

 * Payload URL: `http://maestro-prod.azurewebsites.net/CommitPushed`
 * Content Type: `application/json`
 * Secret: [Maestro-WebhookSecretToken on EngKeyVault](https://ms.portal.azure.com/#asset/Microsoft_Azure_KeyVault/Secret/https://engkeyvault.vault.azure.net/secrets/Maestro-WebhookSecretToken/e6b838f3a9ce420d8ef3f9bf97047020)
 * "Which events would you like to trigger this webhook?": `Just the push event.`
 * Active: checked