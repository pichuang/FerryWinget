# FerryWinget 套件維護人員手冊

## 目錄

- [概述](#概述)
- [系統架構](#系統架構)
- [初始部署](#初始部署)
- [日常維運](#日常維運)
- [套件管理](#套件管理)
- [Azure Firewall Policy 管理](#azure-firewall-policy-管理)
- [Server 維運](#server-維運)
- [Windows Client 設定](#windows-client-設定)
- [監控與可觀測性](#監控與可觀測性)
- [報告解讀](#報告解讀)
- [災難復原](#災難復原)
- [常見問題](#常見問題)

---

## 概述

FerryWinget 是企業內部 Windows Package Manager (winget) 鏡像系統，由兩個主要元件組成：

| 元件 | 部署環境 | 用途 |
|------|---------|------|
| **Downloader** | 可連外網的主機 | 從 GitHub 下載套件，產生報告，更新 Firewall 規則 |
| **Server** | 內網伺服器 | 提供 winget REST source API，讓 Windows 機器安裝套件 |

### 運作流程

```
                     ┌─────────────┐
                     │  GitHub     │
                     │  winget-pkgs│
                     └──────┬──────┘
                            │ ① 列舉 + 下載
                            ▼
                   ┌─────────────────┐
                   │  Downloader CLI │ ← config.yaml
                   │  (外網主機)      │
                   └────────┬────────┘
                            │ ② 產生 mirror-data/ + reports/
                            │ ③ 部署 Azure Firewall Policy
                            ▼
                   ┌─────────────────┐
                   │  mirror-data/   │
                   │  ├── packages/  │  (離線搬運)
                   │  ├── installers/│  ──────────┐
                   │  └── reports/   │            │
                   └─────────────────┘            │
                                                  ▼
                   ┌─────────────────┐   ┌─────────────────┐
                   │  Windows Client │──▶│  Server          │ ← config.yaml
                   │  (winget CLI)   │   │  (內網伺服器)     │
                   └─────────────────┘   └─────────────────┘
```

---

## 系統架構

### 檔案系統結構

```
mirror-data/                              # 由 config.yaml storage.root_path 設定
├── packages/                             # manifest YAML 檔案
│   ├── GitHub.Desktop/
│   │   ├── 3.4.0/
│   │   │   └── GitHub.Desktop.yaml       # installer manifest
│   │   └── 3.5.0/
│   │       └── GitHub.Desktop.yaml
│   └── GitHub.CLI/
│       └── 2.40.0/
│           └── GitHub.CLI.yaml
├── installers/                           # installer 二進位檔案
│   ├── GitHub.Desktop/
│   │   ├── 3.4.0/
│   │   │   └── x64/
│   │   │       └── GitHubDesktopSetup-x64.exe
│   │   └── 3.5.0/
│   │       └── x64/
│   │           └── GitHubDesktopSetup-x64.exe
│   └── GitHub.CLI/
│       └── 2.40.0/
│           └── x64/
│               └── gh_2.40.0_windows_amd64.msi
└── reports/                              # Markdown 報告
    ├── full-package-list.md              # 完整套件清單
    ├── diff-report.md                    # 差異清單
    ├── firewall-fqdns.md                 # FQDN 清單
    └── firewall-commands.sh              # Firewall 部署腳本 (dry-run 產生)
```

### 設定檔要點

設定檔 `config.yaml` 由 Downloader 和 Server 共用。維護人員最常調整的區塊：

```yaml
# 套件篩選 — 控制要鏡像哪些套件
filtering:
  allowlist:                    # Glob 模式，空列表 = 全部允許
    - "GitHub.*"                # 允許所有 GitHub 開頭的套件
    - "Microsoft.VisualStudioCode"
    - "7zip.7zip"
  blocklist:                    # Glob 模式，優先權最高
    - "Google.*"                # 封鎖所有 Google 套件
    - "*.Beta"                  # 封鎖 Beta 版套件

# 版本保留策略
retention:
  max_major_versions: 5         # 每個套件保留最新 5 個主要版本
```

---

## 初始部署

### 步驟 1: 準備外網主機

```bash
# 安裝 .NET 10 SDK
# https://dotnet.microsoft.com/download

# 複製專案
git clone <repo-url> && cd ferry-winget

# 編輯設定
cp config.yaml config-production.yaml
vim config-production.yaml        # 調整 allowlist/blocklist、GitHub token 等
```

### 步驟 2: 設定 GitHub Token (建議)

到 GitHub → Settings → Developer settings → Personal access tokens → 建立 token (不需要任何 scope，只需公開 repo 存取)。

```yaml
source:
  github_token: "ghp_xxxxxxxxxxxxxxxxxxxx"
```

> **重要**: 未認證 GitHub API 限制 60 req/hr，認證後 5,000 req/hr。生產環境建議設定 token。

### 步驟 3: 首次下載 (建議使用 dry-run)

```bash
# 先用 dry-run 確認
dotnet run --project src/FerryWinget.Downloader -- config-production.yaml --dry-run

# 檢查報告
cat mirror-data/reports/full-package-list.md

# 確認無誤後正式下載
dotnet run --project src/FerryWinget.Downloader -- config-production.yaml
```

### 步驟 4: 搬運 mirror-data 到內網

將以下目錄搬運到內網 Server 主機：

- `mirror-data/` (完整目錄)
- `config.yaml` (或 `config-production.yaml`)

搬運方式視環境而定：USB、SCP、SMB 共享、Azure File Sync 等。

### 步驟 5: 啟動內網 Server

**方式 A: 直接執行**

```bash
dotnet run --project src/FerryWinget.Server -- config-production.yaml
```

**方式 B: 容器執行 (建議)**

```bash
# 建置容器
podman build -t ferry-winget-server:latest -f src/FerryWinget.Server/Dockerfile .

# 執行
podman run -d --name ferry-winget \
  -p 8080:8080 \
  -v /path/to/mirror-data:/app/mirror-data:Z \
  -v /path/to/config-production.yaml:/app/config.yaml:ro \
  --restart=always \
  ferry-winget-server:latest
```

### 步驟 6: 驗證 Server

```bash
# 測試 API
curl http://<server-ip>:8080/api/information

# 測試搜尋
curl -X POST http://<server-ip>:8080/api/manifestSearch \
  -H "Content-Type: application/json" \
  -d '{"Query":{"KeyWord":"GitHub","MatchType":"Substring"}}'

# 開啟 Web UI
# 瀏覽器開啟 http://<server-ip>:8080/
```

---

## 日常維運

### 定期同步流程

建議每週或每日執行一次同步：

```bash
# 1. 在外網主機執行 Downloader
dotnet run --project src/FerryWinget.Downloader -- config-production.yaml

# 2. 搬運新增/更新的檔案到內網
#    (使用 rsync、robocopy 或其他差異同步工具)
rsync -avz mirror-data/ user@internal-server:/path/to/mirror-data/

# 3. 重啟 Server 以重新載入索引
#    容器方式：
podman restart ferry-winget

#    直接執行方式：重啟應用程式
```

### 自動化建議

可搭配 crontab 或排程工具自動化：

```bash
# /etc/cron.d/ferry-winget-sync (外網主機)
# 每天凌晨 2:00 執行同步
0 2 * * * ferry-user cd /opt/ferry-winget && dotnet run --project src/FerryWinget.Downloader -- config-production.yaml >> /var/log/ferry-winget-sync.log 2>&1
```

### 檢查同步結果

每次 Downloader 執行後產生的報告：

| 檔案 | 內容 |
|------|------|
| `reports/full-package-list.md` | 完整套件清單 (預計下載 + 被封鎖 + 略過) |
| `reports/diff-report.md` | 本次差異 (新增/更新/移除) |
| `reports/firewall-fqdns.md` | 所有需要的 FQDN 清單 |
| `reports/firewall-commands.sh` | Firewall 部署腳本 (dry-run 時產生) |

---

## 套件管理

### 新增允許的套件

編輯 `config.yaml` 的 `filtering.allowlist`：

```yaml
filtering:
  allowlist:
    - "GitHub.*"                          # 既有
    - "Microsoft.VisualStudioCode"        # 新增：VS Code
    - "7zip.7zip"                         # 新增：7-Zip
    - "Notepad++.Notepad++"               # 新增：Notepad++
```

然後重新執行 Downloader。

### 封鎖套件

編輯 `config.yaml` 的 `filtering.blocklist`：

```yaml
filtering:
  blocklist:
    - "Google.*"                          # 封鎖所有 Google 套件
    - "*.Beta"                            # 封鎖所有 Beta 版
    - "SomeDangerous.Package"             # 封鎖特定套件
```

> **⚠️ Blocklist 優先權最高** — 即使套件同時匹配 allowlist 和 blocklist，仍會被封鎖。

### 調整版本保留策略

```yaml
retention:
  max_major_versions: 3     # 改為只保留 3 個主要版本 (節省磁碟空間)
```

### 手動移除特定套件

```bash
# 移除套件的所有版本
rm -rf mirror-data/packages/SomePackage.Name/
rm -rf mirror-data/installers/SomePackage.Name/

# 移除特定版本
rm -rf mirror-data/packages/SomePackage.Name/1.0.0/
rm -rf mirror-data/installers/SomePackage.Name/1.0.0/

# 重啟 Server 重新載入索引
podman restart ferry-winget
```

### 查看套件狀態

```bash
# 列出所有已鏡像的套件
ls mirror-data/packages/

# 列出特定套件的版本
ls mirror-data/packages/GitHub.Desktop/

# 檢查特定 installer 是否存在
ls -la mirror-data/installers/GitHub.Desktop/3.4.0/x64/
```

---

## Azure Firewall Policy 管理

### 概述

Downloader 會分析所有 installer URL 的 FQDN（包含 HTTP redirect 追蹤到的 CDN 域名），並自動部署 Azure Firewall Policy 規則。

產生兩個獨立的 Rule Collection：

| Rule Collection | 用途 | TLS Inspection |
|----------------|------|----------------|
| `rc-winget-tls` | 需要深度封包檢測的環境 | ✅ 啟用 |
| `rc-winget-fqdn` | 僅 SNI-based FQDN 過濾 | ❌ 停用 |

> **二擇一使用** — 視企業的安全政策選擇啟用 TLS inspection 版本或 FQDN-only 版本。不需要同時啟用兩者。

### 設定

```yaml
firewall:
  enabled: true                               # false = 完全停用
  resource_group: "rg-firewall"
  policy_name: "fw-policy-winget"
  rule_collection_group_name: "rcg-winget-mirror"
  tls_rule_collection_name: "rc-winget-tls"
  fqdn_rule_collection_name: "rc-winget-fqdn"
  tls_rule_collection_priority: 500
  fqdn_rule_collection_priority: 501
  source_addresses:
    - "10.0.0.0/8"                            # 調整為實際的來源網段
```

### 前提需求

```bash
# 1. 安裝 Azure CLI
az version

# 2. 登入 Azure
az login

# 3. 確認有 Firewall Policy 寫入權限
az network firewall policy show \
  --resource-group rg-firewall \
  --name fw-policy-winget
```

### Dry-Run 模式 (建議首次使用)

```bash
# 只產生命令，不實際執行
dotnet run --project src/FerryWinget.Downloader -- config-production.yaml --dry-run
```

產生的檔案：

- `reports/firewall-commands.sh` — 可直接執行的 shell 腳本
- `reports/firewall-fqdns.md` — FQDN 清單報告

```bash
# 審查產生的腳本
cat mirror-data/reports/firewall-commands.sh

# 確認無誤後手動執行
bash mirror-data/reports/firewall-commands.sh
```

### 正式部署

```bash
# 不帶 --dry-run，直接部署
dotnet run --project src/FerryWinget.Downloader -- config-production.yaml
```

### 常見 FQDN 範例

以下是常見的 winget 套件下載所需 FQDN：

| FQDN | 用途 |
|------|------|
| `github.com` | GitHub 套件 release 頁面 |
| `objects.githubusercontent.com` | GitHub release 實際下載 CDN |
| `github-releases.githubusercontent.com` | GitHub release 下載 |
| `dl.7-zip.org` | 7-Zip 下載 |
| `notepad-plus-plus.org` | Notepad++ 下載 |

> 實際 FQDN 清單取決於 allowlist 中的套件，每次 Downloader 執行都會重新分析。

---

## Server 維運

### 啟動方式

**容器方式 (建議)**

```bash
podman run -d --name ferry-winget \
  -p 8080:8080 \
  -v /data/mirror-data:/app/mirror-data:Z \
  -v /etc/ferry-winget/config.yaml:/app/config.yaml:ro \
  --restart=always \
  ferry-winget-server:latest
```

**直接執行方式**

```bash
dotnet run --project src/FerryWinget.Server -- /etc/ferry-winget/config.yaml
```

### 健康檢查

```bash
# API 健康檢查
curl -s http://localhost:8080/api/information | jq .

# 預期回應
# {
#   "Data": {
#     "SourceIdentifier": "FerryWinget",
#     "ServerSupportedVersions": ["1.4.0", "1.7.0", "1.9.0"],
#     ...
#   }
# }
```

### 日誌

```bash
# 容器日誌
podman logs ferry-winget
podman logs -f ferry-winget      # 持續追蹤
```

### 更新套件索引

Server 啟動時會自動從 `mirror-data/` 建立記憶體索引。新增套件後需重啟：

```bash
podman restart ferry-winget
```

### Web UI

瀏覽器開啟 `http://<server-ip>:8080/` 可使用 Web UI：

- 搜尋套件
- 查看版本清單
- 查看 winget source add 命令

---

## Windows Client 設定

### 新增 Source

在 Windows 機器上以**系統管理員**開啟 PowerShell：

```powershell
# 新增內部 mirror source
winget source add -n "InternalMirror" -a "http://<server-ip>:8080/api/" -t "Microsoft.Rest"

# 驗證
winget source list
```

### 使用 Mirror

```powershell
# 搜尋套件 (會同時搜尋所有 source)
winget search "GitHub Desktop"

# 指定 source 安裝
winget install GitHub.Desktop -s InternalMirror

# 指定 source 升級
winget upgrade --all -s InternalMirror
```

### 移除 Source

```powershell
winget source remove -n "InternalMirror"
```

### GPO 部署 (大量部署)

可透過 Group Policy 或 Intune 統一設定 winget source：

```powershell
# 用 PowerShell 腳本批次部署
$sourceName = "InternalMirror"
$sourceUrl = "http://winget-mirror.corp.local:8080/api/"

# 移除舊的同名 source
winget source remove -n $sourceName 2>$null

# 新增
winget source add -n $sourceName -a $sourceUrl -t "Microsoft.Rest"
```

### 支援的 Windows 版本

| Windows 版本 | winget 版本 | 支援狀態 |
|-------------|-------------|---------|
| Windows 11 | 內建 | ✅ |
| Windows Server 2025 | 內建 | ✅ |
| Windows Server 2022 | 需安裝 | ✅ (安裝 App Installer) |
| Windows 10 1809+ | 需安裝 | ✅ (安裝 App Installer) |

---

## 監控與可觀測性

### OpenTelemetry

Server 內建 OpenTelemetry instrumentation，輸出 Traces、Metrics、Logs 到 OTLP endpoint。

```bash
# 設定 OTLP endpoint (環境變數)
export OTEL_EXPORTER_OTLP_ENDPOINT="http://otel-collector:4317"

# 或在容器中設定
podman run -d --name ferry-winget \
  -p 8080:8080 \
  -e OTEL_EXPORTER_OTLP_ENDPOINT="http://otel-collector:4317" \
  -v /data/mirror-data:/app/mirror-data:Z \
  ferry-winget-server:latest
```

### 關鍵指標

| 指標 | 來源 | 說明 |
|------|------|------|
| HTTP request duration | ASP.NET Core instrumentation | API 回應時間 |
| HTTP request count | ASP.NET Core instrumentation | 各 endpoint 的請求數 |
| Outbound HTTP calls | HTTP client instrumentation | 外部 HTTP 呼叫 (Downloader) |

### 磁碟空間監控

```bash
# 檢查 mirror-data 大小
du -sh mirror-data/
du -sh mirror-data/packages/
du -sh mirror-data/installers/

# 各套件佔用空間
du -sh mirror-data/installers/* | sort -rh | head -20
```

---

## 報告解讀

### full-package-list.md

完整套件清單報告，分為三個區塊：

| 區塊 | 說明 |
|------|------|
| 預計下載套件 | 通過 allowlist 且未被 blocklist 封鎖的套件，含版本列表 |
| 被封鎖套件 | 匹配 blocklist 的套件，顯示匹配的 pattern |
| 略過套件 | 不在 allowlist 中的套件 (僅計數) |

### diff-report.md

與前次同步的差異：

| 區塊 | 說明 |
|------|------|
| 新增套件 | 新出現在 mirror 中的套件 |
| 更新套件 | 已存在但有新版本的套件 |
| 移除套件 | 不再被鏡像的套件 |

### firewall-fqdns.md

Firewall 需要的所有 FQDN，按頂層域名分組：

```markdown
### github.com
- `github.com`
- `objects.githubusercontent.com`
- `github-releases.githubusercontent.com`
```

也包含 redirect 追蹤結果，顯示哪些 URL 會被重新導向到哪些 CDN。

---

## 災難復原

### Server 故障

1. 重新部署容器：

   ```bash
   podman rm -f ferry-winget
   podman run -d --name ferry-winget \
     -p 8080:8080 \
     -v /data/mirror-data:/app/mirror-data:Z \
     --restart=always \
     ferry-winget-server:latest
   ```

2. 驗證: `curl http://localhost:8080/api/information`

### mirror-data 損毀

1. 從備份還原 `mirror-data/` 目錄
2. 若無備份，在外網主機重新執行 Downloader 完整下載
3. 搬運到內網，重啟 Server

### 完全重建

```bash
# 外網主機
dotnet run --project src/FerryWinget.Downloader -- config-production.yaml

# 搬運到內網
rsync -avz mirror-data/ user@internal-server:/data/mirror-data/

# 重啟 Server
podman restart ferry-winget
```

---

## 常見問題

**Q: 磁碟空間不足**

```
A: 減少 retention.max_major_versions 的數值 (例如從 5 改為 3)
   然後重新執行 Downloader，舊版本會被標記為移除
   手動刪除 mirror-data/installers/ 中多餘的版本目錄
```

**Q: 新增套件後 winget 搜尋不到**

```
A: 1. 確認套件已在 mirror-data/packages/ 中
   2. 重啟 Server (podman restart ferry-winget)
   3. 在 Windows 執行 winget source update
```

**Q: winget install 下載失敗**

```
A: 1. 確認 installer 檔案存在於 mirror-data/installers/ 中
   2. 檢查 Server 日誌: podman logs ferry-winget
   3. 手動測試: curl http://<server>:8080/api/installers/<id>/<ver>/<arch>/<file>
```

**Q: Downloader 執行中斷，部分下載**

```
A: 直接重新執行 Downloader。已下載的檔案會被跳過 (InstallerExistsAsync 檢查)。
   SHA256 驗證確保只有完整正確的檔案被保存。
```

**Q: 某個套件有安全漏洞，需要緊急封鎖**

```
A: 1. 將套件加入 config.yaml 的 blocklist
   2. 手動刪除 mirror-data/ 中對應的檔案
   3. 重啟 Server
   4. 通知使用者移除該套件
```

**Q: Azure Firewall Policy 更新後，下載仍被封鎖**

```
A: 1. 確認 Firewall Policy 已正確套用到 Firewall
   2. 檢查 reports/firewall-fqdns.md 的 FQDN 清單是否完整
   3. 部分 CDN 可能使用動態域名，需手動新增
   4. Azure Firewall 規則套用可能需要 1-2 分鐘
```
