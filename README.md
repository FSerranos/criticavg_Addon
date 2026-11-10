# Critic AVG for Jellyfin

*Dozens of critics. One clear score.*

A Jellyfin plugin that writes each film's **Critic AVG score** into its rating. The score is one weighted average of IMDb, TMDB, Trakt, Letterboxd, MUBI, Rotten Tomatoes, Metacritic and SensaCine. Once it's in the rating, you can **sort and filter your library by it** in every Jellyfin app: web, TV, phone.

- Works with **Jellyfin 12** and **Jellyfin 10.11** (one build for each; Jellyfin picks the right one).
- Uses the same weight presets as the Critic AVG app: Balanced, All equal, Critics, Audience, Cinephile, Spain.
- Runs every night at 04:00. Each film is asked again only after 7 days, so your server stays within OMDb's free daily limit.
- If a metadata refresh puts TMDB's rating back, the plugin restores the Critic AVG score on its next run, without asking the server again.

---

## 1 · Update your Critic AVG server

The plugin uses a new address on your server, `/api/score`. Paste **criticavg-worker-v2026-11-10.1.js** (or newer) into Cloudflare (**Edit code** → replace everything → **Deploy**). Check it by opening:

```
https://YOUR-ADDRESS.workers.dev/api/score?tmdb=693134
```

You should see Dune: Part Two with a `"score"`.

## 2 · Build the plugin (free, on GitHub, about 5 minutes)

Jellyfin plugins are compiled with Microsoft's .NET tools. GitHub does that for free:

1. Create a free account at **github.com**, then **New repository**. Name it `criticavg-jellyfin` and choose **Public** (Jellyfin has to be able to read it), then **Create**.
2. Put these files in it: **uploading an existing file**, then drag in everything inside this folder.
   - Check that **`.github/workflows/publish.yml`** is there afterwards. Some computers skip folders whose name starts with a dot. If it's missing: **Add file → Create new file**, type the name `.github/workflows/publish.yml`, paste the file's contents, and **Commit**.
3. Open the **Actions** tab → **Build and publish Critic AVG for Jellyfin** → **Run workflow**. After about 3 minutes both builds show a green tick.
   - If a step shows a red cross, open it and send the error text to Claude. That's where Jellyfin-version differences show up.

The workflow publishes a **release** with both builds and a **repository manifest** at:

```
https://raw.githubusercontent.com/YOUR-GITHUB-NAME/criticavg-jellyfin/manifest/manifest.json
```

## 3 · Install it in Jellyfin

1. **Dashboard → Plugins → Repositories** (on some versions: **Catalog → ⚙ Manage repositories**) → **+**.
   - Name: `Critic AVG`
   - URL: the manifest address above
2. **Catalog → Critic AVG → Install**, then **restart Jellyfin**.
3. **Dashboard → Plugins → Critic AVG**:
   - **Critic AVG server address**: your `workers.dev` address (or your own domain), then **Test connection**
   - **Weights**: a preset
   - **Where to write the score**: *Community rating* (the star, out of 10) is recommended
   - **Save**
4. To see scores straight away: **Dashboard → Scheduled Tasks → Critic AVG → Update Critic AVG scores → ▶**.
5. In your Movies library: **Sort by → Community rating**.

**Manual install (no repository):** download the zip for your Jellyfin version from the GitHub release. Unzip it into a new folder inside Jellyfin's `plugins` folder (for example `plugins/Critic AVG_1.0.0.12`), then restart Jellyfin.

## Good to know

- **Where the score goes:** *Community rating* replaces the star rating, normally TMDB's. *Critic rating* (out of 100) replaces the tomato-style score; Jellyfin may show a fresh/rotten icon next to it.
- **Large libraries:** at most 300 films are fetched per night by default (setting: *Most films to fetch per run*). A library of 1,500 films is fully covered in five nights, then kept fresh.
- **Removing it:** uninstall the plugin, then refresh the library's metadata to get the original ratings back.

## Files

| File | What it is |
|---|---|
| `Jellyfin.Plugin.CriticAvg/Plugin.cs` | The plugin itself and its settings page |
| `Jellyfin.Plugin.CriticAvg/UpdateScoresTask.cs` | The nightly task that fetches and writes scores |
| `Jellyfin.Plugin.CriticAvg/CriticAvgClient.cs` | Calls your Critic AVG server (`/api/score`) |
| `Jellyfin.Plugin.CriticAvg/ScoreStore.cs` | Remembers each film's score between runs (`scores.json` in the plugin's data folder) |
| `Jellyfin.Plugin.CriticAvg/Configuration/configPage.html` | The settings page in the Jellyfin dashboard |
| `.github/workflows/publish.yml` | Builds for Jellyfin 12 and 10.11, publishes the release and the manifest |
| `scripts/package.py`, `scripts/make_manifest.py` | Package the builds and write the repository manifest |

Plugin ID: `96c4c7d6-624e-40a0-86af-108a3c395608`. Never change it, or Jellyfin sees a different plugin.
