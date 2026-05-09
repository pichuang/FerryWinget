# FerryWinget — 常見問題 (Q&A)

## 目錄

- [一般問題](#一般問題)
- [Downloader 下載器](#downloader-下載器)
- [Server 伺服器](#server-伺服器)
- [Windows Client 設定](#windows-client-設定)
- [Azure Firewall Policy](#azure-firewall-policy)
- [套件篩選與版本保留](#套件篩選與版本保留)
- [容器化部署](#容器化部署)
- [效能與維運](#效能與維運)

---

## 一般問題

### Q: FerryWinget 是什麼？

A: 企業內部的 Windows Package Manager (winget) 鏡像系統。它從 [microsoft/winget-pkgs](https://github.com/microsoft/winget-pkgs) 下載套件，在內網提供 winget REST source API，讓 Windows 機器在離線或受限網路環境中使用 `winget install`。

### Q: 需要哪些環境才能執行？

A:
- **.NET 10 SDK** — 建置與執行
- **podman 或 Docker** — 容器化部署 (選用)
- **Azure CLI** — Firewall Policy 自動部署 (選用)
- 外網主機需要能存取 `github.com` 和相關 CDN

### Q: Downloader 和 Server 需要在同一台機器嗎？

A: 不需要。典型部署方式：
1. **外網主機**執行 Downloader 下載套件到 `mirror-data/`
2. 將 `mirror-data/` 目錄搬運到內網（USB、SCP、SMB 等）
3. **內網主機**執行 Server 提供 API

### Q: config.yaml 在哪裡？Server 和 Downloader 共用嗎？

A: 是的，兩者共用同一個 `config.yaml`。預設在專案根目錄，可透過命令列參數指定：
```bash
dotnet run --project src/FerryWinget.Downloader -- /path/to/config.yaml
dotnet run --project src/FerryWinget.Server -- /path/to/config.yaml
```

---

## Downloader 下載器

### Q: `--dry-run` 模式做了什麼？

A: 執行列舉、篩選、manifest 分析、FQDN 報告，但 **不下載 installer 二進位檔**、**不部署 Azure Firewall Policy**。用於預覽哪些套件會被下載。
```bash
dotnet run --project src/FerryWinget.Downloader -- config.yaml --dry-run
```

### Q: 下載很慢，怎麼辦？

A:
1. **設定 GitHub Token** — 未認證 API 限制 60 req/hr，設定 token 後 5,000 req/hr
   ```yaml
   source:
     github_token: "ghp_xxxxxxxxxxxx"
   ```
2. **調整並行數** — 預設 16，可增加（注意 rate limit）
   ```yaml
   downloader:
     max_concurrency: 32
   ```
3. **第二次執行更快** — 套件列舉結果會快取 30 分鐘，已下載的檔案會自動跳過

### Q: 下載逾時怎麼辦？

A: 預設 600 秒（10 分鐘）。大檔案（如 GitHub Desktop ~190MB）需要足夠的時間：
```yaml
downloader:
  download_timeout_seconds: 600   # 調大此值
```

### Q: 為什麼某些檔案下載失敗？

A: 常見原因：
- **逾時** — 檔案太大或網路慢，增加 `download_timeout_seconds`
- **404 Not Found** — 該版本的 release 已被刪除
- **SHA256 不符** — 檔案損壞，會自動重試 3 次
- **Rate limit** — GitHub API 限制，設定 `github_token`

失敗的檔案會被跳過並在最後列出詳細原因，不會中斷整個下載流程。

### Q: 已經下載過的檔案會重複下載嗎？

A: 不會。Downloader 會檢查本地檔案是否存在：
- 有 SHA256 hash → 驗證完整性，一致則跳過，不一致則重新下載
- 無 SHA256 hash → 信任現有檔案，跳過

### Q: 為什麼沒有下載 arm64 版本？

A: 預設排除 `arm64` 架構，可在 config.yaml 修改：
```yaml
downloader:
  excluded_architectures:
    - "arm64"          # 移除此行即可下載 arm64
```

### Q: 報告產生在哪裡？

A: `{storage.root_path}/reports/` 目錄（預設 `./mirror-data/reports/`）：
- `full-package-list.md` — 完整套件清單（預計下載 + 被封鎖 + 略過）
- `diff-report.md` — 差異清單（新增/更新/移除）
- `firewall-fqdns.md` — FQDN 清單 + Azure Firewall Policy 設定
- `firewall-commands.sh` — Firewall 部署腳本（`--dry-run` 時產生）

---

## Server 伺服器

### Q: 如何啟動 Server？

A:
```bash
# 直接執行
dotnet run --project src/FerryWinget.Server -- config.yaml

# 容器方式
podman run -d -p 8080:8080 -v ./mirror-data:/app/mirror-data:Z ferry-winget-server:latest
```

### Q: 如何驗證 Server 是否正常？

A:
```bash
curl http://localhost:8080/api/information
```
應回傳包含 `SourceIdentifier: "FerryWinget"` 的 JSON。

### Q: Server 有 Web UI 嗎？

A: 有。瀏覽器開啟 `http://<server-ip>:8080/` 可使用搜尋和瀏覽介面。

### Q: 新增套件後 Server 搜尋不到？

A: Server 啟動時從 `mirror-data/packages/` 建立記憶體索引。新增套件後需重啟：
```bash
podman restart ferry-winget
```

### Q: Server 支援哪些 winget API 版本？

A: 預設支援 `1.4.0`、`1.7.0`、`1.9.0`，涵蓋 Windows 11 和 Server 2022/2025。

---

## Windows Client 設定

### Q: 如何驗證各 Windows 版本的 winget 相容性？

A: FerryWinget 支援所有具備 winget ≥ 1.4（REST source 支援）的 Windows 版本。

**各平台安裝 winget 方式：**

| OS | winget 來源 | 備註 |
|----|------------|------|
| Windows 11 | 內建 | 直接可用 |
| Windows Server 2025 | 內建 | 直接可用 |
| Windows 10 (1809+) | Microsoft Store / GitHub Release | 需安裝 App Installer ≥ 1.4 |
| Windows Server 2022 | 手動安裝 | 需安裝 VCLibs + UI.Xaml + App Installer |

**Windows Server 2022 手動安裝 winget：**
```powershell
# 安裝相依項
Add-AppxPackage -Path "Microsoft.VCLibs.x64.14.00.Desktop.appx"
Add-AppxPackage -Path "Microsoft.UI.Xaml.2.8.x64.appx"

# 安裝 App Installer (含 winget)
Add-AppxPackage -Path "Microsoft.DesktopAppInstaller_8wekyb3d8bbwe.msixbundle"
```
從 [GitHub Releases](https://github.com/microsoft/winget-cli/releases) 下載。

**驗證步驟（適用所有平台）：**
```powershell
# 1. 確認 winget 版本 (需 ≥ v1.4)
winget --version

# 2. 新增 FerryWinget 為自訂來源
winget source add -n FerryWinget -a http://<SERVER_IP>:8080/api -t "Microsoft.Rest"

# 3. 驗證來源連線
winget source list
winget source update -n FerryWinget

# 4. 搜尋測試
winget search --source FerryWinget --query "GitHub"

# 5. 安裝測試
winget install --source FerryWinget GitHub.cli
```

### Q: 如何新增 FerryWinget 為 winget source？

A: 以系統管理員開啟 PowerShell：
```powershell
winget source add -n "InternalMirror" -a "http://<server-ip>:8080/api/" -t "Microsoft.Rest"
```

### Q: 如何從 FerryWinget 安裝套件？

A:
```powershell
# 搜尋
winget search "GitHub" -s InternalMirror

# 安裝（指定 source）
winget install GitHub.Desktop -s InternalMirror

# 升級
winget upgrade --all -s InternalMirror
```

### Q: 如何移除 FerryWinget source？

A:
```powershell
winget source remove -n "InternalMirror"
```

### Q: 哪些 Windows 版本支援？

| Windows 版本 | 支援狀態 |
|-------------|---------|
| Windows 11 | ✅ winget 內建 |
| Windows Server 2025 | ✅ winget 內建 |
| Windows Server 2022 | ✅ 需安裝 App Installer |
| Windows 10 1809+ | ✅ 需安裝 App Installer |

### Q: 可以用 GPO 大量部署嗎？

A: 可以，用 PowerShell 腳本透過 GPO 或 Intune 派送：
```powershell
winget source remove -n "InternalMirror" 2>$null
winget source add -n "InternalMirror" -a "http://winget-mirror.corp.local:8080/api/" -t "Microsoft.Rest"
```

---

## Azure Firewall Policy

### Q: Firewall Policy 部署是什麼？

A: Downloader 會分析所有 installer URL 的 FQDN（包含 redirect 追蹤到的 CDN），自動建立 Azure Firewall 規則允許這些域名。產生兩個 rule collection：
- **TLS inspection** — 啟用 TLS 深度檢測
- **FQDN-only** — 僅 SNI-based 過濾（不需 TLS inspection）

二擇一使用，視企業安全政策決定。

### Q: 如何只預覽 Firewall 命令而不執行？

A:
```bash
dotnet run --project src/FerryWinget.Downloader -- config.yaml --dry-run
```
會在 `reports/firewall-commands.sh` 產生可執行的腳本。

### Q: 如何使用 IP Groups 而不是 IP 位址？

A: 在 config.yaml 設定 `source_ip_groups`（優先於 `source_addresses`）：
```yaml
firewall:
  source_addresses: []
  source_ip_groups:
    - "ipg-v-a"
    - "ipg-v-b"
```

### Q: Firewall 部署需要什麼權限？

A:
1. `az login` 已完成
2. 對目標 Firewall Policy 有 `Contributor` 或 `Network Contributor` 權限
3. config.yaml 中的 `resource_group` 和 `policy_name` 正確

---

## 套件篩選與版本保留

### Q: Blocklist 和 Allowlist 的優先順序？

A: **Blocklist 永遠最優先**。即使套件同時匹配 allowlist 和 blocklist，仍會被封鎖。

### Q: 如何新增允許的套件？

A: 編輯 `config.yaml`：
```yaml
filtering:
  allowlist:
    enabled: true
    publishers:
      - "Microsoft"
      - "GitHub, Inc."
    packages:
      - "GitHub.*"
      - "Microsoft.VisualStudioCode"
      - "7zip.7zip"
```

`publishers` 和 `packages` 為 OR 聯集關係：符合任一條件即允許同步。

### Q: Blocklist 支援哪些封鎖方式？

A: 兩種：
```yaml
filtering:
  blocklist:
    enabled: true
    publishers:            # 依發行者名稱封鎖
      - "Google"
    packages:              # 依套件識別碼封鎖（支援 glob）
      - "Microsoft.*.Beta"
      - "GitHub.Atom"
```

### Q: 版本保留策略是什麼？

A: 階層式修剪：

| 層級 | 規則 | 預設 |
|------|------|------|
| Major | 最多保留 N 個 | 3 |
| Minor (最新 Major) | 保留最近 N 個 | 3 |
| Minor (歷史 Major) | 只保留最後 1 個 | 1 |
| Patch | 每個 (Major, Minor) 保留最新 N 個 | 2 |
| 安全: 時間豁免 | 發布未滿 N 天一律保留 | 30 天 |
| 安全: 標籤豁免 | prod / latest 標記一律保留 | — |

範例：GitHub.cli 有 25 個版本 (1.2.1~2.0.0) → 保留 `2.0.0`, `1.14.0`, `1.13.1`, `1.12.1` 共 4 個。

### Q: 如何手動刪除特定套件？

A:
```bash
# 刪除特定版本
rm -rf mirror-data/packages/SomePackage.Name/1.0.0/

# 刪除整個套件
rm -rf mirror-data/packages/SomePackage.Name/

# 重啟 Server 重新載入索引
podman restart ferry-winget
```

---

## 容器化部署

### Q: 如何建置容器映像？

A:
```bash
podman build -t ferry-winget-server:latest -f src/FerryWinget.Server/Dockerfile .
```

### Q: 如何使用自訂 config.yaml？

A:
```bash
podman run -d -p 8080:8080 \
  -v ./mirror-data:/app/mirror-data:Z \
  -v ./my-config.yaml:/app/config.yaml:ro \
  ferry-winget-server:latest
```

### Q: 容器使用什麼 base image？

A: `mcr.microsoft.com/dotnet/aspnet:10.0-azurelinux3.0`（Azure Linux 3.0，非 root 使用者）。

### Q: 容器內健康檢查怎麼做？

A: Dockerfile 內建 HEALTHCHECK：
```
curl -f http://localhost:8080/api/information
```
間隔 30 秒，逾時 3 秒，啟動等待 5 秒，重試 3 次。

---

## 效能與維運

### Q: 套件列舉很慢（要等很久）？

A: 首次執行需從 GitHub API 取得完整 manifest tree（約 55 萬筆）。之後會快取 30 分鐘：
```yaml
downloader:
  cache_ttl_minutes: 30    # 調整快取時間，或設為 0 停用
```

### Q: 磁碟空間不足怎麼辦？

A:
1. 減少版本保留數量：
   ```yaml
   retention:
     max_major_versions: 2
     latest_major_minor_count: 2
     patch_count: 1
   ```
2. 檢查佔用空間：
   ```bash
   du -sh mirror-data/packages/* | sort -rh | head -20
   ```

### Q: 如何監控 Server？

A: 內建 OpenTelemetry，設定 OTLP endpoint 即可：
```bash
podman run -d -p 8080:8080 \
  -e OTEL_EXPORTER_OTLP_ENDPOINT="http://otel-collector:4317" \
  -v ./mirror-data:/app/mirror-data:Z \
  ferry-winget-server:latest
```

### Q: 如何完全重建 mirror？

A:
```bash
# 刪除所有套件資料和快取
rm -rf mirror-data/packages mirror-data/.cache

# 重新執行 Downloader
dotnet run --project src/FerryWinget.Downloader -- config.yaml

# 重啟 Server
podman restart ferry-winget
```

### Q: 某個套件有安全漏洞，如何緊急封鎖？

A:
1. 加入 config.yaml blocklist：
   ```yaml
   filtering:
     blocklist:
       packages:
         - "Vulnerable.Package"
   ```
2. 刪除已下載的檔案：`rm -rf mirror-data/packages/Vulnerable.Package/`
3. 重啟 Server：`podman restart ferry-winget`
4. 通知使用者移除該套件
