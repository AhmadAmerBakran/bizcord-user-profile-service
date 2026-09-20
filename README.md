# Bizcord User Profile Service

This repository contains the User Profile microservice for Bizcord, a Discord-like system developed as part of the System Integration course.

The service is being developed incrementally through the course. It currently contains the ASP.NET Core API setup, the internal user-profile domain model, shared profile contracts and the RabbitMQ messaging abstraction.

## Repository layout

```text
apps/
└── user-profile-microservice/
    ├── src/
    │   ├── UserProfile.Api/
    │   ├── UserProfile.Contracts/
    │   └── UserProfile.Domain/
    ├── tests/
    ├── Dockerfile
    └── README.md
```

Development details for the microservice are kept in `apps/user-profile-microservice/README.md`.

## Development workflow

Work for each course week is kept on its own branch and merged when the week's tasks are complete.

Other changes can still use short-lived branches when needed, for example:

```text
fix/profile-validation
chore/update-project-setup
```
