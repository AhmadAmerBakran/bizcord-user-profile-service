#!/usr/bin/env bash
set -euo pipefail

cd "$(dirname "$0")/../.."
compose=(docker compose -f week45/vault/compose.yaml)
root_token=''
app_token=''
first_key=''
second_key=''
api_pid=''

cleanup() {
    if [ -n "$api_pid" ]; then
        kill "$api_pid" >/dev/null 2>&1 || true
        wait "$api_pid" >/dev/null 2>&1 || true
    fi
    "${compose[@]}" down -v >/dev/null 2>&1 || true
}
trap cleanup EXIT

assert_status() {
    local expected="$1" method="$2" url="$3" token="$4"
    local actual
    actual="$(curl --silent --output /dev/null --write-out '%{http_code}' \
        --max-time 5 --request "$method" --header "X-Vault-Token: $token" "$url")"
    if [ "$actual" != "$expected" ]; then
        echo "Expected HTTP $expected for $method $url, got $actual" >&2
        exit 1
    fi
}

"${compose[@]}" up -d vault
for attempt in {1..45}; do
    status="$(curl --silent --output /dev/null --write-out '%{http_code}' --max-time 2 http://127.0.0.1:8200/v1/sys/health || true)"
    if [ "$status" != "000" ]; then break; fi
    sleep 2
done
if [ "$status" = "000" ]; then
    "${compose[@]}" logs vault
    echo "Vault did not start" >&2
    exit 1
fi

init_json="$("${compose[@]}" exec -T vault vault operator init -key-shares=3 -key-threshold=2 -format=json)"
root_token="$(printf '%s' "$init_json" | python3 -c 'import json,sys;print(json.load(sys.stdin)["root_token"])')"
first_key="$(printf '%s' "$init_json" | python3 -c 'import json,sys;print(json.load(sys.stdin)["unseal_keys_b64"][0])')"
second_key="$(printf '%s' "$init_json" | python3 -c 'import json,sys;print(json.load(sys.stdin)["unseal_keys_b64"][1])')"
unset init_json

"${compose[@]}" exec -T vault vault operator unseal "$first_key" >/dev/null
"${compose[@]}" exec -T vault vault operator unseal "$second_key" >/dev/null
"${compose[@]}" exec -T -e VAULT_TOKEN="$root_token" vault vault secrets enable -path=secret kv-v2 >/dev/null

test_key="vault-smoke-only-jwt-signing-key-longer-than-32-bytes"
"${compose[@]}" exec -T -e VAULT_TOKEN="$root_token" vault vault kv put secret/user-profile \
    "AuthenticationProviderKey=$test_key" \
    "RabbitMqConnectionString=host=localhost;username=bizcord;password=bizcord" >/dev/null
printf 'path "secret/data/user-profile" { capabilities = ["read"] }\n' | \
    "${compose[@]}" exec -T -e VAULT_TOKEN="$root_token" vault vault policy write user-profile-read - >/dev/null
app_token="$("${compose[@]}" exec -T -e VAULT_TOKEN="$root_token" vault vault token create -field=token -policy=user-profile-read -no-default-policy -ttl=1h)"

assert_status 200 GET http://127.0.0.1:8200/v1/secret/data/user-profile "$app_token"
assert_status 403 GET http://127.0.0.1:8200/v1/secret/data/another-secret "$app_token"
assert_status 403 POST http://127.0.0.1:8200/v1/secret/data/user-profile "$app_token"
assert_status 403 GET http://127.0.0.1:8200/v1/secret/data/user-profile "invalid-token"

VAULT_TOKEN="$app_token" Vault__Enabled=true Vault__Address=http://127.0.0.1:8200 \
    ASPNETCORE_URLS=http://127.0.0.1:5194 \
    dotnet run --project apps/user-profile-microservice/src/UserProfile.Api/UserProfile.Api.csproj --no-launch-profile \
    > /tmp/user-profile-vault-smoke.log 2>&1 &
api_pid="$!"

api_status='000'
for attempt in {1..45}; do
    api_status="$(curl --silent --output /dev/null --write-out '%{http_code}' --max-time 2 http://127.0.0.1:5194/api/user-profiles || true)"
    if [ "$api_status" = "401" ]; then break; fi
    if ! kill -0 "$api_pid" >/dev/null 2>&1; then
        cat /tmp/user-profile-vault-smoke.log
        echo "User Profile service exited during Vault startup check" >&2
        exit 1
    fi
    sleep 2
done
if [ "$api_status" != "401" ]; then
    cat /tmp/user-profile-vault-smoke.log
    echo "User Profile service did not start using Vault" >&2
    exit 1
fi

"${compose[@]}" restart vault >/dev/null
status='000'
for attempt in {1..30}; do
    status="$(curl --silent --output /dev/null --write-out '%{http_code}' --max-time 5 http://127.0.0.1:8200/v1/sys/health || true)"
    if [ "$status" = "503" ]; then break; fi
    sleep 1
done
if [ "$status" != "503" ]; then
    echo "Expected sealed Vault after restart (HTTP 503), got $status" >&2
    exit 1
fi
assert_status 503 GET http://127.0.0.1:8200/v1/secret/data/user-profile "$app_token"

"${compose[@]}" exec -T vault vault operator unseal "$first_key" >/dev/null
"${compose[@]}" exec -T vault vault operator unseal "$second_key" >/dev/null
assert_status 200 GET http://127.0.0.1:8200/v1/secret/data/user-profile "$app_token"
echo "Vault initialization, scoped policies, API startup, and unseal checks passed."
