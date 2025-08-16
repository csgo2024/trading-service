#!/bin/bash

# 部署脚本 (deploy.sh)
set -e  # 遇到错误立即退出

# 输出当前时间
echo "开始部署: $(date)"

# 切换到项目目录
cd "$(dirname "$0")"
echo "当前目录: $(pwd)"

# 显示容器状态
echo "容器状态:"
docker-compose ps 

echo "部署完成: $(date)"