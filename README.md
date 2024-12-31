# Octockup [IN DEVELOPMENT]
Client and server application for autobackup

[https://backup.belov.us](https://backup.belov.us)

# Environment variables

CORS_ORIGINS: allowed origins


# Docker-compose

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
    environment:
      POSTGRES_HOST: postgres-server
      POSTGRES_PORT: 5432
      POSTGRES_DATABASE: octockup
      POSTGRES_USERNAME: octockup_client
      POSTGRES_PASSWORD: j7Hx0UjzlFS250pZf6e5Z7yxgmO88xZ6
      CORS_ORIGINS: localhost 10.0.0.100
```

# Database

If you want to use your own database - you can specify env

# Dockerhub

[Link](https://hub.docker.com/r/bvdcode/octockup)
