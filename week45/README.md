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
