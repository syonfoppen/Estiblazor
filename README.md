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

The public [how-to guide](http://localhost:8080/how-it-works) lists the built-in catalog and card values.

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

CI builds and tests the entire solution with Redis, builds the runtime image, and runs the Compose browser, restart and Lighthouse checks.

## Self-hosting

Estiblazor can run on any host with Docker and Compose. Build the app from the repository using `compose.yaml`, and place an HTTPS reverse proxy in front of the loopback-bound app port (`127.0.0.1:8080` by default). Redis stays private to the Compose network.

Set `PUBLIC_URL` in your local `.env` file to your public HTTPS origin, such as `https://poker.example.com`. Set `ACCESSIBILITY_CONTACT_URL` to a monitored feedback URL or email address. Enable secure cookies with a local `compose.override.yaml`:

```yaml
services:
  app:
    environment:
      Cookies__Secure: "true"
```

Compose loads this override automatically when running `docker compose up -d --build`. The local HTTP configuration deliberately uses non-Secure cookies; the override is required when publishing behind HTTPS.

Configure your reverse proxy to support WebSockets for interactive rooms and preserve the original Host header. If it forwards the HTTPS scheme, set `ReverseProxy__KnownNetwork` on the app to the proxy's actual source network in CIDR notation. Trust only the proxy network. Use the configuration table above when supplying environment variables outside Compose.

Keep exactly one app instance and preserve both the Redis and browser-key volumes across updates. Verify `/health`, room creation, voting and owner access through your public URL. Use `docker compose logs --tail 100 app` to inspect app logs. To update, fetch the desired repository version and run `docker compose up -d --build` again, keeping your local configuration and persistent volumes.

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

This version does not include accounts, backlogs, final estimates, exports, personal templates, multiple app replicas or a migration for old in-memory rooms. Those rooms never existed in repository storage.

## Performance, accessibility, SEO and AEO

Public pages render on the server without downloading Blazor. Interactive room pages prerender, and assets use .NET 10 compression, fingerprints and immutable caching. Inter and JetBrains Mono are self-hosted, with no external font requests. The navy/cyan visual theme includes an EB favicon and a light-theme alternative.

The accessibility target is WCAG 2.2 AA, with axe/keyboard/reflow/contrast checks in CI and a public `/accessibility` page. Full EAA compliance requires the operator's applicable legal assessment and a complete assistive-technology audit; automated scores alone are insufficient.

Set `Site__PublicUrl` (Compose: `PUBLIC_URL`) to the canonical HTTPS origin and `Accessibility__ContactUrl` (Compose: `ACCESSIBILITY_CONTACT_URL`) to your monitored feedback channel. Public metadata, sitemap, visible answers and JSON-LD support discovery. Room responses carry `noindex`; private codes and management links are excluded from discovery metadata. The `/robots.txt` endpoint serves the editable [crawler rules](src/Estiblazor.UI/Estiblazor.UI/robots.txt) and appends an absolute sitemap URL using `Site__PublicUrl`.

Run `npm run audit --prefix tests/browser` after the browser tests to generate mobile/desktop Lighthouse reports and enforce quality thresholds.

Latest local verification: **16 domain/Redis tests and 12 browser checks passed**. All four public pages scored **100 in all Lighthouse categories on mobile and desktop**. These are local lab measurements, not field performance guarantees.
