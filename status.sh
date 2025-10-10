#!/bin/bash

# 部署脚本 (deploy.sh)
set -e  # 遇到错误立即退出

# 切换到项目目录
cd "$(dirname "$0")"
echo "当前目录: $(pwd)"

# 查看进程状态
ps -eo start,pid,command | grep dotnet | grep -i trading | while read -r line; do
  pid=$(echo "$line" | awk '{print $2}')
  etime=$(ps -p "$pid" -o etime=)
  echo "$line" | awk -v etime="$etime" '{printf "%-8s up=%-8s ", $1, etime; for(i=3;i<=NF;i++) printf "%s ", $i; print ""}'
done