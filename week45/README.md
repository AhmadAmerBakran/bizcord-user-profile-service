# Week 45 security

This branch starts from `feat/week-44-reliability` so the reliability changes remain in place. The week 43 API gateway work was saved separately; the route file here is based on that configuration and adds the internal-only route. It should replace the previous week 43 route file when installing it into the lecturer's separate gateway clone.

## East/west security

The service uses the same classroom HS256 JWT format as the lecturer's gateway. Both sides validate the signature and expiration. Ocelot filters by role and scope, and the receiving User Profile API enforces its own policies so direct calls cannot bypass authorization.

| Route | Required role | Required scope | Extra rule |
| --- | --- | --- | --- |
| GET /api/user-profiles | admin | profile.read | |
| GET /api/user-profiles/{userId} | member or admin | profile.read | Members can only read their own user ID |
| POST /api/user-profiles | admin | profile.write | |
| PUT /api/user-profiles/{userId} | admin | profile.write | |
| DELETE /api/user-profiles/{userId} | admin | profile.write | |
| GET /internal/user-profiles/{userId} | internal-service | profile.internal.read | Intended for other microservices only |

A missing or invalid bearer token returns 401. A valid token without the required role or scope returns 403. The internal route is deliberately separate from the user-facing CRUD API.

Both the gateway and the microservice require the same `AuthenticationProviderKey`. Never commit a signing key to this repository. Use an environment variable or Vault to supply it. The key must be at least 32 UTF-8 bytes. The lecturer's `POST /faucet` utility can issue exercise tokens with claims such as `role=internal-service` and `scope=profile.internal.read`. Production systems must use an identity provider and validate issuer and audience, not the simplified classroom configuration.

Copy the gateway configuration into a separate clone of https://github.com/patrickstolc/api-gateway:

```powershell
.\week45\api-gateway\install.ps1 -GatewayPath C:\path\to\api-gateway
```

Run `python week45/api-gateway/check_config.py` to check the route configuration's structure. This is only a static check. Real HTTP tests still need the gateway, API, RabbitMQ and valid test tokens.

The Week 45 Vault setup is described in `week45/vault/README.md`.

## Vault integration

The application can read `AuthenticationProviderKey` and `RabbitMqConnectionString` from Vault KV v2 when `Vault__Enabled=true`. It expects `VAULT_TOKEN` at runtime and fails at startup if the token or secrets are missing. The checked-in `appsettings.json` includes only non-sensitive Vault settings.

Follow [the Vault setup](vault/README.md) for initialization, unsealing, a read-only one-hour token and test scenarios. The local Compose setup includes Vault, RabbitMQ and the User Profile API.

## Verification

After starting the lecturer's gateway at port 8080 and the User Profile API at port 5194, run:

```powershell
.\week45\api-gateway\test-week45.ps1
```

The script checks 401 for no token, 403 for insufficient role or scope, and 404 for a permitted read of a nonexistent profile at both the gateway and the direct service address. It gets exercise JWTs from the lecturer's `/faucet`.

The repository CI also runs Vault parser unit tests, direct API authorization integration tests and the container startup smoke test. Without `dotnet` and Docker in the current environment, their runtime results are not verified here.
