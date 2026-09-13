# Bizcord User Profile Service

This repository contains the User Profile microservice for Bizcord, a Discord-like system developed as part of the System Integration course.

The service is intended to handle profile-related data and operations. The API, persistence, and messaging details will be added as the service is developed through the course.

## Project structure

```text
src/        Application source code
tests/      Automated tests
Dockerfile  Container definition
```

At the moment, the repository only contains the initial project structure. The Dockerfile is intentionally empty and will be completed when the service is ready to be containerized.

## Development workflow

The `main` branch should stay stable and ready to deploy. New work is done in short-lived branches and merged back into `main` when it is ready.

Branch names should describe the type of change being made, for example:

```text
feat/add-profile-endpoint
fix/profile-validation
chore/update-project-setup
```
