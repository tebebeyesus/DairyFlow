# DairyFlow — Pilot Deployment (Hetzner + DuckDNS, ~$5/month)

Get DairyFlow live over HTTPS on a single small server, for a pilot of up to ~10 farms.
Everything runs in Docker: SQL Server Express, the app, and Caddy (free automatic HTTPS).

**Total cost:** ~€3.79/mo server + free DuckDNS domain + free Let's Encrypt certs.
**Time:** ~30–45 minutes.

---

## 1. Get a free DuckDNS domain

1. Go to <https://www.duckdns.org> and sign in (GitHub/Google).
2. Pick a subdomain, e.g. `yourfarm` → you get **`yourfarm.duckdns.org`**.
3. Leave the page open — you'll set its IP in step 3 once you have the server.

## 2. Create the server (Hetzner Cloud)

- <https://console.hetzner.cloud> → **New project** → **Add server**.
- **Location:** any EU (Germany/Finland). **Image:** Ubuntu (latest LTS).
- **Type:** **CX22** (2 vCPU, 4 GB RAM, x86) — ~€3.79/mo. ⚠️ Must be x86 (CX/CPX), **not** ARM (CAX) — SQL Server needs x86-64.
- Add your SSH key, create the server, and copy its **public IP**.

## 3. Point the domain at the server

Back on DuckDNS, set your subdomain's **current ip** to the server's public IP → **update**.
(Check it resolves: `ping yourfarm.duckdns.org` should show the server IP.)

## 4. Open the firewall

In Hetzner → your server → **Firewalls**, allow inbound **22 (SSH), 80, 443**. Do **not** expose 1433.

## 5. Install Docker on the server

SSH in (`ssh root@<server-ip>`), then:
```bash
curl -fsSL https://get.docker.com | sh
```

## 6. Get the code onto the server

```bash
git clone <your-repo-url> dairyflow && cd dairyflow
# (or copy the project up with scp/rsync)
```

## 7. Configure secrets

```bash
cp .env.example .env
nano .env
```
Set:
- `SA_PASSWORD` — a strong SQL password (upper+lower+digit, no spaces).
- `JWT_KEY` — run `openssl rand -base64 48` and paste the result.
- `DOMAIN` — your `yourfarm.duckdns.org`.

## 8. Launch

```bash
docker compose up -d --build
```
What happens (first run ~2–3 min to build):
1. SQL Server starts; the app retries until it's ready, then **auto-creates the database**.
2. Caddy requests a Let's Encrypt certificate for your domain (needs steps 3–4 done).

Check it:
```bash
docker compose ps
docker compose logs -f caddy   # watch for the certificate being obtained
docker compose logs -f app
```

Then open **`https://yourfarm.duckdns.org`** and register your farm. On an Android phone, Chrome → ⋮ → **Add to Home screen** installs it as an app that works offline.

---

## Day-2 operations

**Deploy an update**
```bash
git pull
docker compose up -d --build
```
Your data persists in the `sqldata` volume — redeploys don't touch it.

**Back up the database**
```bash
docker exec dairyflow-sql /opt/mssql-tools18/bin/sqlcmd -S localhost -U sa -P "$SA_PASSWORD" -C \
  -Q "BACKUP DATABASE DairyFlow TO DISK='/var/opt/mssql/DairyFlow.bak' WITH INIT"
docker cp dairyflow-sql:/var/opt/mssql/DairyFlow.bak ./DairyFlow.bak
```
(Schedule with `cron`; also snapshot the `sqldata` volume.)

**Connect a SQL client** from your laptop (1433 isn't public — tunnel over SSH):
```bash
ssh -L 1433:127.0.0.1:1433 root@<server-ip>
# then connect a client to localhost,1433 as sa
```

---

## Notes

- **Certs need steps 3–4 first.** If Caddy can't get a certificate, confirm the domain resolves to the server IP and ports 80/443 are open, then `docker compose restart caddy`.
- **Runtime is .NET 7 (end-of-life).** Fine to launch a pilot, but plan to bump to .NET 8 LTS (change the `TargetFramework`s to `net8.0`, the `7.0.*` packages to `8.0.*`, and the two image tags in the `Dockerfile` to `8.0`).
- **DB login uses `sa`** for simplicity. For tighter security later, create a dedicated SQL login scoped to the DairyFlow database and swap it into the connection string in `docker-compose.yml`.
- **Adding a separate test site later** is easy: add a second `app` service on another database + a second domain block in the Caddyfile.
