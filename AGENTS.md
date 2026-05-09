# AGENTS.md

> 本檔案供 AI Coding Agents (Codex, Jules, OpenCode 等) 使用，提供專案上下文與開發規範。

## 專案概述

**FerryWinget** — 企業內部 Windows Package Manager (winget) 鏡像系統。

- **外網下載器** (`src/FerryWinget.Downloader/`) — 從 `microsoft/winget-pkgs` GitHub repo 下載套件
- **內網伺服器** (`src/FerryWinget.Server/`) — 提供 winget REST source API (ASP.NET Core)
- **共用核心** (`src/FerryWinget.Core/`) — 設定、模型、篩選、儲存

## 建置與測試

```bash
dotnet build                    # 建置全部
dotnet test                     # 執行全部 61 個測試
dotnet test --filter "FullyQualifiedName~PackageFilterTests.Blocklist_HasHighestPriority"  # 單一測試
```

## 技術堆疊

- .NET 10 (LTS), C#
- ASP.NET Core (Server)
- YamlDotNet (`UnderscoredNamingConvention`)
- OpenTelemetry (Traces + Metrics + Logs)
- xUnit + FluentAssertions
- 容器: `mcr.microsoft.com/dotnet` Azure Linux 3.0

## 關鍵設計決策

### 篩選邏輯 (`PackageFilter`)

- **Blocklist 永遠優先於 Allowlist** — 即使套件同時匹配兩者，仍會被封鎖
- Blocklist 是結構化物件 (`BlocklistConfig`)，含 `enabled`、`publishers`（依發行者名稱封鎖）、`packages`（依套件識別碼封鎖）
- Allowlist 和 Blocklist 都使用 glob pattern（`*` 任意字元, `?` 單字元, 大小寫不敏感）

### 版本保留策略 (`VersionRetentionService`)

階層式版本修剪 + 安全機制：

| 層級                | 規則                                      | 預設值                 |
| ------------------- | ----------------------------------------- | ---------------------- |
| 安全: 時間豁免      | 發布未滿 N 天的版本一律保留            | 30 天                  |
| 安全: 標籤豁免      | 被標記 `prod` / `latest` 的版本一律保留 | `["prod", "latest"]`   |
| Major               | 最多保留 N 個主版號                      | 3                      |
| Minor (最新 Major)  | 保留最近 N 個次版號                    | 3                      |
| Minor (歷史 Major)  | 只保留最後 1 個次版號                  | 1                      |
| Patch               | 每個 (Major, Minor) 保留最新 N 個修訂     | 2                      |

### InstallerUrl Rewrite

Server 回傳 `/api/packageManifests/{id}` 時，將原始 `InstallerUrl` 改寫為 `http://<server>/api/installers/{id}/{ver}/{arch}/{filename}`。

### 架構排除

Downloader 預設排除 `arm64` 架構的 installer，可透過 `config.yaml` 的 `downloader.excluded_architectures` 設定。可用值: `x86`, `x64`, `arm`, `arm64`, `neutral`。

### 本地快取

GitHub 套件列舉結果快取於 `{storage.root_path}/.cache/package-list-cache.json`，TTL 預設 30 分鐘，避免重複 API 呼叫。

### Azure Firewall Policy

Downloader 分析所有 installer URL 的 FQDN（含 redirect 追蹤），自動部署兩個獨立 rule collection：

- **TLS inspection** (`rc-winget-tls`) — `enable-tls-inspection true`
- **FQDN-only** (`rc-winget-fqdn`) — `enable-tls-inspection false`
- 支援 `--dry-run` 模式（只輸出命令，不執行）

## 設定檔

`config.yaml` 由 Server 和 Downloader 共用。使用 `YamlDotNet` 的 `UnderscoredNamingConvention`（YAML key 為 `snake_case`，C# 屬性為 `PascalCase`）。

對應的 POCO model: `src/FerryWinget.Core/Configuration/FerryConfig.cs`

## 測試慣例

- 測試框架: xUnit + FluentAssertions
- 暫存目錄: `./tmp_download`，每次測試 setup/teardown 清除
- Server 測試: `WebApplicationFactory<Program>` 整合測試
- 測試資料: allowlist 使用 `GitHub.*`，blocklist 使用 `Google.*`

## 檔案系統儲存結構

```text
mirror-data/
├── packages/
│   └── {PackageId}/
│       └── {Version}/
│           ├── {PackageId}.yaml        # manifest
│           ├── setup-x64.exe           # installer (扁平放置，無架構子目錄)
│           └── setup-x86.msi
├── reports/
│   ├── full-package-list.md
│   ├── diff-report.md
│   ├── firewall-fqdns.md
│   └── firewall-commands.sh
└── .cache/package-list-cache.json
```

## Self-Maintenance Rule

⚠️ 對本專案進行結構性變更時（新增/刪除/重新命名檔案、變更架構、新增功能、修改設定參數），**必須同步更新**：

1. `.github/copilot-instructions.md`
2. `AGENTS.md`（本檔案）
3. `docs/developer-guide.md`（若影響開發流程）
4. `docs/operations-guide.md`（若影響維運流程）

## 時區

所有時間戳記使用 `Asia/Taipei` (UTC+8)，透過 `TaipeiTimeHelper` 類別。
