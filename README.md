# QBitRemote

QBitRemote is a small, fast Windows desktop companion for a remote qBittorrent server. It gives you a native way to inspect and manage your torrent queue, send local `.torrent` files to the server, and keep common remote actions close at hand without opening a browser.

> QBitRemote controls a qBittorrent server; it is not a standalone BitTorrent client and does not download torrent data on this PC.

## Highlights

- Connect to local, LAN, NAS, Docker, VPS, or other reachable qBittorrent Web UI instances.
- See live transfer speeds, disk free space, progress, localized status labels, categories, and tags.
- Sort and filter the queue; active downloads are shown first by default.
- Select one or many torrents with standard Windows `Ctrl` / `Shift` selection.
- Start, pause, recheck, move, categorize, tag, or delete selected torrents in one action.
- Add local `.torrent` files, choose individual files, a save path, category, tags, and start state.
- Browse useful torrent details: files, peers, trackers, connection information, and transfer statistics.
- Create, edit, and remove server-side qBittorrent categories and their save paths.
- Save successful connections as reusable presets.
- Optional `.torrent` file association during installation or from the application settings.
- English, Hungarian, German, Spanish, French, Italian, and Polish user interfaces.

## Requirements

| Component | Requirement |
| --- | --- |
| Desktop computer | Windows 10 or Windows 11, 64-bit |
| Remote torrent server | qBittorrent with its Web UI enabled |
| Network | The Windows PC must be able to reach the qBittorrent Web UI URL |
| Access | A valid qBittorrent Web UI username and password |

## Supported servers

QBitRemote uses the **qBittorrent Web UI API**. It works with a normal qBittorrent installation, `qbittorrent-nox`, and container/NAS deployments as long as they expose a compatible qBittorrent Web UI API.

It does **not** support Transmission, Deluge, rTorrent, ruTorrent, SABnzbd, or other torrent-server APIs.

For the best experience, use a currently maintained qBittorrent release with the Web UI enabled. qBittorrent documents its Web UI API and remote Web UI workflow in its official project resources.

## Installation

### Installer (recommended)

1. Open this repository's [Releases](../../releases) page.
2. Download `QBitRemote-Setup.msi`.
3. Run the installer and complete the Windows setup wizard. In the feature-selection step, choose the Start menu shortcut, optional desktop shortcut, and optional `.torrent` association.
4. Launch **QBitRemote** from the Start menu.

The association option applies only to the Windows user who runs the installer. It is optional and can be changed later without reinstalling.

### Portable version

1. Download `QBitRemote.exe` from the [Releases](../../releases) page.
2. Save it in a folder you control.
3. Run the executable. No installation is required.

The portable executable still stores its preferences for the current Windows user under `%LocalAppData%\QBitRemote`.

## `.torrent` file association

When using the MSI installer, choose **Associate .torrent files with QBitRemote** in the feature-selection step if you want double-clicking a local `.torrent` file to open QBitRemote's add-torrent dialog.

You can change this later in **Settings**:

1. Open **Settings**.
2. On the **General** tab, enable or disable **Open .torrent files with QBitRemote**.
3. If Windows asks, confirm QBitRemote as the default application for `.torrent` files.

Changing this setting only affects the current Windows user. It does not change any torrent-server setting.

## First-time setup

### 1. Prepare qBittorrent on the server

In qBittorrent, enable the Web UI and note its address, port, username, and password. Typical local examples look like:

```text
http://192.168.1.50:8080
https://torrent.example.net
```

If the server is outside your home network, use HTTPS or a VPN/reverse proxy you trust. Do not expose a password-protected qBittorrent Web UI directly to the public internet without appropriate network protection.

### 2. Connect QBitRemote

1. Launch QBitRemote.
2. Enter the server URL, Web UI username, and password in the connection window.
3. Choose **Save & connect**.
4. After a successful connection, QBitRemote saves the connection as a preset. You can switch presets later from the connection window.

Passwords are protected with Windows DPAPI for the current Windows user. Server URLs and usernames remain local to that Windows profile.

### 3. Add a torrent

Use one of these workflows:

- Open a local `.torrent` file with QBitRemote after enabling the association in **Settings**.

In the add dialog, choose the save path, category, tags, files to download, and whether the torrent should start paused. Category save paths come from the qBittorrent server.

## Everyday use

| Task | How to do it |
| --- | --- |
| View details | Double-click a torrent or use the details icon at the end of its row. |
| Select multiple torrents | Hold `Ctrl` to add/remove individual rows or `Shift` to select a range. |
| Perform bulk actions | Right-click the selection and choose an action. |
| Change a category save path | Open **Settings** → **Torrent defaults** → **Manage server categories**. |
| Change language | Open **Settings** → **General** → **Language**. |
| Change appearance | Open **Settings** → **General** → **Theme**, then choose Windows setting, Light, or Dark. |
| Manage a connection | Use the connection button in the top-right corner. |
| Filter the list | Use the filter button in the top-right corner; an indicator appears when a filter is active. |

## Security and privacy

- QBitRemote communicates only with the qBittorrent Web UI URL you configure.
- Your password is protected using Windows DPAPI and is intended for the current Windows account only.
- Do not share `%LocalAppData%\QBitRemote\settings.json` or application log files: they can contain connection names, URLs, usernames, and diagnostic details.
- Use a strong qBittorrent Web UI password. Prefer HTTPS, a VPN, or a properly secured reverse proxy for remote access.

## Troubleshooting

### Cannot connect

- Confirm that qBittorrent is running and its Web UI is enabled.
- Check the server URL, including the protocol (`http://` or `https://`) and port.
- Verify that the Web UI credentials work in a browser.
- Check firewall, VPN, Docker port mapping, reverse-proxy, and NAS permissions.

### The torrent file association does not open QBitRemote

Open **Settings** and toggle the `.torrent` file association off and on again. Windows can also require confirmation in its Default apps settings. If the installer did not offer the feature-selection step, run the MSI again and choose **Change** or **Modify** to add the optional association feature.

### A category save path is unavailable

Category save paths belong to the server. Ensure the path exists and is writable by the qBittorrent process on the remote host, not by QBitRemote on the Windows PC.

## Support and feedback

Please open a GitHub issue with:

- the qBittorrent version and deployment type (desktop, nox, Docker, NAS, etc.);
- your Windows version;
- steps to reproduce the problem;
- a redacted error message or log excerpt.

Never include passwords, session cookies, private tracker URLs, or personal download paths in an issue.

## Credits

QBitRemote is an independent client for qBittorrent's Web UI API. It is not affiliated with qBittorrent.

## License

QBitRemote is released under the [MIT License](LICENSE).
