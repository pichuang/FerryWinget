# Changelog

本專案遵循 [語意化版本 (Semantic Versioning)](https://semver.org/lang/zh-TW/) 規範。

格式基於 [Keep a Changelog](https://keepachangelog.com/zh-TW/)。

## [1.0.0] - 2026-05-09

### 首次發佈

#### 新增 (Added)

**Downloader CLI**
- 從 microsoft/winget-pkgs GitHub repo 列舉和下載套件 manifest + installer
- Glob pattern allowlist/blocklist 篩選（結構化物件，支援 publishers + packages，blocklist 優先）
- 階層式版本保留策略：Major (3) → Minor (最新 3, 歷史 1) → Patch (2) + 安全機制 (30 天 grace period + pinned tags)
- SemaphoreSlim(16) 並行串流下載 + SHA256 完整性驗證
- 已下載檔案自動跳過 + SHA256 重新驗證
- 架構排除（預設排除 arm64）
- Installer URL FQDN 分析 + HTTP redirect 追蹤
- Azure Firewall Policy 自動部署（TLS + FQDN-only 兩個 rule collection），支援 IP Groups
- `--dry-run` 模式：列舉/篩選/報告但不下載不部署
- 本地快取：套件列舉結果快取 30 分鐘
- 每 100 筆進度更新 + 下載進度條
- 下載失敗逐檔跳過 + 最後統一回報
- Markdown 報告：完整清單 / 差異清單 / FQDN + Firewall Policy 設定

**Server**
- winget REST source API（GET /api/information, POST /api/manifestSearch, GET /api/packageManifests/{id}）
- 支援 Windows 11 / Server 2022 / Server 2025 的 winget CLI
- InstallerUrl 自動改寫為內網 URL
- Installer 二進位檔下載 endpoint
- Web UI 搜尋介面 (index.html)
- OpenTelemetry instrumentation (Traces + Metrics + Logs → OTLP)
- Response Compression (Brotli + Gzip) + Response Caching
- ILogger 結構化日誌

**Core**
- 共用 config.yaml 設定（YamlDotNet + UnderscoredNamingConvention）
- 扁平式 FileSystemPackageStore（{PackageId}/{Version}/ 下放 manifest + installer）
- Asia/Taipei 時區工具
- 離線 NuGet 套件庫（nuget-packages/）

**Container**
- Multi-stage Dockerfile（mcr.microsoft.com/dotnet Azure Linux 3.0）
- 非 root 使用者 + Health check
- podman / docker 支援

**文件**
- README.md
- docs/developer-guide.md — 開發人員手冊
- docs/operations-guide.md — 套件維護人員手冊
- docs/QA.md — 常見問題集
- .github/copilot-instructions.md
- AGENTS.md

**測試**
- 63 個自動化測試（Core 26 + Downloader 27 + Server 10）
- xUnit + FluentAssertions
- WebApplicationFactory 整合測試

**Skills**
- containerize-aspnetcore — ASP.NET Core 容器化
- multi-stage-dockerfile — 多階段 Dockerfile 最佳實踐
- dotnet10-best-practices — .NET 10 最佳實踐審查
- sync-docs — 文件同步 Agent
