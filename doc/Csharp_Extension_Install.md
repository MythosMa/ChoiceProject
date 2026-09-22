# 手动安装 / 更新 C# 扩展（ms-dotnettools.csharp）说明

> 适用环境：**Qoder CN** + macOS **Apple Silicon (darwin-arm64)**
> 本项目为 Godot Mono C# 项目，C# 语言支持依赖 `ms-dotnettools.csharp` 扩展。

## 一、为什么需要手动安装

在 Qoder CN 的扩展市场里搜索 **C#（Microsoft）** 点安装时，会提示：

```
Installation is unavailable. Please download the extension package from the VS Code Marketplace.
```

原因是 **微软的授权限制**：微软官方扩展（含 `ms-dotnettools.csharp`、`ms-dotnettools.csdevkit` 等）
只允许通过官方 VS Code 的 Marketplace 分发，第三方 IDE（Qoder CN 使用的 Open VSX 类市场）被屏蔽，
所以**无法在扩展市场内直接安装/更新**。

> ⚠️ 结论：以后每次更新 C# 扩展都会遇到同样的提示，都必须走下面"下载 VSIX → 从 VSIX 安装"的手动流程。

## 二、下载 VSIX 包（darwin-arm64 直链）

VS Code Marketplace 的平台专用扩展直链格式如下：

```
https://marketplace.visualstudio.com/_apis/public/gallery/publishers/{发布者}/vsextensions/{扩展名}/{版本号}/vspackage?targetPlatform={平台}
```

针对 C# 扩展（发布者 `ms-dotnettools`、扩展名 `csharp`、Apple Silicon 平台 `darwin-arm64`）：

```
https://marketplace.visualstudio.com/_apis/public/gallery/publishers/ms-dotnettools/vsextensions/csharp/{版本号}/vspackage?targetPlatform=darwin-arm64
```

- 把 `{版本号}` 换成需要的版本，例如 `2.140.9`：

  ```
  https://marketplace.visualstudio.com/_apis/public/gallery/publishers/ms-dotnettools/vsextensions/csharp/2.140.9/vspackage?targetPlatform=darwin-arm64
  ```

- **务必带 `?targetPlatform=darwin-arm64` 参数**，否则下到的可能是通用版 / 其它平台版，
  里面的 Roslyn 语言服务原生二进制与本机不匹配，装了也无法正常工作。

### 版本选择建议

- 需与已安装的 **C# Dev Kit（`ms-dotnettools.csdevkit`）** 版本配套。
- 本项目验证过可用的组合：`csdevkit 3.40.204` + `csharp 2.140.9`。
- 查看当前已装的 Dev Kit 版本：

  ```bash
  ls ~/.qoder-cn/extensions/ | grep csdevkit
  ```

### 备用下载方式

若直链下载失败，可打开市场页面手动下载（注意仍要选 darwin-arm64 平台包）：

```
https://marketplace.visualstudio.com/items?itemName=ms-dotnettools.csharp
```

页面右侧 **Resources → Download Extension**。此方式可能给的是通用版，**优先用上面带 `targetPlatform` 的直链**。

## 三、处理下载文件（.gz / .vspackage → .vsix）

直链下载得到的文件往往不是标准 `.vsix` 名，需要处理：

1. **如果是 `.gz` 结尾**（gzip 压缩）：先解压。

   ```bash
   gunzip 下载的文件.gz
   ```

2. **如果扩展名是 `.vspackage`**（或其它非 `.vsix`）：直接改名为 `.vsix`。

   ```bash
   mv ms-dotnettools.csharp-*.vspackage ms-dotnettools.csharp.vsix
   ```

3. 最终得到一个可安装的 `xxx.vsix` 文件即可。

> 小提示：VSIX 本质是一个 zip 包，正常的 VSIX 里应包含 `extension.vsixmanifest` 和 `extension/` 目录。
> 若解压后看不到这些内容，说明下载的其实是 gzip 包，需先 `gunzip` 再改名。

## 四、在 Qoder CN 中从 VSIX 安装

1. 打开左侧 **扩展（Extensions）** 面板。
2. 点面板右上角的 **`...`（更多操作）** 菜单。
3. 选择 **`Install from VSIX...`（从 VSIX 安装）**。
4. 选中上一步得到的 `.vsix` 文件。
5. 安装完成后 **重新加载窗口**：`Cmd+Shift+P` → 输入 `Reload Window` → 回车。

重载后 C# 语言服务启动，代码补全、错误提示（如 `CS0535`、`CS0050` 等）即恢复正常。

## 五、依赖与排错

### 相关扩展

C# 完整支持通常涉及三个扩展（后两个一般已在市场内装好，无需手动处理）：

| 扩展 ID                                | 作用                               | 是否受微软市场限制 |
| -------------------------------------- | ---------------------------------- | ------------------ |
| `ms-dotnettools.csharp`                | C# 基础语言支持（本文重点）        | 是，需手动 VSIX    |
| `ms-dotnettools.csdevkit`              | C# Dev Kit（解决方案管理、调试等） | 是                 |
| `ms-dotnettools.vscode-dotnet-runtime` | .NET 运行时管理                    | 依赖项             |

查看当前已登记的扩展：

```bash
python3 - <<'EOF'
import json,os
d=json.load(open(os.path.expanduser('~/.qoder-cn/extensions/extensions.json')))
for e in d:
    i=e.get('identifier',{}).get('id','')
    if 'dotnet' in i or 'csharp' in i:
        print(i, e.get('version',''))
EOF
```

### 安装后不生效 / 反复"待删除"

若之前更新中途失败，可能残留"半卸载"状态：扩展文件夹还在，但被标记作废、且从安装清单里被摘掉，
导致重装冲突。清理方法（**必须先完全退出 Qoder CN，`Cmd+Q`**，因为 IDE 退出时会把状态写回覆盖）：

```bash
EXT=~/.qoder-cn/extensions
# 1) 删除残留的旧 csharp 文件夹（版本号按实际替换）
rm -rf "$EXT"/ms-dotnettools.csharp-<旧版本号>-darwin-arm64
# 2) 从 .obsolete 中移除对应作废标记
python3 - "$EXT/.obsolete" <<'EOF'
import json,sys
p=sys.argv[1]
d=json.load(open(p))
# 删除所有 csharp 相关作废项
for k in [k for k in d if 'ms-dotnettools.csharp' in k]:
    d.pop(k)
json.dump(d,open(p,'w'))
print('已清理 csharp 作废项')
EOF
```

清理后再重开 Qoder CN，按第四节重新从 VSIX 安装即可。

## 六、快速流程速查（TL;DR）

1. `Cmd+Q` 退出 Qoder CN（仅排错/清理时需要；单纯安装可跳过）。
2. 下载：`.../publishers/ms-dotnettools/vsextensions/csharp/<版本>/vspackage?targetPlatform=darwin-arm64`
3. 处理：`gunzip` 解压 → 改名为 `.vsix`。
4. 安装：扩展面板 `...` → `Install from VSIX...` → 选文件。
5. 重载：`Cmd+Shift+P` → `Reload Window`。
