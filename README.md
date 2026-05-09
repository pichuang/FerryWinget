# 🚢 FerryWinget

企業內部 Windows Package Manager (winget) 鏡像系統。從 [microsoft/winget-pkgs](https://github.com/microsoft/winget-pkgs) 下載套件，在內網提供完整的 winget REST source API。

## 功能

- **外網下載器 (Downloader CLI)** — 從 GitHub 下載 manifest + installer 二進位檔
  - Glob pattern allowlist / blocklist 篩選（blocklist 優先權最高，支援 publisher 和 package 層級）
  - 階層式版本保留：Major (3) → Minor (最新 Major 留 3, 歷史留 1) → Patch (每組留 2) + 安全機制 (grace period + pinned tags)
  - `SemaphoreSlim(16)` 並行下載 + SHA256 驗證 + 串流下載
  - 架構排除：預設排除 arm64
  - Installer URL FQDN 分析（含 HTTP redirect 追蹤）
  - Azure Firewall Policy 自動部署（TLS + FQDN-only），支援 IP Groups + `--dry-run`
  - 本地快取：套件列舉結果快取 30 分鐘
  - Markdown 報告（完整清單 / 差異清單 / FQDN 清單 + Firewall Policy 設定）

- **內網伺服器 (Server)** — winget REST source API (ASP.NET Core)
  - 支援 Windows 11 / Server 2022 / Server 2025 的 `winget` CLI
  - `InstallerUrl` 自動改寫為指向內網
  - Web UI 搜尋與下載介面
  - OpenTelemetry instrumentation (Traces / Metrics / Logs)
  - Response Compression (Brotli + Gzip) + Response Caching
  - 容器化部署（Azure Linux 3.0）

## 快速開始

### 環境需求

- [.NET 10 SDK](https://dotnet.microsoft.com/download)
- [podman](https://podman.io/) 或 Docker（容器部署）
- [Azure CLI](https://learn.microsoft.com/cli/azure/)（Firewall 部署，選用）

### 建置與測試

```bash
dotnet build
dotnet test       # 62 tests across 3 projects
```

### 執行 Downloader

```bash
# 編輯設定
vim config.yaml

# Dry-run（不實際部署 Firewall）
dotnet run --project src/FerryWinget.Downloader -- config.yaml --dry-run

# 正式執行
dotnet run --project src/FerryWinget.Downloader -- config.yaml
```

### 啟動 Server

```bash
# 直接執行
dotnet run --project src/FerryWinget.Server -- config.yaml

# 容器方式
podman build -t ferry-winget-server:latest -f src/FerryWinget.Server/Dockerfile .
podman run -d -p 8080:8080 \
  -v ./mirror-data:/app/mirror-data:Z \
  ferry-winget-server:latest
```

### Windows Client 設定

```powershell
winget source add -n "InternalMirror" -a "http://<server-ip>:8080/api/" -t "Microsoft.Rest"
winget search "GitHub Desktop" -s InternalMirror
winget install GitHub.Desktop -s InternalMirror
```

## 設定檔 (config.yaml)

Server 和 Downloader 共用同一份設定檔：

```yaml
source:
  github_repo: "microsoft/winget-pkgs"
  github_token: ""                        # 選填，提高 GitHub API rate limit

filtering:
  allowlist:
    - "GitHub.*"                          # Glob pattern
  blocklist:
    enabled: true
    publishers: []                        # 依發行者封鎖
    packages:                             # 依套件識別碼封鎖，優先權最高
      - "Google.*"

retention:
  max_major_versions: 3
  latest_major_minor_count: 3
  older_major_minor_count: 1
  patch_count: 2
  grace_period_days: 30

server:
  port: 8080
  source_identifier: "FerryWinget"

firewall:
  enabled: true
  resource_group: "rg-firewall"
  policy_name: "fw-policy-winget"
  source_ip_groups: ["ipg-v-a", "ipg-v-b"]
```

完整設定說明參見 [開發人員手冊](docs/developer-guide.md#設定檔-configyaml)。

## API Endpoints

| Endpoint | Method | Description |
|----------|--------|-------------|
| `/api/information` | GET | Server 資訊、支援 API 版本 |
| `/api/manifestSearch` | POST | 套件搜尋 (keyword, filters, inclusions) |
| `/api/packageManifests/{id}` | GET | 完整 manifest (URL rewrite 到內網) |
| `/api/installers/{id}/{ver}/{arch}/{file}` | GET | Installer 二進位下載 |
| `/` | GET | Web UI 搜尋介面 |

## 專案結構

```text
src/
  FerryWinget.Core/             → 共用函式庫 (config, models, filtering, storage)
  FerryWinget.Downloader/       → 外網下載器 CLI
  FerryWinget.Server/           → 內網 winget REST source server
tests/
  FerryWinget.Core.Tests/       → 25 tests
  FerryWinget.Downloader.Tests/ → 27 tests
  FerryWinget.Server.Tests/     → 10 tests
config.yaml                     → 共用設定檔
docs/
  developer-guide.md            → 開發人員手冊
  operations-guide.md           → 套件維護人員手冊
```

## 文件

| 文件 | 對象 |
|------|------|
| [開發人員手冊](docs/developer-guide.md) | 架構、測試、容器化、新增功能指南 |
| [套件維護人員手冊](docs/operations-guide.md) | 部署、日常維運、套件管理、Firewall、Windows Client |

## 技術堆疊

.NET 10 · ASP.NET Core · YamlDotNet · OpenTelemetry · xUnit + FluentAssertions · Azure Linux 3.0

## License

MIT
