# Copilot Instructions

## Repository Purpose

**FerryWinget** — 企業內部 winget mirror 系統，包含外網下載器和內網伺服器。從 microsoft/winget-pkgs GitHub repo 下載套件，提供 winget REST source API 給 Windows 11 / Server 2022 / Server 2025 使用。

## Build / Test / Run

```bash
# Build all
dotnet build

# Run all tests (61 tests across 3 projects)
dotnet test

# Run single test project
dotnet test tests/FerryWinget.Core.Tests
dotnet test tests/FerryWinget.Downloader.Tests
dotnet test tests/FerryWinget.Server.Tests

# Run a single test
dotnet test --filter "FullyQualifiedName~PackageFilterTests.Blocklist_HasHighestPriority"

# Run downloader
dotnet run --project src/FerryWinget.Downloader -- config.yaml
dotnet run --project src/FerryWinget.Downloader -- config.yaml --dry-run

# Run server
dotnet run --project src/FerryWinget.Server -- config.yaml

# Container build & run (podman)
podman build -t ferry-winget-server:latest -f src/FerryWinget.Server/Dockerfile .
podman run -p 8080:8080 -v ./mirror-data:/app/mirror-data ferry-winget-server:latest
```

## Architecture

```text
src/
  FerryWinget.Core/           → 共用函式庫 (config, models, filtering, storage)
  FerryWinget.Downloader/     → 外網下載器 CLI
  FerryWinget.Server/         → 內網 winget REST source server (ASP.NET Core)
tests/
  FerryWinget.Core.Tests/
  FerryWinget.Downloader.Tests/
  FerryWinget.Server.Tests/
config.yaml                   → 共用設定檔
```

**Tech Stack**: .NET 10, ASP.NET Core, YamlDotNet, OpenTelemetry, xUnit + FluentAssertions

### Key Components

- **PackageFilter** (`Core/Filtering/`) — Glob pattern allowlist/blocklist, blocklist 優先權最高
- **FileSystemPackageStore** (`Core/Storage/`) — 本地檔案系統套件儲存
- **GitHubManifestClient** (`Downloader/Services/`) — GitHub Tree API 列舉 winget-pkgs manifests，支援本地快取 (TTL 30min)
- **UrlAnalyzer** (`Downloader/Services/`) — 從 InstallerUrl 提取 FQDN + redirect 追蹤
- **FirewallPolicyDeployer** (`Downloader/Services/`) — Azure Firewall Policy 部署 (TLS + FQDN-only rule collections)，支援 --dry-run
- **VersionRetentionService** (`Downloader/Services/`) — 階層式版本保留策略 (Major→Minor→Patch) + 安全機制 (grace period + pinned tags)
- **SearchService** (`Server/Services/`) — winget REST search 邏輯 (Query/Inclusions/Filters)
- **PackageIndexService** (`Server/Services/`) — 本地套件記憶體索引

### Winget REST API Endpoints (Server)

| Endpoint                                   | Method | Description                                 |
| ------------------------------------------ | ------ | ------------------------------------------- |
| `/api/information`                         | GET    | Server 資訊、支援 API 版本                  |
| `/api/manifestSearch`                      | POST   | 套件搜尋 (keyword, filters, inclusions)     |
| `/api/packageManifests/{id}`               | GET    | 完整 manifest (URL rewrite 到內網)          |
| `/api/installers/{id}/{ver}/{arch}/{file}` | GET    | Installer 二進位下載                        |

## Key Conventions

- **時區**: 所有時間戳記使用 `Asia/Taipei` (UTC+8)，透過 `TaipeiTimeHelper`
- **config.yaml**: Server 和 Downloader 共用同一個設定檔，使用 `YamlDotNet` + `UnderscoredNamingConvention`
- **Blocklist 優先**: 篩選邏輯中 blocklist 永遠優先於 allowlist
- **Blocklist 結構**: `blocklist` 是結構化物件，含 `enabled`、`publishers`（依發行者名稱封鎖）、`packages`（依套件識別碼封鎖，支援 glob）
- **版本保留策略**: 階層式修剪 — Major (最多 3) → Minor (最新 Major 留 3, 歷史留 1) → Patch (每組留最新 2)，加上安全機制 (30 天 grace period + pinned tags)
- **InstallerUrl rewrite**: Server 回傳 manifest 時將外部 URL 改寫為指向內網 `/api/installers/...`
- **架構排除**: Downloader 預設排除 `arm64` 架構的 installer，可透過 `downloader.excluded_architectures` 設定
- **本地快取**: GitHub 套件列舉結果快取於 `{storage.root_path}/.cache/package-list-cache.json`，TTL 預設 30 分鐘
- **Tests 使用 `./tmp_download`**: 測試暫存目錄，每次 setup/teardown 清除，已在 `.gitignore`
- **⚠️ Self-Maintenance Rule**: 當對本專案進行結構性變更時（新增/刪除/重新命名檔案、變更架構、新增功能、修改設定參數），必須同步更新本檔案

## Documentation

- `docs/developer-guide.md` — 開發人員手冊 (環境需求、架構、測試、容器化、新增功能指南)
- `docs/operations-guide.md` — 套件維護人員手冊 (部署、日常維運、套件管理、Firewall、Windows Client 設定)
