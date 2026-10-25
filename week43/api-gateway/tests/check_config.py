"""Check the Week 43 gateway route configuration without running Docker."""

from pathlib import Path
import json

file_path = Path(__file__).resolve().parents[1] / 'src/Configuration/ocelot.user-profile.json'
config = json.loads(file_path.read_text(encoding='utf-8'))
routes = config['Routes']
assert len(routes) == 6
assert len({route['Key'] for route in routes}) == len(routes)
assert len({(route['UpstreamPathTemplate'], tuple(route['UpstreamHttpMethod'])) for route in routes}) == len(routes)
assert {route['UpstreamHttpMethod'][0] for route in routes} == {'GET', 'POST', 'PUT', 'DELETE'}
for route in routes:
    assert route['DownstreamHostAndPorts'] == [{'Host': 'host.docker.internal', 'Port': 5194}]
    assert route['AuthenticationOptions']['AuthenticationProviderKeys'] == ['Bearer']
    assert route['AuthenticationOptions']['AllowedScopes'] in (['profile.read'], ['profile.write'])
    assert route['RouteClaimsRequirement']['role'] in ('admin', 'member')
    assert route['AddHeadersToRequest']['X-UserId'] == 'Claims[sub] > value'
    assert route['AddHeadersToRequest']['X-Role'] == 'Claims[role] > value'
    assert route['DownstreamPathTemplate'].startswith('/api/user-profiles')
me = next(route for route in routes if route['Key'] == 'user-profile-self-read')
assert me['ChangeDownstreamPathTemplate'] == {'userId': 'Claims[sub] > value'}
assert '{userId}' in me['DownstreamPathTemplate']
assert me['Priority'] > 1
assert me['RouteClaimsRequirement']['role'] == 'member'
print('Week 43 route configuration passed static checks.')
