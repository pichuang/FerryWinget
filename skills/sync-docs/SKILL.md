---
name: sync-docs
description: 'Audit and synchronize all project documentation after code changes. Ensures README, copilot-instructions, AGENTS.md, developer guide, and operations guide stay accurate and consistent.'
---

# FerryWinget 文件同步 Agent

## 目標

掃描程式碼目前狀態，與所有文件檔案比對，找出過時或遺漏的內容並直接修正。確保所有文件在結構性變更後保持一致。

## 需要同步的文件清單

| 檔案 | 對象 | 重點內容 |
|------|------|---------|
| `README.md` | 所有人 | 功能總覽、快速開始、API 表、專案結構、技術堆疊 |
| `.github/copilot-instructions.md` | GitHub Copilot | Build/test 指令、架構、元件清單、慣例、API 表 |
| `AGENTS.md` | AI Coding Agents | 設計決策、設定檔說明、測試慣例、儲存結構 |
| `docs/developer-guide.md` | 開發人員 | 環境需求、架構圖、資料流、config.yaml 完整說明、測試涵蓋 |
| `docs/operations-guide.md` | 維運人員 | 部署步驟、日常維運、套件管理、Firewall、Windows Client |

## 執行流程

### Step 1: 收集程式碼現狀

從程式碼中提取以下事實 (source of truth)：

1. **測試數量** — 執行 `dotnet test` 計算 Core / Downloader / Server 各有幾個 test
2. **config.yaml 結構** — 讀取 `FerryConfig.cs` 所有 config class 的屬性和預設值
3. **API Endpoints** — 掃描 `Server/Controllers/` 的所有 `[HttpGet]` / `[HttpPost]` 屬性
4. **Key Components** — 掃描 `src/` 下所有 `.cs` 檔案的 class 名稱和 XML summary
5. **專案結構** — 列出 `src/` 和 `tests/` 下所有 `.csproj` 檔案
6. **NuGet 套件** — 從 `.csproj` 提取所有 PackageReference
7. **Dockerfile** — 讀取 base image 和 port 設定
8. **儲存結構** — 從 `FileSystemPackageStore.cs` 提取目錄布局
9. **CLI 參數** — 從 `Program.cs` 提取 Downloader 和 Server 的命令列參數
10. **版本保留策略** — 從 `VersionRetentionService.cs` 和 `RetentionConfig` 提取規則

### Step 2: 逐檔比對與更新

對每個文件檔案：

1. 讀取檔案內容
2. 比對 Step 1 收集的事實
3. 找出以下差異類型：
   - **數字過時** — 測試數量、套件數量、API 版本等
   - **元件遺漏** — 新增的 service/controller 沒列在元件清單
   - **設定遺漏** — config.yaml 新增的參數沒出現在說明中
   - **結構過時** — 儲存目錄結構、專案結構已變更
   - **慣例過時** — 新的開發慣例沒記錄
   - **指令過時** — build/run 指令已變更
4. 直接修正差異（不只是報告）

### Step 3: 交叉一致性檢查

確保以下資訊在所有文件中一致：

| 資訊 | 出現的文件 |
|------|-----------|
| 測試總數 | README, copilot-instructions, developer-guide |
| API Endpoints 表 | README, copilot-instructions, developer-guide |
| 技術堆疊 | README, copilot-instructions, developer-guide |
| config.yaml 設定參數 | copilot-instructions, AGENTS.md, developer-guide, operations-guide |
| 儲存結構 | AGENTS.md, developer-guide, operations-guide |
| CLI 使用方式 | README, copilot-instructions, developer-guide, operations-guide |
| 版本保留策略 | AGENTS.md, developer-guide, operations-guide |
| Firewall 設定 | AGENTS.md, developer-guide, operations-guide |
| Self-Maintenance Rule | copilot-instructions, AGENTS.md |

### Step 4: 驗證與報告

1. 確認所有修改的文件語法正確（Markdown 格式）
2. 執行 `dotnet build` 和 `dotnet test` 確認程式碼沒被意外修改
3. 輸出同步摘要：
   ```
   文件同步完成:
   - README.md: 更新測試數量 (61 → 65), 新增 UrlAnalyzer 說明
   - copilot-instructions.md: 新增 IP Groups 慣例
   - AGENTS.md: 更新儲存結構
   - developer-guide.md: 無變更
   - operations-guide.md: 更新 Firewall source_ip_groups 說明
   ```

## 比對規則

### 必須精確匹配的欄位
- 測試數量（從 `dotnet test` 輸出計算）
- API endpoints（從 controller attributes 提取）
- config.yaml 參數名稱和預設值（從 `FerryConfig.cs` 提取）

### 允許概略描述的欄位
- 元件功能描述（只要沒有事實錯誤即可）
- 架構圖（只要反映正確的專案結構即可）

### 不修改的內容
- 使用者自訂的補充說明或範例
- 不屬於 FerryWinget 的外部參考連結

## Scope

- ✅ 修正過時的數字、名稱、結構描述
- ✅ 新增遺漏的元件、設定、慣例
- ✅ 移除已不存在的元件或功能的描述
- ✅ 確保所有文件交叉一致
- ❌ 不修改程式碼
- ❌ 不新增文件檔案（只更新現有的）
- ❌ 不改變文件的寫作風格或語言

## Progress Tracking

```markdown
# 文件同步進度

## 程式碼現狀收集
- [ ] 測試數量統計
- [ ] Config 結構提取
- [ ] API Endpoints 掃描
- [ ] 元件清單掃描
- [ ] 專案結構確認

## 文件更新
- [ ] README.md
- [ ] .github/copilot-instructions.md
- [ ] AGENTS.md
- [ ] docs/developer-guide.md
- [ ] docs/operations-guide.md

## 驗證
- [ ] 交叉一致性檢查通過
- [ ] dotnet build 成功
- [ ] dotnet test 通過
```

**YOU ARE NOT DONE UNTIL ALL CHECKBOXES ARE MARKED!**
