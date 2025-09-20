#!/bin/bash

# 部署脚本 (deploy.sh)
set -e  # 遇到错误立即退出

# 输出当前时间
echo "当前时间: $(date)"

# 显示容器状态
echo "容器状态:"
docker-compose ps 

echo "部署完成: $(date)"