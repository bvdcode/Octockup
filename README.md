# Octockup

![Status](https://img.shields.io/badge/status-beta-yellow)
[![License](https://badgen.net/github/license/bvdcode/octockup)](LICENSE)
[![CI](https://github.com/bvdcode/octockup/actions/workflows/docker-image.yml/badge.svg)](https://github.com/bvdcode/octockup/actions)
[![CodeFactor](https://www.codefactor.io/repository/github/bvdcode/octockup/badge)](https://www.codefactor.io/repository/github/bvdcode/octockup)
[![Release](https://badgen.net/github/release/bvdcode/octockup?label=version)](https://github.com/bvdcode/octockup/releases)
[![Docker Pulls](https://badgen.net/docker/pulls/bvdcode/octockup?icon=docker&label=pulls)](https://hub.docker.com/r/bvdcode/octockup)
[![Docker Image Size](https://badgen.net/docker/size/bvdcode/octockup?icon=docker&label=size)](https://hub.docker.com/r/bvdcode/octockup)
[![Github last-commit](https://img.shields.io/github/last-commit/bvdcode/octockup)](https://github.com/bvdcode/octockup/commits/main/)

> Live: [octockup.splidex.com](https://octockup.splidex.com)

Octockup is an all-in-one client and server application for autobackup that includes both backend and frontend in a single Docker container. It allows you to gather and manage data from various sources, such as YouTube, SSH, FTP, Email, and more, directly through the browser.

## Key Features

- **Containerization:** A single Docker container includes all necessary components.
- **Backend and Frontend:** Full integration of backend and frontend for simplified deployment.
- **Incremental Backups:** Save only the necessary changes with each backup.
- **Connecting Various Sources:** You can connect YouTube, SSH, FTP, and many other sources to gather data.
- **Web Interface:** User-friendly web interface for managing all application functions.
- **Multibase:** Octockup uses SQLite by default, but switches to PostgreSQL if environment variables are specified.

## Installation

Dockerhub: [Link](https://hub.docker.com/r/bvdcode/octockup)

1. Make sure you have Docker and Docker Compose installed.
2. Create `docker-compose.yml` file:

```yaml
services:
  octockup:
    image: bvdcode/octockup:latest
    ports:
      - 8080:8080
    environment:
      - MASTER_KEY=${OCTOCKUP_MASTER_KEY} # 32 chars master key for encrypting sensitive data in the database
    volumes:
      - /data/octockup:/app/data
      - /data:/app/data/mounts/data:ro
```

3. Start the application using Docker Compose:

```bash
docker compose up -d
```

## Usage

1. Open your browser and navigate to the address where the application is running.
2. Log in and set up connections to the necessary data sources (YouTube, SSH, FTP, etc.).
3. Start gathering and managing data using the user-friendly web interface.

## Configuration

### Configuration Files

- **`docker-compose.yml`** - Docker Compose configuration for managing the container.

### Environment variables

```yaml
MASTER_KEY: A 32-character master key for encrypting sensitive data in the database.
```

## Updating

To update to the latest version of the application, follow these steps:

1. Update the image:
   ```bash
   docker compose pull
   ```
2. Restart the application:
   ```bash
   docker compose up -d
   ```

## Support

If you have any questions or issues, please create a new issue on GitHub or contact me via email:

octockup-github-support@belov.us

---
