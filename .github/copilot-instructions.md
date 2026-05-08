# Copilot Instructions

## Repository Purpose

**FerryWinget** — 企業內部 winget mirror 系統，包含外網下載器和內網伺服器。從 microsoft/winget-pkgs GitHub repo 下載套件，提供 winget REST source API 給 Windows 11 / Server 2022 / Server 2025 使用。

## Build / Test / Run

```bash
# Build all
dotnet build

# Run all tests (55 tests across 3 projects)
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

```
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
- **GitHubManifestClient** (`Downloader/Services/`) — GitHub Tree API 列舉 winget-pkgs manifests
- **UrlAnalyzer** (`Downloader/Services/`) — 從 InstallerUrl 提取 FQDN + redirect 追蹤
- **FirewallPolicyDeployer** (`Downloader/Services/`) — Azure Firewall Policy 部署 (TLS + FQDN-only rule collections)，支援 --dry-run
- **SearchService** (`Server/Services/`) — winget REST search 邏輯 (Query/Inclusions/Filters)
- **PackageIndexService** (`Server/Services/`) — 本地套件記憶體索引

### Winget REST API Endpoints (Server)

| Endpoint | Method | Description |
|----------|--------|-------------|
| `/api/information` | GET | Server 資訊、支援 API 版本 |
| `/api/manifestSearch` | POST | 套件搜尋 (keyword, filters, inclusions) |
| `/api/packageManifests/{id}` | GET | 完整 manifest (URL rewrite 到內網) |
| `/api/installers/{id}/{ver}/{arch}/{file}` | GET | Installer 二進位下載 |

## Key Conventions

- **時區**: 所有時間戳記使用 `Asia/Taipei` (UTC+8)，透過 `TaipeiTimeHelper`
- **config.yaml**: Server 和 Downloader 共用同一個設定檔，使用 `YamlDotNet` + `UnderscoredNamingConvention`
- **Blocklist 優先**: 篩選邏輯中 blocklist 永遠優先於 allowlist
- **InstallerUrl rewrite**: Server 回傳 manifest 時將外部 URL 改寫為指向內網 `/api/installers/...`
- **Tests 使用 `./tmp_download`**: 測試暫存目錄，每次 setup/teardown 清除，已在 `.gitignore`
- **⚠️ Self-Maintenance Rule**: 當對本專案進行結構性變更時（新增/刪除/重新命名檔案、變更架構、新增功能、修改設定參數），必須同步更新本檔案

## Documentation

- `docs/developer-guide.md` — 開發人員手冊 (環境需求、架構、測試、容器化、新增功能指南)
- `docs/operations-guide.md` — 套件維護人員手冊 (部署、日常維運、套件管理、Firewall、Windows Client 設定)
