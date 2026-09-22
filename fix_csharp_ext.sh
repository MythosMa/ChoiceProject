#!/bin/bash
# ============================================================
# 修复 Qoder CN 的 C# 扩展（ms-dotnettools.csharp）缺失问题
#
# 背景：微软授权限制导致 Qoder CN 无法从扩展市场安装 C# 扩展，
#       且历史更新失败留下"半卸载"状态（文件夹被删、.obsolete 残留作废标记、
#       extensions.json 未登记），最终 Roslyn 语言服务起不来、代码无报错提示。
#
# 做法：把本机其它 profile 里已有的可用 csharp 扩展复制到 ~/.qoder-cn，
#       并在 extensions.json 登记、清理 .obsolete 作废标记。
#
# ⚠️ 运行前必须：完全退出 Qoder CN（Cmd+Q），并在【系统终端 Terminal.app】里运行，
#    不要用 Qoder 内置终端（退出 IDE 会把它一起关掉）。
# ============================================================
set -e

VERSION="2.140.9"
FOLDER="ms-dotnettools.csharp-${VERSION}-darwin-arm64"
DEST_DIR="$HOME/.qoder-cn/extensions"
DEST="$DEST_DIR/$FOLDER"

# 依次尝试从这些已存在可用副本的 profile 复制
SRC_CANDIDATES=(
  "$HOME/.qoder/extensions/$FOLDER"
  "$HOME/.vscode/extensions/$FOLDER"
)

echo "=== 0. 检查 Qoder CN 是否已退出 ==="
if pgrep -f "Qoder CN.app/Contents/MacOS/Qoder CN" >/dev/null 2>&1; then
  echo "⚠️  Qoder CN 仍在运行。请先 Cmd+Q 完全退出，再重新运行本脚本。"
  exit 1
fi
echo "OK：Qoder CN 未运行。"

echo ""
echo "=== 1. 复制扩展文件夹到 $DEST_DIR ==="
if [ -d "$DEST" ]; then
  echo "目标已存在，跳过复制：$DEST"
else
  SRC=""
  for c in "${SRC_CANDIDATES[@]}"; do
    if [ -d "$c" ]; then SRC="$c"; break; fi
  done
  if [ -z "$SRC" ]; then
    echo "❌ 未找到可用的 csharp 源文件夹，请先按 doc/Csharp_Extension_Install.md 下载 VSIX。"
    exit 1
  fi
  cp -R "$SRC" "$DEST"
  echo "✅ 已从 $SRC 复制到 $DEST"
fi

echo ""
echo "=== 2. 登记 extensions.json + 3. 清理 .obsolete ==="
python3 - "$DEST_DIR" "$DEST" "$VERSION" <<'EOF'
import json, sys, os, time

dest_dir, dest, version = sys.argv[1], sys.argv[2], sys.argv[3]

# --- extensions.json ---
ej = os.path.join(dest_dir, 'extensions.json')
d = json.load(open(ej))
# 先移除同 id 旧条目，避免重复
d = [e for e in d if e.get('identifier', {}).get('id', '') != 'ms-dotnettools.csharp']
entry = {
    "identifier": {"id": "ms-dotnettools.csharp", "uuid": "d0bfc4ab-1d3a-4487-8782-7cf6027b4fff"},
    "version": version,
    "location": {"$mid": 1, "path": dest, "scheme": "file"},
    "relativeLocation": os.path.basename(dest),
    "metadata": {
        "isApplicationScoped": False, "isMachineScoped": False, "isBuiltin": False,
        "installedTimestamp": int(time.time() * 1000), "pinned": False, "source": "gallery",
        "id": "d0bfc4ab-1d3a-4487-8782-7cf6027b4fff",
        "publisherId": "d05e23de-3974-4ff0-8d47-23ee77830092",
        "publisherDisplayName": "Microsoft", "targetPlatform": "darwin-arm64",
        "updated": True, "private": False, "isPreReleaseVersion": False,
        "hasPreReleaseVersion": False, "preRelease": False
    }
}
d.append(entry)
json.dump(d, open(ej, 'w'), ensure_ascii=False)
print("✅ extensions.json 已登记 ms-dotnettools.csharp", version)

# --- .obsolete ---
ob = os.path.join(dest_dir, '.obsolete')
if os.path.exists(ob):
    o = json.load(open(ob))
    removed = [k for k in o if 'ms-dotnettools.csharp' in k]
    for k in removed:
        o.pop(k)
    json.dump(o, open(ob, 'w'))
    print("✅ .obsolete 已移除 csharp 作废标记:", removed or "(原本就没有)")
else:
    print("(无 .obsolete 文件，跳过)")
EOF

echo ""
echo "============================================================"
echo "🎉 全部完成！"
echo "下一步：重新打开 Qoder CN → 打开本项目 →"
echo "       等待右下角 C# / Roslyn 语言服务加载完成（首次约十几秒），"
echo "       打开任意 .cs 文件即可看到报错提示与代码补全。"
echo "============================================================"
