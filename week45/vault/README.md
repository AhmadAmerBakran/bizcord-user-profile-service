# Week 45: Vault setup

This setup uses Vault KV v2 to store the User Profile API's JWT signing key and RabbitMQ connection string. The application fetches them before it registers authentication or the broker client. Secret values are never logged or committed to Git.

This is a local classroom setup only. Vault uses HTTP and the development RabbitMQ account. Do not use this configuration for production.

## Start Vault

In PowerShell, from the repository root:

```powershell
cd week45/vault
docker compose config --quiet
docker compose up -d vault
docker compose logs vault
```

Open http://localhost:8200. Initialize it once using **3 key shares and a threshold of 2**. Save all three unseal keys and the root token securely, outside the Git repository. Unseal using two different keys and log in with the root token.

The named `vault-data` volume persists the secrets when containers restart. Do not run `docker compose down -v` unless you deliberately want to destroy that data. After a restart, unseal Vault using two original keys.

## Create the secrets and the scoped token

Open a Vault container shell and log in:

```powershell
docker compose exec vault sh
vault login
vault secrets enable -path=secret kv-v2
```

The `vault secrets enable` command is run only once. If `secret/` already exists, verify it uses KV v2 instead of enabling it again.

In the Vault web UI, go to `secret` and create a secret at `user-profile` with these two entries:

| Key | Value |
| --- | --- |
| `AuthenticationProviderKey` | The JWT signing key configured in the lecturer's local Ocelot gateway |
| `RabbitMqConnectionString` | `host=rabbitmq;username=bizcord;password=bizcord` for this local Compose example |

The signing key must be at least 32 UTF-8 bytes and must match the lecturer's `AuthenticationProviderKey`. Enter the values in the Vault UI; do not copy them into Git-tracked files, PowerShell history, source code or logs.

Back in the container shell, configure a read-only policy and generate a one-hour application token:

```sh
printf 'path "secret/data/user-profile" { capabilities = ["read"] }\n' | vault policy write user-profile-read -
vault token create -policy=user-profile-read -no-default-policy -ttl=1h
exit
```

Copy the newly created application token to a secure place. It cannot read other paths or modify secrets. Keep the root token exclusively for administration.

## Start the microservice with Vault

In PowerShell, still inside `week45/vault`:

```powershell
$secureToken = Read-Host "Vault application token" -AsSecureString
$env:VAULT_TOKEN = [System.Net.NetworkCredential]::new("", $secureToken).Password
docker compose --profile app up --build -d
Remove-Item Env:VAULT_TOKEN
docker compose --profile app logs user-profile
```

The API is available on http://localhost:5194. The Vault UI is bound only to http://localhost:8200. The container reaches Vault through `http://vault:8200` (not `localhost`, which refers to the container itself).

For a direct `dotnet run` outside Docker, set `Vault__Enabled=true`, `Vault__Address=http://localhost:8200`, and `VAULT_TOKEN` in that process's environment. The RabbitMQ connection value in Vault must then use a broker hostname reachable from the host machine, not Docker's `rabbitmq` hostname.

## Verify the required failure scenarios

1. With Vault initialized, unsealed and the scoped token set, the service should start without displaying the signing key or broker password.
2. Restart the API with an invalid `VAULT_TOKEN`. Vault should return 403 and the service should fail to start rather than silently using another secret.
3. Try reading another Vault path using the scoped token, or writing to `secret/user-profile` with it. Both must be rejected with 403.
4. Run `docker compose restart vault`. Vault will be sealed; attempts to retrieve the secret should fail. Unseal with two original keys and check the saved secret is still there.
5. Check the protected HTTP endpoint using the gateway test script from `week45/api-gateway`.

Tokens created with a one-hour TTL need renewal or replacement before the next startup. Secret values are read **once**, at startup; after rotating a secret, restart the application to load the new value.

Production deployments need TLS, automated identity-based Vault login, secret renewal/rotation, an appropriately restricted network and a real identity provider.
