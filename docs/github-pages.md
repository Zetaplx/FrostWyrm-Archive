# GitHub Pages deployment

Cryptid Companion is published as a standalone static PWA. The intended project URL is:

`https://zetaplx.github.io/FrostWyrm-Archive/`

## Deployment source

`.github/workflows/pages.yml` publishes pushes to **master**, the release branch. It builds and tests the solution, publishes Release assets, reads GitHub's Pages base path, prepares the artifact, runs the browser suite against that actual subdirectory, and deploys only after those checks pass. A failed build/test leaves the previous deployment in place.

The `github-pages` environment must permit deployments from `master`. Development work can continue on `work`; promoting a tested commit to `master` publishes it. Manual deployment is also available through **Actions → Deploy Cryptid Companion to Pages → Run workflow** on `master`.

## One-time repository configuration

A repository administrator enables **Settings → Pages → Build and deployment → Source: GitHub Actions**. If `github-pages` has deployment branch restrictions, allow `master` in **Settings → Environments → github-pages**. No personal access token is stored in this workflow. It uses GitHub's short-lived action token with `pages: write` and `id-token: write` only for deployment.

The deployment publishes the app and its bundled content publicly. Saves remain in each visitor's browser. GitHub Pages availability for private repositories depends on the repository's GitHub plan; the workflow never changes repository visibility.

## Repository paths and offline integrity

After `dotnet publish`, `scripts/prepare_pages.py`:

- Sets the published HTML base URL to GitHub's repository path.
- Creates a `404.html` app shell for direct visits and refreshes on routes such as `settings`, `game`, and `review/...`.
- Creates `.nojekyll`, preserving `_framework` assets.
- Refreshes the offline manifest's index/fallback hashes and version, and verifies every asset hash against actual bytes.
- Removes stale compressed copies of the rewritten HTML/manifest.

PWA caches are isolated by service-worker scope, so another app on the same GitHub Pages origin does not have its cache removed. The worker handles only requests within this app's path. Runtime integrity verification stays enabled.

Direct routes work online through GitHub Pages' custom 404 shell (the initial response has HTTP 404); once the service worker controls the page, cached navigation is served normally. The app remains usable offline after its initial cache completes.

## Test the Pages artifact locally

```sh
dotnet publish apps/CryptidCompanion -c Release -o artifacts/pages
python3 scripts/prepare_pages.py artifacts/pages/wwwroot --base-path /FrostWyrm-Archive/
python3 scripts/serve.py artifacts/pages/wwwroot --port 5013 --base-path /FrostWyrm-Archive/ --pages
```

In another terminal, after installing the browser-test dependencies:

```sh
CRYPTID_BASE_URL=http://127.0.0.1:5013/FrostWyrm-Archive/ CRYPTID_PUBLISHED=1 CRYPTID_PAGES=1 npm test --prefix tests/browser
```

This tests actual repository-prefixed resources, direct route loading from the 404 shell, PWA start URL resolution, game play, settings, saved reviews, and offline reload.

## Saves and updates

The hosted origin has separate browser storage from a local development address. Existing local saves do not transfer automatically. Installed app updates wait until older tabs/windows close; a deployment does not force an active expedition to reload.
