# Deploying DairyFlow (prod + test on one server)

> **Deploying a pilot?** Use **[PILOT-DEPLOY.md](PILOT-DEPLOY.md)** instead — a simpler single-site setup
> on a free DuckDNS domain that matches the current `docker-compose.yml`. This document describes the
> older dual-site (prod + test) layout with custom domains, kept for reference.

This runs **two sites** (production + test) and **two databases** on a single Hetzner Cloud server:

| Container        | Purpose            | Database         | URL                         |
|------------------|--------------------|------------------|-----------------------------|
| `dairyflow-sql`  | SQL Server Express | both DBs         | internal only               |
| `dairyflow-prod` | production site    | `DairyFlow_Prod` | `https://$PROD_DOMAIN`      |
| `dairyflow-test` | test site (Swagger on) | `DairyFlow_Test` | `https://$TEST_DOMAIN` |
| `dairyflow-caddy`| HTTPS reverse proxy| —                | ports 80/443                |

One SQL Server instance holds both databases (Express allows many databases, 10 GB each), which keeps memory use down. Each app creates its own database automatically on first start.

---

## 1. Pick the server

- A Hetzner **CX** (Intel x86) or **CPX** (AMD x86) instance — **not** a CAX/ARM one (SQL Server needs x86‑64).
- **≥ 4 GB RAM minimum; 8 GB recommended** (SQL Express alone uses ~1.5 GB, plus two app containers).
- OS: **Ubuntu** (latest LTS).

## 2. DNS

Create two **A records** pointing at the server's public IP, e.g.:
```
dairyflow.example.com        → <server IP>
test.dairyflow.example.com   → <server IP>
```
HTTPS won't be issued until these resolve.

## 3. Server firewall

Open inbound **22 (SSH), 80, 443**. (Don't expose 1433 — SQL is bound to localhost only.)

## 4. Install Docker

```bash
curl -fsSL https://get.docker.com | sh
```
This installs Docker Engine + the Compose plugin (both free).

## 5. Get the code onto the server

```bash
git clone <your-repo-url> dairyflow && cd dairyflow
# or: scp/rsync the project folder up
```

## 6. Configure secrets

```bash
cp .env.example .env
nano .env          # set SA_PASSWORD, JWT_KEY_PROD, JWT_KEY_TEST, PROD_DOMAIN, TEST_DOMAIN
```
Generate strong values:
```bash
openssl rand -base64 48     # run twice for the two JWT keys
```

## 7. Launch

```bash
docker compose up -d --build
```
What happens:
1. Image builds (first time ~2–3 min).
2. SQL Server starts (~30 s); the app containers retry until it's ready, then **auto-create `DairyFlow_Prod` and `DairyFlow_Test`**.
3. Caddy obtains Let's Encrypt certificates for both domains.

Check status / logs:
```bash
docker compose ps
docker compose logs -f app-prod
```

Then browse to `https://$PROD_DOMAIN` (register your farm) and `https://$TEST_DOMAIN`.

---

## Day-2 operations

**Deploy an update**
```bash
git pull
docker compose up -d --build      # rebuilds the app image, recreates app containers
```
Your databases persist in the `sqldata` volume — they are not touched by redeploys.

**Back up the databases**
```bash
docker exec dairyflow-sql /opt/mssql-tools18/bin/sqlcmd -S localhost -U sa -P "$SA_PASSWORD" -C \
  -Q "BACKUP DATABASE DairyFlow_Prod TO DISK='/var/opt/mssql/DairyFlow_Prod.bak' WITH INIT"
docker cp dairyflow-sql:/var/opt/mssql/DairyFlow_Prod.bak ./DairyFlow_Prod.bak
```
(Schedule this with `cron`. The `sqldata` Docker volume itself is also worth snapshotting.)

**Reset just the test database** (fresh start for testing)
```bash
docker exec dairyflow-sql /opt/mssql-tools18/bin/sqlcmd -S localhost -U sa -P "$SA_PASSWORD" -C \
  -Q "DROP DATABASE DairyFlow_Test"
docker compose restart app-test    # it recreates an empty DairyFlow_Test on boot
```

**Connect a SQL client** from your laptop: SSH-tunnel `1433` (it isn't public):
```bash
ssh -L 1433:127.0.0.1:1433 user@<server IP>
# then connect a client to localhost,1433 as sa
```

---

## Notes / hardening (later)

- **Runtime:** the image is built on **.NET 7, which is end-of-life**. Fine to start, but plan to bump the projects to **.NET 8 LTS** (change `TargetFramework` to `net8.0`, the EF/ASP.NET packages to `8.0.*`, and the two image tags in the `Dockerfile` to `8.0`). This also needs the .NET 8 SDK on your dev box.
- **DB login:** the apps connect as `sa` for simplicity. For tighter security, create a dedicated SQL login per database with only `db_owner` on its own DB and swap it into the connection strings.
- **HTTPS redirect:** the app sits behind Caddy (which terminates TLS), so the in-app `UseHttpsRedirection` is a harmless no-op here.
