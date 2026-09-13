# Bizcord User Profile Service

This repository contains the User Profile microservice for Bizcord, a Discord-like system developed as part of the System Integration course.

The service is being developed incrementally through the course. The current version contains the initial ASP.NET Core Web API boilerplate; profile-specific API, persistence, and messaging functionality will be added in later tasks.

## Repository layout

```text
apps/
└── user-profile-microservice/
    ├── src/
    │   └── UserProfile.Api/
    ├── tests/
    ├── Dockerfile
    └── README.md
```

Development details for the microservice are kept in `apps/user-profile-microservice/README.md`.

## Development workflow

The `main` branch should stay stable. New work is done in short-lived branches and merged back into `main` when it is ready.

Examples:

```text
feat/add-profile-endpoint
fix/profile-validation
chore/update-project-setup
```
