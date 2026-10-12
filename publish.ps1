<#
.SYNOPSIS
    环形器自动测试上位机软件发布脚本
.DESCRIPTION
    自动叠加版本号 + 编译发布 + 打 zip 包
    不传参数时自动从 .csproj 读取当前版本号，叠加修订号（1.0.0 -> 1.0.1）
.PARAMETER Version
    手动指定目标版本号（3 段式，如 1.1.0），优先级最高
.PARAMETER Major
    叠加主版本号（1.0.0 -> 2.0.0）
.PARAMETER Minor
    叠加次版本号（1.0.0 -> 1.1.0）
.PARAMETER Patch
    叠加修订号（1.0.0 -> 1.0.1），默认
.PARAMETER Runtime
    运行时标识，默认 win-x64
.PARAMETER SelfContained
    是否自包含发布（包含 .NET 运行时），默认 false
.PARAMETER AppcastOnly
    不编译、不改版本号，只用现有 zip 重新生成 appcast.xml（可配合发布，用于修复”文件完整性检查失败”）
.PARAMETER UpdateUrlBase
    更新包在共享上的访问前缀，默认指向 192.168.0.150 的更新目录；打包后产物也发布到这里
.PARAMETER AppcastPath
    本地 appcast.xml 输出路径，默认与 zip 同目录（脚本根目录）
.PARAMETER Mandatory
    生成强制更新标记（<mandatory>true</mandatory>），默认 false
.PARAMETER NoDeploy
    只在本机生成 zip 与 appcast.xml，不拷贝到共享 update 目录（默认会自动发布）
.PARAMETER ForceDeploy
    共享上已存在同版本号但内容不同的 zip 时，强制覆盖（默认拒绝，防止旧清单校验失败）
.PARAMETER NoPlugins
    跳过插件组装（默认会按解决方案编译插件并放入 Plugins\，缺失则发布中止）
.EXAMPLE
    .\publish.ps1
    自动叠加修订号（1.0.0 -> 1.0.1）并发布到共享 update 目录（zip + appcast.xml 一步到位）
.EXAMPLE
    .\publish.ps1 -AppcastOnly
    不重新编译，自动选目录里最新版本包，重建正确 appcast.xml 并发布到共享
.EXAMPLE
    .\publish.ps1 -NoDeploy
    仅本机产物，不动共享
.EXAMPLE
    .\publish.ps1 -Minor
    叠加次版本号（1.0.0 -> 1.1.0）
.EXAMPLE
    .\publish.ps1 -Version 1.5.0
    手动指定版本号为 1.5.0
#>

param(
    [string]$Version,
    [switch]$Major,
    [switch]$Minor,
    [switch]$Patch,
    [string]$Runtime = "win-x64",
    [switch]$SelfContained,
    [switch]$AppcastOnly,
    [string]$UpdateUrlBase = '\\192.168.0.150\研发部文件共享\软件组\SoftwareRepository\环形器自动测试\update',
    [string]$AppcastPath,
    [switch]$Mandatory,
    [switch]$NoDeploy,
    [switch]$ForceDeploy,
    [switch]$NoPlugins
)

$ErrorActionPreference = "Stop"
$ScriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$CsprojPath = Join-Path $ScriptDir "MultiOpticalSwitchTestApp.csproj"
$PublishDir = Join-Path $ScriptDir "publish"
$ProductName = "MultiOpticalSwitchTestApp"
$SlnxPath = Join-Path $ScriptDir "MultiOpticalSwitchTestApp.slnx"
$RepositoryRoot = Split-Path -Parent $ScriptDir

