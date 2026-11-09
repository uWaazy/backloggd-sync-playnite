# Backloggd Sync (Beta)

Playnite plugin that sends your play sessions, status, rating and platform to your [Backloggd](https://backloggd.com) journal.

> **Unofficial.** Not affiliated with or endorsed by Backloggd.

[Portugues](README.pt-BR.md) | [Espanol](README.es.md)

## What it does

- **Playtime per day:** when a game closes, the session is added to that day in a Log named **Playnite** in the game's journal. Your own Logs are never changed.
- **Status:** Playnite completion status becomes Played, Playing or Backlog (recognized by name in Portuguese, English and Spanish). Only sent when it changes in Playnite.
- **Rating:** the Playnite user score (0-100) becomes a 1-10 rating (half stars). Only sent when it changes.
- **Platform:** PC games (Steam, Epic, Game Pass...) become *Windows PC*; consoles use their own platform. Only filled when the Log has none, so your own choice is kept.
- **Safe by design:** every send writes an exact value for the day, then reads the site back to confirm. Failures stay queued and are retried; sending the same thing twice does not add the time twice.
- **Linking:** games are matched by exact title and release year. When that is not certain, right-click the game > **Backloggd Sync > Link Backloggd game...** and pick the right one; everything pending for that game is sent right after.
- Menus and messages in Portuguese, English and Spanish.

## Install

1. Download the `.pext` from [Releases](../../releases) and open it (or drag it into Playnite).
2. Playnite > **Settings > Extensions > Generic > Backloggd Sync (Beta)**.
3. Click **Sign in to Backloggd**, sign in in the window and close it. You should see *Connected as <your name>*.

Each release lists the SHA-256 of the `.pext`, so you can compare it with the file you downloaded.

## Settings

- Send automatically when a game closes
- Send playtime / status / rating (each one can be turned off)
- **Send pending now** and a live pending counter

Per game (right-click > Backloggd Sync): link the game, send status and rating now, or never send this game.

## How to report problems

Open an [issue](../../issues/new/choose) with the plugin and Playnite versions and what happened. Attach the lines containing `[BLS]` from `%AppData%\Playnite\extensions.log`. They contain game names and dates but no passwords or cookies; review them before sharing.

## License

MIT. See [LICENSE](LICENSE).
