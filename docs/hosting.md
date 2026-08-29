# Where to host this

Short version: run it at home on a small always on device, and reach it over
Tailscale. Free cloud hosting does not work for this app, for a reason that has
nothing to do with pricing.

## Why free cloud hosting fails

**YouTube blocks datacenter IP ranges.** Cloud provider address blocks are
public and heavily flagged by bot detection. From AWS, GCP, Azure, Oracle,
Fly.io, Render or Railway, yt-dlp returns "Sign in to confirm you're not a bot"
instead of audio. There is no flag or setting that fixes this. The usual
workarounds are supplying account cookies, which means handing a cloud box your
Google credentials, or paying for a residential proxy, which costs more per
month than the hardware costs once.

A home internet connection is a residential IP, and it does not have this
problem. That single fact decides the architecture.

Verified on 2026-08-29: resolving and downloading a YouTube track through the
Docker container on a home connection works first try.

**What still works from a datacenter IP.** SoundCloud, Bandcamp, Archive.org,
podcast feeds and direct media links have no comparable bot detection. If those
are the only sources you use, cloud hosting is viable and Oracle Cloud Always
Free is the pick. If YouTube matters, host at home.

Three smaller problems compound it:

- **Ephemeral disk.** Render and Fly.io free tiers wipe the filesystem on every
  restart, taking the SQLite database and cached audio with them.
- **Cold starts.** Free tiers sleep after inactivity, so the first play waits
  for a container to boot.
- **No authentication.** This app has none. A public URL is usable by anyone
  who finds it.

## Why not Firebase specifically

Firebase fails on four separate counts:

1. Firebase Hosting serves static files only, the same blocker as GitHub Pages.
2. Cloud Functions has no .NET runtime.
3. The free Spark plan blocks all outbound network calls to non-Google
   services, so yt-dlp could not reach YouTube even in principle.
4. Cloud Functions requires the paid Blaze plan regardless, and it would still
   run on a Google datacenter IP.

Cloud Run does run .NET containers, but it is still a Google datacenter IP, so
point 4 stands.

## What to run it on

Any always on device on your home network. Ranked by how well they fit:

| Device | One time cost | Power | Notes |
| --- | --- | --- | --- |
| NAS with Docker | already owned | already on | Best option if you have one |
| Raspberry Pi 5 (4GB) | around 6000 INR | ~3W | Buy once, runs for years |
| Old laptop or mini PC | free if spare | 10 to 20W | Close the lid, set it to stay awake |

Running cost on a Pi at 3W is roughly 26 kWh a year, about 200 INR. Tailscale
is free. There is no monthly bill.

The image is multi-arch and builds on ARM64, so a Pi works without changes.

## Setup

### 1. Install Docker on the device

Raspberry Pi OS or any Debian based system:

```bash
curl -fsSL https://get.docker.com | sh
sudo usermod -aG docker $USER
```

Log out and back in for the group change to apply. On a Synology or QNAP NAS,
install Container Manager or Container Station from the package centre instead.

### 2. Build and start

```bash
git clone <your repo url> music-player
cd music-player
docker compose up -d --build
```

The first build takes a few minutes on a Pi. Check it came up:

```bash
docker compose ps
curl http://localhost:5080/health
```

You want `{"status":"ok","cacheBytes":0}`.

Playlists and cached audio live in the `player-data` Docker volume and survive
rebuilds. The compose file binds the port to `127.0.0.1` only, so nothing is
exposed to the network yet.

### 3. Expose it with Tailscale

```bash
curl -fsSL https://tailscale.com/install.sh | sh
sudo tailscale up
sudo tailscale serve --bg 5080
```

The last command prints an `https://<device>.<tailnet>.ts.net` address with a
real certificate. Install the Tailscale app on your phone, sign in to the same
account, and that address works from anywhere.

Nothing is exposed to the public internet, no ports are forwarded, and only
your own devices can reach it. That also covers the missing authentication,
since access is controlled at the network layer.

### 4. Install on the phone

Open the Tailscale address in Chrome, then Menu and Add to Home screen. Launch
from the icon rather than a browser tab. Play something and lock the screen.

### 5. Keep yt-dlp current

yt-dlp breaks whenever YouTube changes, usually every few weeks. Update it
without rebuilding:

```bash
docker compose exec player pip install --break-system-packages -U yt-dlp
docker compose restart player
```

Worth putting on a monthly cron job.

## If you want a public URL instead

Cloudflare Tunnel is free and gives a public HTTPS address without forwarding
ports, but it needs a domain you control and it puts the app on the open
internet. If you go that way, put Cloudflare Access in front of it so only your
Google or GitHub login gets through. The app itself still has no authentication
and must not be left open.

Tailscale is the better default. Use Cloudflare Tunnel only if you need to
reach the app from a device that cannot run Tailscale.