# ============================================================
# 0. appcast 生成函数：SHA256 由脚本计算，禁止手填
#    AutoUpdater.NET 读取的清单格式：
#    <item><version>..</version><url>..</url><mandatory>..</mandatory><checksum algorithm="SHA256">..</checksum></item>
#    url 需为 file:// 形式的 UNC 绝对地址（与现有 appcast.xml 保持一致）
# ============================================================
function Write-Appcast {
    param(
        [Parameter(Mandatory)][string]$ZipPath,
        [Parameter(Mandatory)][string]$Version,
        [Parameter(Mandatory)][string]$UpdateUrlBase,
        [Parameter(Mandatory)][string]$OutPath,
        [bool]$Mandatory = $false
    )

    if (-not (Test-Path -LiteralPath $ZipPath)) {
        Write-Host "错误：找不到更新包 $ZipPath" -ForegroundColor Red
        exit 1
    }

    # 1. 用实际文件算哈希，杜绝“包换了、哈希没换”
    $sha = (Get-FileHash -LiteralPath $ZipPath -Algorithm SHA256).Hash

    # 2. 拼 file:// 下载地址：\\server\share\a.zip -> file://server/share/a.zip
    #    中文段保留原样（现有 appcast.xml 即此写法，客户端按 file 方案可正常下载）
    $base = $UpdateUrlBase.TrimEnd('\')
    if (-not $base.StartsWith('\\')) {
        Write-Host "错误：-UpdateUrlBase 应为 UNC 路径（如 \\server\share\...），当前为 $base" -ForegroundColor Red
        exit 1
    }
    $url = 'file://' + ($base.Substring(2) -replace '\\', '/') + '/' + (Split-Path -Leaf $ZipPath)

    # 3. 生成 XML（UTF-8 无 BOM，避免 XML 解析兼容问题）
    $mandFlag = if ($Mandatory) { 'true' } else { 'false' }
    $xml = @"
<?xml version="1.0" encoding="UTF-8"?>
<item>
  <version>$Version</version>
  <url>$url</url>
  <mandatory>$mandFlag</mandatory>
  <checksum algorithm="SHA256">$sha</checksum>
</item>
"@
    $utf8NoBom = New-Object System.Text.UTF8Encoding($false)
    [System.IO.File]::WriteAllText($OutPath, $xml, $utf8NoBom)

    Write-Host "  appcast 已生成: $OutPath" -ForegroundColor Green
    Write-Host "  version=$Version  SHA256=$sha" -ForegroundColor DarkGray
}

if (-not $AppcastPath) { $AppcastPath = Join-Path $ScriptDir "appcast.xml" }

# ============================================================
# 0.2 发布到共享 update 目录
#     顺序很重要：先传 zip → 回读校验 zip 哈希 → 校验通过才覆盖 appcast.xml。
#     反过来（先改清单）会让客户端拿到指向未完成/不存在包的清单，正中本次故障。
# ============================================================
function Publish-ToShare {
    param(
        [Parameter(Mandatory)][string]$ZipPath,
        [Parameter(Mandatory)][string]$AppcastLocal,
        [Parameter(Mandatory)][string]$UpdateUrlBase,
        [bool]$Force = $false
    )

    $shareDir = $UpdateUrlBase.TrimEnd('\')
    if (-not $shareDir.StartsWith('\\')) {
        Write-Host "错误：无法发布，-UpdateUrlBase 不是 UNC 路径：$shareDir" -ForegroundColor Red
        exit 1
    }

    # 共享可达性检查
    if (-not (Test-Path -LiteralPath $shareDir)) {
        Write-Host "错误：共享目录不可访问：$shareDir" -ForegroundColor Red
        Write-Host "  请确认已连上内网/共享权限正常，或用 -NoDeploy 只出本地产物。" -ForegroundColor Red
        exit 1
    }

    $leaf = Split-Path -Leaf $ZipPath
    $remoteZip = Join-Path $shareDir $leaf
    $localSha = (Get-FileHash -LiteralPath $ZipPath -Algorithm SHA256).Hash
    $needCopy = $true

    # 同版本号已存在：内容必须一致，否则说明"同版本重新打包"，默认拒绝覆盖
    if (Test-Path -LiteralPath $remoteZip) {
        $remoteSha = (Get-FileHash -LiteralPath $remoteZip -Algorithm SHA256).Hash
        if ($remoteSha -ne $localSha) {
            if (-not $Force) {
                $old16 = $remoteSha.Substring(0, 16)
                $new16 = $localSha.Substring(0, 16)
                Write-Host "错误：共享上已存在同名包 $leaf，但内容不同（旧 $old16…，新 $new16…）。" -ForegroundColor Red
                Write-Host "  覆盖会让已发布客户端的校验值失效——这正是这次更新失败的根因。" -ForegroundColor Red
                Write-Host "  正确做法：递增版本号重新发布。确实要覆盖请加 -ForceDeploy。" -ForegroundColor Yellow
                exit 1
            }
            Write-Host "  警告：-ForceDeploy 已指定，覆盖共享上同版本号的不同内容包（老客户端可能校验失败）" -ForegroundColor Yellow
        } else {
            $needCopy = $false
            Write-Host "  共享上已存在相同的 $leaf，跳过上传" -ForegroundColor DarkGray
        }
    }

    # 1. 传 zip：先落临时名，校验通过后再改名就位，避免写坏共享上原有的包
    if ($needCopy) {
        $tmpRemote = "$remoteZip.uploading"
        Write-Host "  上传更新包 -> $remoteZip" -ForegroundColor DarkGray
        Copy-Item -LiteralPath $ZipPath -Destination $tmpRemote -Force

        # 2. 回读校验（网络复制可能截断）
        $verifySha = (Get-FileHash -LiteralPath $tmpRemote -Algorithm SHA256).Hash
        if ($verifySha -ne $localSha) {
            Remove-Item -LiteralPath $tmpRemote -Force
            Write-Host "错误：共享包回读校验不一致，临时副本已删除，原包与 appcast.xml 均未改动。请重试。" -ForegroundColor Red
            exit 1
        }
        Move-Item -LiteralPath $tmpRemote -Destination $remoteZip -Force
        Write-Host "  zip 上传并校验通过" -ForegroundColor Green
    }

    # 3. zip 确认就位后，才更新共享清单
    $remoteAppcast = Join-Path $shareDir "appcast.xml"
    Write-Host "  更新清单 -> $remoteAppcast" -ForegroundColor DarkGray
    Copy-Item -LiteralPath $AppcastLocal -Destination $remoteAppcast -Force

    $aSrc = (Get-FileHash -LiteralPath $AppcastLocal -Algorithm SHA256).Hash
    $aDst = (Get-FileHash -LiteralPath $remoteAppcast -Algorithm SHA256).Hash
    if ($aSrc -ne $aDst) {
        Write-Host "错误：共享 appcast.xml 回读校验不一致，请手动检查 $remoteAppcast" -ForegroundColor Red
        exit 1
    }
    Write-Host "  appcast.xml 已发布" -ForegroundColor Green

    return $remoteAppcast
}

# ============================================================
# 0.5 仅重建 appcast 模式：跳过编译，用现有 zip 生成正确清单
#     版本号不需要手填，按以下优先级自动推导：
#       1. -Version 显式指定（优先级最高，正常发布流程也用这个）
#       2. 目录里 ${ProductName}_v*.zip 中版本号最大的那个包
#       3. csproj 的 <Version>（此时若找不到对应 zip 会直接报错，避免清单指向不存在的包）
#     清单的 version/url/checksum 全部由所选 zip 反推，保证三者互相对得上。
# ============================================================
if ($AppcastOnly) {
    $zipReg = '^' + [regex]::Escape($ProductName) + '_v(\d+\.\d+\.\d+)\.zip$'
    $zipVerMap = @{}
    foreach ($f in Get-ChildItem -LiteralPath $ScriptDir -File -Filter "$ProductName`_v*.zip") {
        if ($f.Name -match $zipReg) { $zipVerMap[$Matches[1]] = $f.FullName }
    }

    if (-not [string]::IsNullOrEmpty($Version)) {
        # 1. 用户显式指定
        $appcastVersion = $Version
        if (-not $zipVerMap.ContainsKey($appcastVersion)) {
            Write-Host "错误：找不到 ${ProductName}_v$appcastVersion.zip，目录下现有：$(($zipVerMap.Keys | Sort-Object -Descending) -join ', ')" -ForegroundColor Red
            exit 1
        }
    }
    elseif ($zipVerMap.Count -gt 0) {
        # 2. 取版本号最大的现有 zip（Sort 用版本对象，避免 1.0.9 > 1.0.10 的字符串排序坑）
        $best = $zipVerMap.Keys |
            ForEach-Object { [pscustomobject]@{ V = [version]$_; S = $_; P = $zipVerMap[$_] } } |
            Sort-Object V -Descending | Select-Object -First 1
        $appcastVersion = $best.S
        Write-Host "  未指定 -Version，自动选用目录中最新版本包：v$appcastVersion" -ForegroundColor DarkGray
    }
    else {
        # 3. 回退 csproj
        if (Test-Path $CsprojPath) {
            $c = Get-Content $CsprojPath -Raw -Encoding UTF8
            if ($c -match '<Version>([^<]+)</Version>') { $appcastVersion = $Matches[1] }
        }
        if ([string]::IsNullOrEmpty($appcastVersion)) {
            Write-Host "错误：既找不到 ${ProductName}_v*.zip，也读不到 csproj 的 <Version>，无法确定版本号" -ForegroundColor Red
            exit 1
        }
        Write-Host "错误：想用 csproj 版本 v$appcastVersion，但目录里没有对应 zip 包，清单会指向不存在的文件。请先发布或用 -Version 指定已有包。" -ForegroundColor Red
        exit 1
    }

    $zip = $zipVerMap[$appcastVersion]

    # 与 csproj 交叉核对：不一致只警告不阻断（补救模式常见场景就是包已发布、源码版本另动过）
    if (Test-Path $CsprojPath) {
        $c2 = Get-Content $CsprojPath -Raw -Encoding UTF8
        if ($c2 -match '<Version>([^<]+)</Version>' -and $Matches[1] -ne $appcastVersion) {
            Write-Host "  提示：csproj 是 v$($Matches[1])，本次清单按 zip 写成 v$appcastVersion" -ForegroundColor Yellow
        }
    }

    Write-Host "`n[AppcastOnly] 仅重新生成 appcast.xml（版本 v$appcastVersion，哈希按该包实算）..." -ForegroundColor Yellow
    Write-Appcast -ZipPath $zip -Version $appcastVersion -UpdateUrlBase $UpdateUrlBase -OutPath $AppcastPath -Mandatory:$Mandatory

    if ($NoDeploy) {
        Write-Host "`n[-NoDeploy] 未发布到共享。本地产物：$AppcastPath 与 $zip" -ForegroundColor Yellow
    } else {
        Write-Host "`n[发布] 将 zip 与清单写入共享 update 目录..." -ForegroundColor Yellow
        Publish-ToShare -ZipPath $zip -AppcastLocal $AppcastPath -UpdateUrlBase $UpdateUrlBase -Force:$ForceDeploy
        Write-Host "`n发布完成，共享清单已同步 v$appcastVersion。" -ForegroundColor Green
    }
    exit 0
}

# ============================================================
# 1. 确定目标版本号
# ============================================================
if (-not (Test-Path $CsprojPath)) {
    Write-Host "错误：找不到 MultiOpticalSwitchTestApp.csproj" -ForegroundColor Red
    exit 1
}

# 从 .csproj 读取当前版本号
$content = Get-Content $CsprojPath -Raw -Encoding UTF8
$currentVersion = ""
if ($content -match '<Version>([^<]+)</Version>') {
    $currentVersion = $Matches[1]
}

if (-not [string]::IsNullOrEmpty($Version)) {
    $targetVersion = $Version
    $versionSource = "手动指定"
}
else {
    if ([string]::IsNullOrEmpty($currentVersion)) {
        Write-Host "错误：.csproj 里没有找到 <Version> 配置，无法自动叠加" -ForegroundColor Red
        exit 1
    }

    $parts = $currentVersion.Split('.')
    if ($parts.Length -lt 3) {
        Write-Host "错误：当前版本号格式不正确（$currentVersion），应为 3 段式" -ForegroundColor Red
        exit 1
    }

    $verMajor = [int]$parts[0]
    $verMinor = [int]$parts[1]
    $verPatch = [int]$parts[2]

    if ($Major) {
        $verMajor++
        $verMinor = 0
        $verPatch = 0
        $versionSource = "自动叠加主版本号"
    }
    elseif ($Minor) {
        $verMinor++
        $verPatch = 0
        $versionSource = "自动叠加次版本号"
    }
    else {
        $verPatch++
        $versionSource = "自动叠加修订号"
    }

    $targetVersion = "$verMajor.$verMinor.$verPatch"
}

# 校验版本号格式
if ($targetVersion -notmatch '^\d+\.\d+\.\d+$') {
    Write-Host "错误：版本号格式不正确（$targetVersion），应为 3 段式（如 1.1.0）" -ForegroundColor Red
    exit 1
}

$ZipPath = Join-Path $ScriptDir "${ProductName}_v$targetVersion.zip"

Write-Host "========================================" -ForegroundColor Cyan
Write-Host "  $ProductName 发布脚本" -ForegroundColor Cyan
Write-Host "  当前版本: v$currentVersion" -ForegroundColor Cyan
Write-Host "  目标版本: v$targetVersion ($versionSource)" -ForegroundColor Cyan
Write-Host "  运行时: $Runtime" -ForegroundColor Cyan
Write-Host "  自包含: $SelfContained" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan

# ============================================================
# 2. 修改 .csproj 版本号
# ============================================================
Write-Host "`n[1/4] 修改 .csproj 版本号..." -ForegroundColor Yellow
$content = $content -replace '<Version>[^<]*</Version>', "<Version>$targetVersion</Version>"
$content = $content -replace '<FileVersion>[^<]*</FileVersion>', "<FileVersion>$targetVersion.0</FileVersion>"
$content = $content -replace '<AssemblyVersion>[^<]*</AssemblyVersion>', "<AssemblyVersion>$targetVersion.0</AssemblyVersion>"
Set-Content $CsprojPath -Value $content -Encoding UTF8 -NoNewline
Write-Host "  版本号已修改为 v$targetVersion" -ForegroundColor Green

# ============================================================
# 3. 清理旧的发布目录
# ============================================================
if (Test-Path $PublishDir) {
    Write-Host "`n[2/4] 清理旧发布目录..." -ForegroundColor Yellow
    Remove-Item $PublishDir -Recurse -Force
    Write-Host "  已清理" -ForegroundColor Green
}

# ============================================================
# 4. 执行 dotnet publish
# ============================================================
Write-Host "`n[3/4] 编译发布..." -ForegroundColor Yellow
$selfContainedArg = if ($SelfContained) { "--self-contained" } else { "--no-self-contained" }
dotnet publish $CsprojPath -c Release -r $Runtime $selfContainedArg -o $PublishDir --nologo
if ($LASTEXITCODE -ne 0) {
    Write-Host "错误：编译发布失败" -ForegroundColor Red
    exit 1
}
$fileCount = (Get-ChildItem $PublishDir -Recurse -File).Count
Write-Host "  发布完成，共 $fileCount 个文件" -ForegroundColor Green

# ============================================================
# 4.2 组装插件目录 Plugins\（插件 dll + SCPI 内置配置）
# 主工程 publish 只带源码内嵌内容：插件工程（.slnx 里的兄弟项目）不会被编译，
# 且负责搬运配置的 OrganizePluginConfigs 挂在 Build 上、publish 不触发。
# 缺了这一步，客户端 Plugins\ 里没有 dll，型号下拉框为空（Ommodel 点不开）。
# ============================================================
if ($NoPlugins) {
    Write-Host "`n[3.2/4] 跳过插件组装（-NoPlugins）" -ForegroundColor DarkGray
} else {
    Write-Host "`n[3.2/4] 组装插件目录..." -ForegroundColor Yellow
    $PluginsOutDir = Join-Path $PublishDir "Plugins"

    # (1) SCPI 内置配置归位到运行时读取的 Plugins\configs\
    $configsDst = Join-Path $PluginsOutDir "configs"
    New-Item -ItemType Directory -Path $configsDst -Force | Out-Null
    $configsSrc = Join-Path $PublishDir "Plugins\GenericScpiPowerMeter\configs"
    if (-not (Test-Path $configsSrc)) { $configsSrc = Join-Path $ScriptDir "Plugins\GenericScpiPowerMeter\configs" }
    $jsons = Get-ChildItem -Path $configsSrc -Filter "*.json" -File -ErrorAction SilentlyContinue
    if (-not $jsons -or $jsons.Count -eq 0) {
        Write-Host "错误：找不到 SCPI 配置（$configsSrc），中止发布" -ForegroundColor Red
        exit 1
    }
    foreach ($j in $jsons) { Copy-Item -LiteralPath $j.FullName -Destination $configsDst -Force }
    $strayTree = Join-Path $PluginsOutDir "GenericScpiPowerMeter"
    if (Test-Path $strayTree) { Remove-Item -LiteralPath $strayTree -Recurse -Force }
    Write-Host "  SCPI 配置 x$($jsons.Count) -> $configsDst" -ForegroundColor DarkGray

    # (2) 解析 .slnx 里的插件工程并编译、收 dll
    if (-not (Test-Path $SlnxPath)) {
        Write-Host "错误：找不到解决方案 $SlnxPath，无法确定插件工程" -ForegroundColor Red
        exit 1
    }
    $slnx = Get-Content $SlnxPath -Raw -Encoding UTF8
    $mainLeaf = Split-Path -Leaf $CsprojPath
    $pluginList = @(
        [regex]::Matches($slnx, 'Path="([^"]+\.csproj)"') |
        ForEach-Object { [System.IO.Path]::GetFullPath((Join-Path $ScriptDir ($_.Groups[1].Value -replace '\\', '/'))) } |
        Where-Object { (Split-Path -Leaf $_) -ne $mainLeaf }
    )
    if ($pluginList.Count -eq 0) {
        Write-Host "错误：解决方案里没有插件工程，中止发布（若确属无插件版本请加 -NoPlugins）" -ForegroundColor Red
        exit 1
    }
    foreach ($proj in $pluginList) {
        $name = [System.IO.Path]::GetFileNameWithoutExtension($proj)
        Write-Host "  编译插件 $name ..." -ForegroundColor DarkGray
        dotnet build $proj -c Release --nologo | Out-Null
        if ($LASTEXITCODE -ne 0) {
            Write-Host "错误：插件 $name 编译失败，中止发布（不能发出缺插件的包）" -ForegroundColor Red
            exit 1
        }
        $dll = Get-ChildItem -Path (Join-Path (Split-Path -Parent $proj) "bin\Release") -Recurse -Filter "$name.dll" -File | Select-Object -First 1
        if (-not $dll) {
            Write-Host "错误：找不到插件产物 $name.dll，中止发布" -ForegroundColor Red
            exit 1
        }
        Copy-Item -LiteralPath $dll.FullName -Destination $PluginsOutDir -Force
        Write-Host "  $name.dll -> Plugins\" -ForegroundColor Green
    }

    # (3) 终检：发布包里必须有插件 dll
    $finalDlls = @(Get-ChildItem -Path $PluginsOutDir -Filter "*.dll" -File)
    if ($finalDlls.Count -lt $pluginList.Count) {
        Write-Host "错误：Plugins\ 里 dll 数量（$($finalDlls.Count)）少于插件工程数（$($pluginList.Count)），中止发布" -ForegroundColor Red
        exit 1
    }
    Write-Host "  插件组装完成：$($finalDlls.Count) 个 dll（$($finalDlls.Name -join ', ')）" -ForegroundColor Green
}

# ============================================================
# 4.5 编译 Updater 并复制到发布目录
# ============================================================
Write-Host "`n[3.5/4] 编译 Updater..." -ForegroundColor Yellow
$UpdaterCsproj = Join-Path $ScriptDir "Updater\Updater.csproj"
$UpdaterPublishDir = Join-Path $ScriptDir "Updater\publish"
if (Test-Path $UpdaterPublishDir) { Remove-Item $UpdaterPublishDir -Recurse -Force }
dotnet publish $UpdaterCsproj -c Release -r $Runtime $selfContainedArg -o $UpdaterPublishDir --nologo
if ($LASTEXITCODE -ne 0) {
    Write-Host "警告：Updater 编译失败，更新功能将不可用" -ForegroundColor Yellow
} else {
    $destUpdaterDir = Join-Path $PublishDir "Updater"
    Copy-Item $UpdaterPublishDir -Destination $destUpdaterDir -Recurse -Force
    Write-Host "  Updater 已复制到发布目录" -ForegroundColor Green
}

# ============================================================
# 5. 打 zip 包
# ============================================================
Write-Host "`n[4/4] 打 zip 包..." -ForegroundColor Yellow
if (Test-Path $ZipPath) { Remove-Item $ZipPath -Force }
Compress-Archive -Path "$PublishDir\*" -DestinationPath $ZipPath -CompressionLevel Optimal
$zipSize = (Get-Item $ZipPath).Length / 1MB
Write-Host "  已生成: ${ProductName}_v$targetVersion.zip ($([math]::Round($zipSize, 2)) MB)" -ForegroundColor Green

# ============================================================
# 5.5 生成 appcast.xml（SHA256 由脚本按刚打出的 zip 计算，不再手填）
# ============================================================
Write-Host "`n[5.5] 生成 appcast.xml..." -ForegroundColor Yellow
Write-Appcast -ZipPath $ZipPath -Version $targetVersion -UpdateUrlBase $UpdateUrlBase -OutPath $AppcastPath -Mandatory:$Mandatory

# ============================================================
# 5.6 发布到共享 update 目录（zip 先就位并回读校验，通过后清单才更新）
# ============================================================
$deployedAppcast = $null
if ($NoDeploy) {
    Write-Host "`n[5.6] 跳过共享发布（-NoDeploy）" -ForegroundColor DarkGray
} else {
    Write-Host "`n[5.6] 发布到共享 update 目录..." -ForegroundColor Yellow
    $deployedAppcast = Publish-ToShare -ZipPath $ZipPath -AppcastLocal $AppcastPath -UpdateUrlBase $UpdateUrlBase -Force:$ForceDeploy
}

# ============================================================
# 6. 输出结果
# ============================================================
Write-Host "`n========================================" -ForegroundColor Cyan
Write-Host "  发布完成！" -ForegroundColor Green
Write-Host "  版本: v$currentVersion -> v$targetVersion" -ForegroundColor Green
Write-Host "  发布目录: $PublishDir" -ForegroundColor Green
Write-Host "  更新包: $ZipPath" -ForegroundColor Green
Write-Host "  更新清单: $AppcastPath" -ForegroundColor Green
if ($deployedAppcast) {
    Write-Host "  已发布到共享: $(Split-Path -Parent $deployedAppcast)" -ForegroundColor Green
    Write-Host "========================================" -ForegroundColor Cyan
    Write-Host "`n客户端下次“检查更新”即可升级到 v$targetVersion（zip 与清单已在共享就位且校验一致）。" -ForegroundColor Yellow
} else {
    Write-Host "========================================" -ForegroundColor Cyan
    Write-Host "`n下一步：将下面两个文件一起拷到共享 update 目录" -ForegroundColor Yellow
    Write-Host "  ${ProductName}_v$targetVersion.zip" -ForegroundColor Yellow
    Write-Host "  appcast.xml（清单里的 SHA256 与 zip 严格对应，切勿只替换其中一个）" -ForegroundColor Yellow
    Write-Host "  路径：$UpdateUrlBase" -ForegroundColor Yellow
}
