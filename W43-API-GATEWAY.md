# Week 43 API Gateway work

This branch contains the API gateway exercises from System Integration, week 43. The files are in [week43/api-gateway](week43/api-gateway/README.md).

Task 01 covers Ocelot routing to our User Profile API. Task 02 adds JWT authentication, role and scope checks, and claim forwarding. The gateway remains a separate application based on Patrick Stolc's teaching repository, https://github.com/patrickstolc/api-gateway. Our own User Profile service is not moved into that repository.

This branch is being kept as a reference. It is not scheduled for a merge into main or master. The route configuration and local scripts are here so we can return to them when the system needs a shared gateway. Check the downstream address, identity flow, and deployment rules again before using this in a later integration.

The configuration has passed local structural checks. Full HTTP tests still need to be run with the gateway, RabbitMQ, and User Profile API running together. No production authentication is provided by the teaching token faucet.

The teaching instructions ask for a local commit in the separate gateway clone and do not require a push to the lecturer's repository. This branch is our own copy for continuity between course weeks.
