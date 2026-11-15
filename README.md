# Estiblazor

An English planning poker app with custom rooms, private voting, owner controls and permanent round history. Built with .NET 10 LTS and Blazor Interactive Server. No accounts are required.

## Run locally

Install Docker with Compose, then run from the repository root:

```sh
docker compose up -d --build --wait
```

Open [localhost:8080](http://localhost:8080). Use `PORT=8090 docker compose up -d` to choose another port. `GET /health` returns 200 when Redis is reachable and 503 otherwise. Compose binds only to loopback and deliberately permits a non-Secure cookie for local HTTP. For a shared deployment use HTTPS, set `Cookies__Secure=true`, and provide a persistent key directory. Redis is not published to the host.

Create a room, save the private management link, and share the separate invitation link. The creator can vote and manage the room. A valid management link grants owner access to another browser; it is a reusable bearer secret until rotated or invalidated by ownership transfer, so keep it private. Only its SHA-256 hash is stored. The HTTP recovery endpoint exchanges it for browser access and redirects to the clean room URL. Responses use `Referrer-Policy: no-referrer`; configure reverse proxies to omit query strings from access logs for `/manage/*`.

An encrypted, HttpOnly, SameSite=Lax cookie holds a random browser identity for one year. Same-name participants are independent; tabs in one browser share identity and votes. Keep the `browser-keys` volume: losing it invalidates browser identities and requires the saved management links to restore ownership.

Unknown codes never create rooms. Each phase permits a single vote per participant. Select the same card to withdraw, or another card to change it. Before reveal the server only returns your own vote values; other participants expose vote status. Reveal closes voting. Starting a new round archives a revealed round exactly once. Discarding an open round with votes requires confirmation and produces no history. Configuration edits are queued for the next round. Archived rounds keep their original deck, names at reveal, votes, optional title and reveal time. History shows five rounds per page.

## Session workflow

- **Recent and owned rooms:** return to rooms from the home page using this browser's protected identity.
- **Duplicate room:** copy the current deck into a fresh room with its own code, owner and management link.
- **Active phase:** owners guide everyone to the same discussion phase; all phases remain votable.
- **Observers:** watch a session without voting or counting as a missing voter.
- **Vote distributions:** after reveal, compare counts and differing cards with frozen names and voter totals.
- **Ownership:** add a co-owner or transfer the room to one participant. Transfer revokes all other owners and old recovery links; the new owner is prompted to save a new one.
- **QR invitations:** share a locally generated QR code or its labelled invitation link, without exposing management secrets.

Room lists belong to this browser identity and show up to 20 recent and 50 owned rooms. Old rooms appear after reopening them. QR invitations require a URL that other devices can reach; localhost links work only on the originating device.

## Built-in room templates

Choose from **12 templates**: Planning poker (the default), Fibonacci, Modified Fibonacci, T-shirt sizes, Powers of two, Score, Like / dislike, Confidence, Risk, Priority, Agreement, and Effort and risk.

Each template is copied into an editable draft. Loading a template replaces draft cards and keeps the room name. Edit phases/options freely; existing rooms and round history keep their saved decks. Owner changes in an existing room take effect in the next round.

The public [how-to guide](https://estiblazor.nl/how-it-works) lists the built-in catalog and card values.

## Solution

| Project | Responsibility |
| --- | --- |
| `src/Estiblazor.Domain` | Immutable room state, validation, authorization and round transitions |
| `src/Estiblazor.Application` | Async commands, privacy-filtered read models, storage/session/update contracts |
| `src/Estiblazor.Infrastructure` | Versioned JSON in Redis hashes with atomic version-checked transactions |
| `src/Estiblazor.UI/Estiblazor.UI` | Blazor components, protected cookie identity and circuit presence |
| `tests/Estiblazor.Tests` | Domain, application and real-Redis integration tests |
| `tests/browser` | Playwright owner/guest, mobile, outage and persistence checks |

Redis is the sole source of room data. Schema version 1 includes backward-compatible optional observer, active-phase and recovery-reminder fields. Browser room-directory sorted sets share the room-store prefix. Schema version 1 stores each room under `estiblazor:room:<CODE>` as `version` and `data` hash fields. Rooms have no TTL. Successful compare-and-swap transactions increment the version; conflicts retry against the latest stored state. Commands carry an ID and round ID, making retries idempotent and rejecting commands from an old round. Configuration changes also require the editor's version. A delete atomically removes the key; stale writers cannot recreate it. Future schema migrations must explicitly handle the schema version; unknown versions are rejected.

Presence and subscriptions are temporary and confined to **one app instance**. Circuit disconnects remove presence; reconnects notify subscribers and reload Redis state. Removing a tab only removes its own subscription. App restarts require clients to reload if the previous Blazor circuit no longer exists. No room state is reconstructed from process memory.

## Configuration

| Setting | Required / default |
| --- | --- |
| `ConnectionStrings__Redis` | Required outside Compose; e.g. `localhost:6379` or authenticated TLS connection options |
| `DataProtection__KeysPath` | `/data/keys`; must be writable and persist across app/container recreation |
| `Cookies__Secure` | `true`; use `false` only for local HTTP |
| `Site__PublicUrl` | Canonical public HTTPS origin; Compose defaults to localhost |
| `Accessibility__ContactUrl` | Monitored feedback URL or mailto link; defaults to repository issues |
| `ReverseProxy__KnownNetwork` | Optional trusted proxy CIDR; enables forwarded HTTPS scheme only from that network |
| `ASPNETCORE_URLS` | Container: `http://+:8080`; terminate HTTPS at your trusted reverse proxy |

Without Docker, install a .NET 10 SDK and a Redis server, set the settings above (use a local writable key path), then run:

```sh
dotnet run --project src/Estiblazor.UI/Estiblazor.UI
```

The SDK is selected by `global.json`. .NET 10 is an [LTS release](https://dotnet.microsoft.com/en-us/platform/support/policy).

## Verify

With a .NET 10 SDK and Redis available:

```sh
dotnet build src/Estiblazor.sln -c Release
TEST_REDIS=localhost:6379 dotnet test src/Estiblazor.sln -c Release
```

Or use Docker against the Compose Redis, without installing .NET:

```sh
docker run --rm --network estiblazor_default \
  --user "$(id -u):$(id -g)" -e DOTNET_CLI_HOME=/tmp -e TEST_REDIS=redis:6379 \
  -v "$PWD:/work" -w /work mcr.microsoft.com/dotnet/sdk:10.0 \
  dotnet test src/Estiblazor.sln -c Release
```

If you change the Compose project name, replace `estiblazor_default` with its network. Integration tests use unique key prefixes and clean up their own rooms. They run when `TEST_REDIS` is set; CI requires it. They cover concurrent joins/votes, duplicate transitions, management recovery, unavailable Redis and deletion without resurrection.

For browser tests, first start Compose. Node.js and Docker access are required:

```sh
npm ci --prefix tests/browser
npx --prefix tests/browser playwright install --with-deps chromium
npm test --prefix tests/browser
```

Set `BASE_URL` to override `http://localhost:8080`. These tests **restart the local Compose app and Redis**, briefly stop Redis, and delete the rooms they create. Run against a dedicated local stack. Tests cover independent same-name guests, owner permissions, multiple tabs, management recovery, reconnect/reload, safe custom text, mobile ordering, vote withdrawal, history and persistence. They also verify browser-private room lists, short visits, independent duplicate decks, owner-guided phases, observers, frozen distributions, co-owner grants, secure transfer and QR rendering. Screenshots and failure traces are written under `tests/browser/test-results`. On an unsupported newer Ubuntu host use `PLAYWRIGHT_HOST_PLATFORM_OVERRIDE=ubuntu24.04-x64` for install and test, or set `CHROMIUM_PATH` to a compatible installed browser.

CI builds and tests the entire solution with Redis, builds the runtime image, and runs the Compose browser/restart checks. The Azure workflow is **manual**, depends on the same checks, requires a `production` environment, an explicit Redis secret and persistent key mount, and enforces exactly one replica. It does not provision infrastructure. Configure `ESTIBLAZOR_REDIS`, the existing registry/Azure credentials and `ESTIBLAZOR_KEYS_PATH` plus `ESTIBLAZOR_PUBLIC_URL` before invoking it. Optionally set `ESTIBLAZOR_ACCESSIBILITY_CONTACT` to your monitored feedback URL. Configure environment approval rules in GitHub if required by your deployment process.

## Production behind Cloudflare Tunnel

[estiblazor.nl](https://estiblazor.nl) runs from this repository's source using `compose.production.yaml`. This standalone file publishes no host ports. The app joins the external `cloudflare` network as `estiblazor`, while Redis stays on a private internal network. Redis data and browser keys persist in named volumes. Secure cookies are enabled and forwarded HTTPS information is accepted only from the configured network.

```sh
cp .env.production.example .env
# Set PUBLIC_HOST and CLOUDFLARE_SUBNET to your actual hostname and Docker subnet.
docker network inspect cloudflare
docker compose -f compose.production.yaml config --quiet
docker compose -f compose.production.yaml up -d --build
curl --fail https://estiblazor.nl/health
```

Cloudflared must also join `cloudflare`; persist that external network in its own Compose file. Its public hostname route is `http://estiblazor:8080`. Neither Redis nor the app needs a public host port. Do not enable multiple app replicas.

On the OVH host, `/srv/ovh-migration/staged/estiblazor/compose.yaml` includes `repository/compose.production.yaml`. The `repository` directory is a Git checkout of `feature/persistent-rooms-net10`, and `repository/.env` holds production configuration. After updating the source, rebuild from the parent directory with `sudo docker compose up -d --build`; inspect with `sudo docker compose ps` and `sudo docker compose logs --tail 100 app`. Fetch and fast-forward the checked-out branch before rebuilding; keep `.env` unchanged. Updating GitHub does not automatically deploy this host.

The previous Compose files are retained in the server's `backups/` directory, and the old stopped .NET 8 container is named `estiblazor-v8-rollback`. It cannot read the new Redis room format. Never use `--remove-orphans` if you want to preserve that container. Use the backup procedure below for both new volumes; production commands must select the production Compose file or run from the server's parent deployment directory. Never use `down -v` on production.

## Durability, backup and restore

Compose configures a persistent Redis volume with AOF, `appendfsync everysec` and `maxmemory-policy noeviction`. Writes fail visibly when Redis cannot accept them. UI state is updated only after successful storage; a timeout may mean the outcome is uncertain, so reload before retrying a transition. AOF every-second syncing can lose approximately the last second of writes in a system crash, as described in the [Redis persistence documentation](https://redis.io/docs/latest/operate/oss_and_stack/management/persistence/). Persistence is not a backup. Monitor disk space and Redis memory; room histories and command receipts have no automatic retention limit.

For a consistent offline backup, stop the stack and archive **both** volumes. The examples below use Compose's default project name `estiblazor`; adjust if needed. The helper container reads the volumes and writes only the backup directory:

```sh
mkdir -p backups
docker compose stop
docker run --rm -v estiblazor_redis-data:/source:ro -v "$PWD/backups:/backup" \
  alpine:3.22 tar czf /backup/redis-data.tgz -C /source .
docker run --rm -v estiblazor_browser-keys:/source:ro -v "$PWD/backups:/backup" \
  alpine:3.22 tar czf /backup/browser-keys.tgz -C /source .
docker compose start
```

Copy the archives to protected off-host storage. They include room content and cryptographic key material. Keep the full Redis directory including the multipart AOF manifest and files, not just `dump.rdb`.

To restore into a **new, empty** stack/host (restoring over existing data replaces that dataset): create the volumes without starting services, unpack the archives, then start:

```sh
docker compose create
docker run --rm -v estiblazor_redis-data:/target -v "$PWD/backups:/backup:ro" \
  alpine:3.22 tar xzf /backup/redis-data.tgz -C /target
docker run --rm -v estiblazor_browser-keys:/target -v "$PWD/backups:/backup:ro" \
  alpine:3.22 tar xzf /backup/browser-keys.tgz -C /target
docker compose up -d --wait
```

Verify `/health`, an existing room's configuration/history/current votes and owner access in an existing browser. Test restores periodically. Preserve archive numeric ownership so the runtime `app` user can read its key ring. `docker compose down` preserves named volumes; `docker compose down -v` permanently deletes rooms and browser keys.

This version does not include accounts, backlogs, final estimates, exports, personal templates, multiple app replicas or a migration for old in-memory rooms. Those rooms never existed in repository storage. The production deployment uses Docker Compose behind Cloudflare Tunnel; no Azure infrastructure was provisioned.

## Performance, accessibility, SEO and AEO

Public pages render on the server without downloading Blazor. Interactive room pages prerender, and assets use .NET 10 compression, fingerprints and immutable caching. Inter and JetBrains Mono are self-hosted, with no external font requests. The navy/cyan visual theme includes an EB favicon and a light-theme alternative.

The accessibility target is WCAG 2.2 AA, with axe/keyboard/reflow/contrast checks in CI and a public `/accessibility` page. Full EAA compliance requires the operator's applicable legal assessment and a complete assistive-technology audit; automated scores alone are insufficient.

Set `Site__PublicUrl` (Compose: `PUBLIC_URL`) to the canonical HTTPS origin and `Accessibility__ContactUrl` (Compose: `ACCESSIBILITY_CONTACT_URL`) to your monitored feedback channel. Public metadata, sitemap, visible answers and JSON-LD support discovery. Room responses carry `noindex`; private codes and management links are excluded from discovery metadata.

Run `npm run audit --prefix tests/browser` after the browser tests to generate mobile/desktop Lighthouse reports and enforce quality thresholds.

Latest local verification: **16 domain/Redis tests and 12 browser checks passed**. All four public pages scored **100 in all Lighthouse categories on mobile and desktop**. These are local lab measurements, not field performance guarantees.
