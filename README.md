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
```
