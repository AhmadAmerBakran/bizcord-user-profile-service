import json
from pathlib import Path

route_file = Path(__file__).with_name("ocelot.user-profile.json")
routes = json.loads(route_file.read_text(encoding="utf-8"))["Routes"]
assert len(routes) == 7
keys = [route["Key"] for route in routes]
assert len(keys) == len(set(keys))
for route in routes:
    assert route["AuthenticationOptions"]["AuthenticationProviderKeys"] == ["Bearer"], route["Key"]
internal = next(route for route in routes if route["Key"] == "user-profile-internal-read")
assert internal["UpstreamPathTemplate"] == "/internal/user-profiles/{userId}"
assert internal["RouteClaimsRequirement"] == {"role": "internal-service"}
assert internal["AuthenticationOptions"]["AllowedScopes"] == ["profile.internal.read"]
assert internal["UpstreamHttpMethod"] == ["GET"]
print("Week 45 gateway routes are valid JSON and have the required internal policy.")
