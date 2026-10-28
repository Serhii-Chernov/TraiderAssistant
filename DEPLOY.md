# Server deployment

The Web API runs in Docker and connects to an existing PostgreSQL instance through the external Docker network `services_default`.

## 1. Create the database

If PostgreSQL runs in Docker, replace `<postgres-container>` with its container name:

```bash
docker exec -it <postgres-container> createdb -U postgres traider_assistant
```

If PostgreSQL runs directly on the server:

```bash
sudo -u postgres createdb traider_assistant
```

The database must exist before the API starts. EF Core migrations create and update its tables automatically.

## 2. Configure the instance

```bash
cp instances/trader-assistant/.env.example instances/trader-assistant/.env
nano instances/trader-assistant/.env
```

Set `Host` to the PostgreSQL service or container DNS name visible in `services_default`.

## 3. Build and start

```bash
docker network inspect services_default
docker compose build
docker compose up -d
docker compose logs -f trader-assistant-api
```

Swagger is available at `http://<server>:5080/swagger`.

The trained ML model is stored in `./instances/trader-assistant/data/global_model.zip` and survives container replacement.

## 4. Update

```bash
git pull
docker compose build
docker compose up -d
docker compose logs --tail=200 trader-assistant-api
```
