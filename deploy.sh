#!/bin/bash

# 部署脚本 (deploy.sh)
set -e  # 遇到错误立即退出

# 输出当前时间
echo "开始部署: $(date)"

# 切换到项目目录
cd "$(dirname "$0")"
echo "当前目录: $(pwd)"

PLIST_PATH="/Users/kaka/Library/LaunchAgents/com.kaka.trading.plist"

# 卸载服务
echo "正在卸载服务"
launchctl unload "$PLIST_PATH" || echo "ℹ️ 服务可能未加载"

# 编译并复制
/opt/homebrew/bin/dotnet publish >/dev/null
cp -R -f ./src/Trading.API/bin/Release/net10.0/publish/* ~/trading/
echo "项目复制完成"

# 启动服务
launchctl load "$PLIST_PATH"

# 检查是否成功
if [ $? -eq 0 ]; then
  echo "✅ 服务已启动"
else
  echo "❌ 服务启动失败"
fi

# 查看进程状态
ps -ef | grep dotnet | grep Trading

echo "部署完成: $(date)"
