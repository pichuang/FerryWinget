# FerryWinget 開發人員手冊

## 目錄

- [環境需求](#環境需求)
- [快速開始](#快速開始)
- [專案架構](#專案架構)
- [開發流程](#開發流程)
- [設定檔 (config.yaml)](#設定檔-configyaml)
- [測試](#測試)
- [容器化](#容器化)
- [新增功能指南](#新增功能指南)
- [疑難排解](#疑難排解)

---

## 環境需求

| 工具 | 最低版本 | 用途 |
| ------ | --------- | ------ |
| .NET SDK | 10.0 | 編譯和執行 |
| podman / docker | 5.0+ | 容器建置與測試 |
| Azure CLI (`az`) | 2.60+ | Firewall Policy 部署 (選用) |
| Git | 2.30+ | 版本控制 |

確認環境：

```bash
dotnet --version    # 應顯示 10.0.x
podman --version    # 應顯示 5.x+
az version          # 選用，Firewall 部署需要
```

## 快速開始

```bash
# 1. Clone 專案
git clone <repo-url> && cd ferry-winget

# 2. 建置
dotnet build

# 3. 執行全部測試
dotnet test

# 4. 啟動 Server (開發模式)
dotnet run --project src/FerryWinget.Server -- config.yaml

# 5. 執行 Downloader (dry-run 模式)
dotnet run --project src/FerryWinget.Downloader -- config.yaml --dry-run
```

## 專案架構

```text
ferry-winget/
├── src/
│   ├── FerryWinget.Core/               # 共用函式庫
│   │   ├── Configuration/
│   │   │   ├── FerryConfig.cs          # config.yaml POCO model (所有設定區塊)
│   │   │   └── ConfigLoader.cs         # YamlDotNet 載入/序列化
│   │   ├── Models/
│   │   │   ├── PackageManifest.cs      # winget manifest 資料模型
│   │   │   └── FilterResult.cs         # 篩選結果 (planned/blocked/skipped)
│   │   ├── Filtering/
│   │   │   └── PackageFilter.cs        # Glob pattern allowlist/blocklist
│   │   ├── Storage/
│   │   │   ├── IPackageStore.cs        # 儲存介面
│   │   │   └── FileSystemPackageStore.cs
│   │   └── Helpers/
│   │       └── TaipeiTimeHelper.cs     # Asia/Taipei 時區工具
│   │
│   ├── FerryWinget.Downloader/         # 外網下載器 CLI
│   │   ├── Program.cs                  # CLI 進入點 (支援 --dry-run)
│   │   └── Services/
│   │       ├── GitHubManifestClient.cs # GitHub Tree API 列舉 manifest
│   │       ├── VersionRetentionService.cs # 版本保留策略
│   │       ├── InstallerDownloader.cs  # SemaphoreSlim(16) 並行下載
│   │       ├── UrlAnalyzer.cs          # FQDN 分析 + redirect 追蹤
│   │       ├── FirewallPolicyDeployer.cs # Azure Firewall 部署
│   │       └── ReportGenerator.cs      # Markdown 報告
│   │
│   └── FerryWinget.Server/            # 內網 Server
│       ├── Program.cs                  # ASP.NET Core + OpenTelemetry
│       ├── Controllers/
│       │   ├── InformationController.cs     # GET /api/information
│       │   ├── ManifestSearchController.cs  # POST /api/manifestSearch
│       │   └── PackageManifestsController.cs # GET /api/packageManifests/{id}
│       ├── Services/
│       │   ├── PackageIndexService.cs  # 記憶體套件索引
│       │   └── SearchService.cs        # 搜尋邏輯
│       ├── Models/
│       │   └── WingetApiModels.cs      # REST API DTO
│       ├── wwwroot/index.html          # Web UI
│       └── Dockerfile
│
├── tests/                              # 測試 (xUnit + FluentAssertions)
│   ├── FerryWinget.Core.Tests/         # 26 tests
│   ├── FerryWinget.Downloader.Tests/   # 27 tests
│   └── FerryWinget.Server.Tests/       # 10 tests
│
├── config.yaml                         # 共用設定檔
└── .github/copilot-instructions.md
```

### 專案相依關係

```text
Core ← Downloader    (Core 提供 config、models、filtering、storage)
Core ← Server        (Core 提供 config、models、storage)

Core.Tests → Core
Downloader.Tests → Downloader + Core
Server.Tests → Server + Core
```

### 技術堆疊

| 類別 | 技術 |
| ------ | ------ |
| Runtime | .NET 10 (LTS) |
| Web Framework | ASP.NET Core Minimal APIs + Controllers |
| YAML 解析 | YamlDotNet (`UnderscoredNamingConvention`) |
| 可觀測性 | OpenTelemetry (Traces + Metrics + Logs → OTLP) |
| 測試 | xUnit + FluentAssertions |
| 容器 | Azure Linux 3.0 (`mcr.microsoft.com/dotnet`) |

---

## 開發流程

### 元件間的資料流

```text
┌──────────────────────────────────────────────────────────────┐
│ 外網環境 (Downloader CLI)                                     │
│                                                              │
│  config.yaml → GitHubManifestClient (列舉套件)                │
│              → PackageFilter (allowlist/blocklist 篩選)        │
│              → VersionRetentionService (版本保留)              │
│              → InstallerDownloader (並行下載 + SHA256)         │
│              → UrlAnalyzer (FQDN 分析)                        │
│              → ReportGenerator (Markdown 報告)                │
│              → FirewallPolicyDeployer (Azure Firewall)        │
│                                                              │
│  輸出: mirror-data/packages/ + reports/*.md                   │
└──────────────────────────────────────────────────────────────┘
                          │
                    (檔案搬運/同步)
                          │
                          ▼
┌──────────────────────────────────────────────────────────────┐
│ 內網環境 (Server)                                             │
│                                                              │
│  config.yaml → PackageIndexService (建立記憶體索引)            │
│              → SearchService (搜尋)                           │
│              → Controllers (REST API)                        │
│              → InstallerUrl Rewrite (改寫為內網 URL)           │
│                                                              │
│  Windows Client: winget source add → search → install        │
└──────────────────────────────────────────────────────────────┘
```

### 關鍵設計決策

1. **Blocklist 優先** — `PackageFilter` 中 blocklist 永遠優先於 allowlist。即使一個套件同時匹配 allowlist 和 blocklist，仍會被封鎖。

2. **InstallerUrl Rewrite** — Server 在回傳 `/api/packageManifests/{id}` 時，會將原始 `InstallerUrl`（如 `https://github.com/...`）改寫為 `http://<server>/api/installers/{id}/{ver}/{arch}/{filename}`。

3. **SemaphoreSlim(16)** — Downloader 使用 `SemaphoreSlim` 控制最大並行下載數，預設 16，可在 `config.yaml` 調整。

4. **時區** — 所有時間戳記透過 `TaipeiTimeHelper` 輸出 Asia/Taipei (UTC+8)，確保報告和日誌時間一致。

5. **檔案系統儲存** — 使用扁平目錄結構而非資料庫：

   ```text
   mirror-data/
   └── packages/{PackageId}/{Version}/
       ├── {PackageId}.yaml        # manifest
       ├── setup-x64.exe           # installer (扁平放置，無架構子目錄)
       └── setup-x86.msi
   ```

---

## 設定檔 (config.yaml)

Server 和 Downloader 共用同一個設定檔。使用 YamlDotNet 的 `UnderscoredNamingConvention`。

```yaml
# === 資料來源 ===
source:
  github_repo: "microsoft/winget-pkgs"   # winget-pkgs GitHub repo
  github_token: ""                        # GitHub PAT (選填，提高 rate limit)
  manifests_branch: "master"              # 使用的分支

# === 儲存路徑 ===
storage:
  root_path: "./mirror-data"              # 根目錄
  packages_dir: "packages"               # manifest YAML + installer 二進位 (扁平結構)
  reports_dir: "reports"                 # Markdown 報告

# === 套件篩選 ===
filtering:
  allowlist:                              # 結構化允許清單 (publishers + packages OR 聯集)
    enabled: true
    publishers:
      - "Microsoft"
      - "GitHub, Inc."
    packages:
      - "GitHub.*"
  blocklist:                              # 結構化封鎖清單，優先權最高
    enabled: true
    publishers: []
    packages:
      - "Google.*"

# === 版本保留 ===
retention:
  max_major_versions: 5                   # 每個套件保留最新 N 個主要版本

# === 下載器設定 ===
downloader:
  max_concurrency: 16                     # SemaphoreSlim 上限
  download_timeout_seconds: 600           # 單檔下載逾時 (秒)
  retry_count: 3                          # 失敗重試次數
  user_agent: "Mozilla/5.0 ..."           # Edge on Windows 10 browser string
  cache_ttl_minutes: 30                   # 套件列舉快取 TTL (分鐘)
  excluded_architectures:                 # 排除的架構
    - "arm64"

# === Server 設定 ===
server:
  port: 8080                              # 監聽埠
  source_identifier: "FerryWinget"        # winget source 識別名稱
  supported_api_versions:                 # 支援的 API 版本
    - "1.4.0"
    - "1.7.0"
    - "1.9.0"

# === Azure Firewall Policy ===
firewall:
  enabled: true                           # 是否啟用自動部署
  resource_group: "rg-firewall"           # Resource Group
  policy_name: "fw-policy-winget"         # Firewall Policy 名稱
  rule_collection_group_name: "rcg-winget-mirror"
  tls_rule_collection_name: "rc-winget-tls"       # TLS inspection 版本
  fqdn_rule_collection_name: "rc-winget-fqdn"     # FQDN-only 版本
  tls_rule_collection_priority: 500
  fqdn_rule_collection_priority: 501
  source_addresses:                       # 允許的來源 IP/CIDR
    - "10.0.0.0/8"

# === 時區 ===
timezone: "Asia/Taipei"                   # 所有時間戳記使用的時區
```

### Glob 模式語法

| 模式 | 說明 | 範例匹配 |
| ------ | ------ | --------- |
| `GitHub.*` | 匹配以 `GitHub.` 開頭的所有套件 | `GitHub.Desktop`, `GitHub.CLI` |
| `*` | 匹配所有套件 | (全部) |
| `Microsoft.Visual?tudio*` | `?` 匹配單字元 | `Microsoft.VisualStudio`, `Microsoft.VisualStudioCode` |
| 大小寫 | 不區分大小寫 | `github.*` 匹配 `GitHub.Desktop` |

---

## 測試

### 執行測試

```bash
# 全部測試 (63 tests)
dotnet test

# 單一專案
dotnet test tests/FerryWinget.Core.Tests
dotnet test tests/FerryWinget.Downloader.Tests
dotnet test tests/FerryWinget.Server.Tests

# 單一測試方法
dotnet test --filter "FullyQualifiedName~PackageFilterTests.Blocklist_HasHighestPriority"

# 帶詳細輸出
dotnet test --logger "console;verbosity=detailed"
```

### 測試慣例

- **暫存目錄**: 所有需要檔案 I/O 的測試使用 `./tmp_download` 作為暫存路徑
- **Setup/Teardown**: 每個測試類別在建構子中清除暫存目錄，在 `Dispose()` 中再次清除
- **Git 排除**: `tmp_download/` 已加入 `.gitignore`
- **測試資料**: allowlist 使用 `GitHub.*`，blocklist 使用 `Google.*`
- **Server 測試**: 使用 `WebApplicationFactory<Program>` 進行 API 整合測試

### 測試涵蓋範圍

| 測試專案 | 數量 | 涵蓋內容 |
| --------- | ------ | --------- |
| Core.Tests | 26 | PackageFilter (9)、ConfigLoader (4)、FileSystemPackageStore (8)、TaipeiTimeHelper (3)、AllowlistConfig (2) |
| Downloader.Tests | 27 | VersionRetention (6)、UrlAnalyzer (5)、FirewallDeployer (6)、ReportGenerator (3)、GitHubManifestClient (3)、InstallerDownloader (4) |
| Server.Tests | 10 | Information API (1)、ManifestSearch (3)、PackageManifests (4)、Installer Download (2) |

### 新增測試

撰寫新測試時遵循：

```csharp
public class MyServiceTests : IDisposable
{
    private readonly string _testRoot = Path.Combine(".", "tmp_download", "my-test");

    public MyServiceTests()
    {
        if (Directory.Exists(_testRoot))
            Directory.Delete(_testRoot, true);
        Directory.CreateDirectory(_testRoot);
    }

    public void Dispose()
    {
        if (Directory.Exists(_testRoot))
            Directory.Delete(_testRoot, true);
    }

    [Fact]
    public void MyTest()
    {
        // Arrange, Act, Assert with FluentAssertions
        result.Should().Be(expected);
    }
}
```

---

## 容器化

### 建置容器映像

```bash
# 使用 podman
podman build -t ferry-winget-server:latest -f src/FerryWinget.Server/Dockerfile .

# 使用 docker
docker build -t ferry-winget-server:latest -f src/FerryWinget.Server/Dockerfile .
```

### 執行容器

```bash
# 基本執行
podman run -d --name ferry-winget \
  -p 8080:8080 \
  -v ./mirror-data:/app/mirror-data:Z \
  ferry-winget-server:latest

# 使用自訂 config
podman run -d --name ferry-winget \
  -p 8080:8080 \
  -v ./mirror-data:/app/mirror-data:Z \
  -v ./my-config.yaml:/app/config.yaml:ro \
  ferry-winget-server:latest
```

### Dockerfile 說明

- **Build stage**: `mcr.microsoft.com/dotnet/sdk:10.0-azurelinux3.0`
- **Runtime stage**: `mcr.microsoft.com/dotnet/aspnet:10.0-azurelinux3.0`
- **Health check**: `curl -f http://localhost:8080/api/information`
- **非 root 使用者**: 使用 `$APP_UID` (Microsoft base image 預設)
- **Port**: 8080

---

## 新增功能指南

### 新增一個 winget API endpoint

1. 在 `Server/Models/WingetApiModels.cs` 新增 request/response DTO
2. 在 `Server/Controllers/` 新增 Controller
3. 在 `Server.Tests/ServerIntegrationTests.cs` 新增整合測試
4. 更新 `.github/copilot-instructions.md` 中的 API 表格

### 新增篩選邏輯

1. 修改 `Core/Filtering/PackageFilter.cs`
2. 在 `Core.Tests/PackageFilterTests.cs` 新增測試案例
3. 若涉及新設定參數，同步更新 `FerryConfig.cs` 和 `config.yaml`

### 新增 Downloader 步驟

1. 在 `Downloader/Services/` 新增 service 類別
2. 在 `Downloader/Program.cs` 的流程中加入呼叫
3. 在 `Downloader.Tests/` 新增單元測試

### ⚠️ Self-Maintenance Rule

對本專案進行結構性變更時（新增/刪除/重新命名檔案、變更架構、新增功能、修改設定參數），**必須同步更新**：

- `.github/copilot-instructions.md`
- 本文件（若影響開發流程）
- `docs/operations-guide.md`（若影響維運流程）

---

## 疑難排解

### 常見問題

#### Q: GitHub API rate limit 錯誤

```text
A: 在 config.yaml 的 source.github_token 設定 GitHub Personal Access Token (PAT)
   未認證: 60 req/hr，認證: 5,000 req/hr
```

#### Q: Downloader 下載速度慢

```text
A: 調整 config.yaml 的 downloader.max_concurrency (預設 16)
   注意：太高可能觸發 GitHub rate limit
```

#### Q: Server 啟動後 winget 找不到套件

```text
A: 確認 mirror-data/ 目錄中有套件資料 (先執行 Downloader)
   Server 啟動時會從檔案系統建立記憶體索引
```

#### Q: Firewall 部署失敗

```text
A: 1. 確認 az login 已完成
   2. 確認有 Firewall Policy 的寫入權限
   3. 先使用 --dry-run 檢查產生的命令
```

#### Q: Server 測試失敗 — config.yaml not found

```text
A: Server 測試使用 WebApplicationFactory，會自動在記憶體中建立測試 config
   確認 TestServerFixture.cs 的 SeedTestData 方法是否有更新
```

#### Q: 容器內無法讀取 mirror-data

```text
A: 確認 volume mount 權限。podman 使用 :Z 標籤處理 SELinux：
   podman run -v ./mirror-data:/app/mirror-data:Z ...
```
