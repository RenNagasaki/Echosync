[![Discord](https://img.shields.io/badge/Join-Discord-blue)](https://discord.gg/5gesjDfDBr)

# Echosync - Sync the Story, Together

Echosync synchronises FFXIV dialogue progression between players. When you and your friends talk to the same NPC, the dialogue only advances once everyone has clicked — so nobody accidentally skips ahead.

## Features

- **Dialogue sync** — all in-game dialogue boxes (Talk addon) are gated. Clicking advance sends a ready signal to the server; the dialogue only progresses once every player in the sync group has clicked.
- **Ready state indicator** — a small counter (e.g. `1/2`) appears on the Talk addon showing how many players have clicked vs. how many are in the group.
- **Server-authoritative gating** — the server decides when to grant advances. Clients cannot skip ahead on their own.
- **Proximity-based sync groups** — players are grouped by which NPC they are talking to, their dialogue index, and the text hash. Nearby idle players trigger a short catchup window before solo users are allowed through.
- **Advance timeout** — if a player doesn't respond within 30 seconds their advance is auto-granted so nobody is stuck forever.
- **Solo passthrough** — if you start a dialogue alone and no synced players are nearby, dialogue advances normally.
- **Special NPC filter** — optionally restrict syncing to NPCs with a marker above their head (quest givers, etc.).
- **Connect at start** — auto-connect to the sync server when the plugin loads.
- **Password-protected channels** — each channel requires a password so only your group can join.
- **Server bot** — the server can spawn a bot that mirrors a real player's dialogue, useful for solo testing.
- **Localization** — UI supports English, German, French, and Japanese.

## Plugin Setup

1. Install the plugin via the Dalamud plugin installer.
2. Open the config window with `/es` or via the Dalamud plugin list.
3. Enter the **Sync server** address (default: `wss://sync.echotools.cloud`).
4. Enter a **Sync channel** name and **Sync password** — share both with your friends.
5. Click **Connect**.
6. (Optional) Enable **Connect at start** to auto-connect on plugin load.
7. Start a dialogue with any NPC — the plugin handles the rest.

## Commands

| Command | Description |
|---------|-------------|
| `/es` | Opens the configuration window |

## Server

Echosync requires a server to coordinate dialogue sync between players. You can use the public server at `wss://sync.echotools.cloud` or host your own.

### Option 1: Download from Releases

1. Download the latest `echosync-server.zip` from [Releases](https://github.com/RenNagasaki/Echosync/releases).
2. Extract and run:
   ```
   Echosync-Server.exe
   ```
3. The server starts on port **2053**. Make sure this port is accessible to your players (firewall/port forwarding).

### Option 2: Docker

Pull and run the pre-built image from GitHub Container Registry:

```bash
docker run -d -p 2053:2053 --name echosync-server ghcr.io/rennagasaki/echosync-server:latest
```

Or use `docker compose`:

```yaml
services:
  echosync-server:
    image: ghcr.io/rennagasaki/echosync-server:latest
    ports:
      - "2053:2053"
    restart: unless-stopped
```

### Server Commands

When running interactively (`docker run -it` or standalone), the server accepts these commands:

| Command | Description |
|---------|-------------|
| `bot <channel> [delayMs]` | Adds a bot to the specified channel. The bot mirrors a real player's dialogue and auto-advances after the given delay (default 500ms). Useful for solo testing. |
| `botoff` | Removes the active bot. |
| `quit` | Shuts down the server gracefully. |

**Note:** The channel must already exist (a client needs to have connected first). If the channel isn't found, available channels are listed.

### Connecting to Your Own Server

In the plugin config, change the **Sync server** field to your server's address, e.g. `ws://your-server-ip:2053` (use `ws://` for unencrypted or `wss://` for TLS-terminated connections).

## Disclaimer

- This plugin connects to either the public server (`wss://sync.echotools.cloud`) or a server you host yourself. The public server only logs connecting IP addresses — no character or game data is collected.
- This plugin is still in active development. Please report issues on [GitHub](https://github.com/RenNagasaki/Echosync/issues) or on [![Discord](https://img.shields.io/badge/Discord-blue)](https://discord.gg/5gesjDfDBr) (preferred).

## Thanks

- [MidoriKami](https://github.com/MidoriKami) for [KamiToolKit](https://github.com/MidoriKami/KamiToolKit) — the native UI framework that makes Echosync's in-game interface possible. An awesome library for building native FFXIV addon UIs.
- Everyone contributing on the plugin-dev and dalamud-dev channels on the official [Dalamud](https://github.com/goatcorp/Dalamud) discord!
