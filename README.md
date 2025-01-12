# Octockup
_IN DEVELOPMENT_

>Live: [https://backup.belov.us](https://backup.belov.us)

Octockup is an all-in-one client and server application for autobackup that includes both backend and frontend in a single Docker container. It allows you to gather and manage data from various sources, such as YouTube, SSH, FTP, and more, directly through the browser.

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
    container_name: octockup
    ports:
      - 8080:8080
    restart: always
    volumes:
      - /data/octockup:/app/files
    # environment:
      # if necessary use variables below
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
CORS_ORIGINS: allowed origins
POSTGRES_HOST: database address
POSTGRES_PORT: port, ex. 5432
POSTGRES_DATABASE: database where data will be stored, ex. octockup
POSTGRES_USERNAME: username to connect
POSTGRES_PASSWORD: database user password
```

## Updating

To update to the latest version of the application, follow these steps:

1. Update the image:
    ```bash
    docker compose pull
    ```
3. Restart the application:
    ```bash
    docker compose up -d
    ```

## Support

If you have any questions or issues, please create a new issue on GitHub or contact me via email: octockup-github-support@belov.us

## License

This project is licensed under the [MIT License](LICENSE).

---
