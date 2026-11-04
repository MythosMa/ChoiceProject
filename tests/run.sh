#!/usr/bin/env bash
# 机制断言（headless）。退出码 0 = 全过，1 = 有失败。
# 用法：./tests/run.sh           （用 PATH 里的 godot）
#       GODOT=/path/to/Godot ./tests/run.sh
set -euo pipefail
GODOT="${GODOT:-godot}"
ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
exec "$GODOT" --headless --path "$ROOT" res://tests/tests.tscn
