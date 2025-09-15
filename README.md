# Trading Service Documentation


## 目录
- [基础命令](#基础命令)
- [策略管理](#策略管理)
  - [策略类型说明](#策略类型说明)
  - [策略示例](#策略示例)
- [警报管理](#警报管理)
- [部署说明](#部署说明)

## 基础命令

| 命令 | 说明 | 子命令 |
|------|------|--------|
| `/help` | 显示帮助信息 | - |
| `/debug` | 查看状态 | - |
| `/market` | 查看行情 | - |
| `/strategy` | 策略管理 | create, delete, pause, resume |
| `/alert` | 警报管理 | create, delete, empty, pause, resume |

## 策略管理

### 策略类型说明

#### RestClient 策略
- **OpenBuy** 和 **OpenSell**:  基于指定周期开盘价格执行的策略
- 特点：不需要等待收盘，自动获取当前周期的开盘价格进行下单。如果AutoReset为true，则会在每个新周期开始时取消未成交订单并重新下单

#### WebSocket 策略
- **CloseBuy** 和 **CloseSell**: 基于指定周期收盘价格执行的策略
- ⚠️ 注意：必须等待当前周期收盘后才会执行下单, 不会在新周期开始时自动更新价格下单

### 策略示例

#### 1. 现货做多策略 (OpenBuy)
```json
/strategy create {
    "Symbol": "BTCUSDT",
    "Amount": 1000,
    "Volatility": 0.2,
    "Interval": "1d",
    "AccountType": "Spot",
    "AutoReset": false,
    "StopLossExpression": "close = 0",
    "StrategyType": "OpenBuy"
}
```

#### 2. 合约做空策略 (OpenSell)
```json
/strategy create {
    "Symbol": "BTCUSDT",
    "Amount": 1000,
    "Volatility": 0.2,
    "Interval": "1d",
    "AccountType": "Future",
    "AutoReset": false,
    "StopLossExpression": "close = 0",
    "StrategyType": "OpenSell"
}
```

#### 3. WebSocket合约做空策略 (CloseSell)
```json
/strategy create {
    "Symbol": "BTCUSDT",
    "Amount": 1000,
    "Volatility": 0.002,
    "Interval": "4h",
    "AccountType": "Future",
    "AutoReset": false,
    "StopLossExpression": "close = 0",
    "StrategyType": "CloseSell"
}
```

#### 4. WebSocket合约做多策略 (CloseBuy)
```json
/strategy create {
    "Symbol": "BTCUSDT",
    "Amount": 1000,
    "Volatility": 0.002,
    "Interval": "4h",
    "AccountType": "Future",
    "AutoReset": false,
    "StopLossExpression": "close = 0",
    "StrategyType": "CloseBuy"
}
```

#### 删除策略
```
/strategy delete <Id>
```

## 警报管理

### 支持的时间间隔
- 5m (5分钟)
- 15m (15分钟)
- 1h (1小时)
- 4h (4小时)
- 1d (1天)
- 3d (3天)
- 1w (1周)

### 警报示例

#### 1. 价格波动警报
```json
/alert create {
    "Symbol": "BTCUSDT",
    "Interval": "4h",
    "Expression": "Math.abs((close - open) / open) >= 0.02"
}
```

#### 2. 价格阈值警报
```json
/alert create {
    "Symbol": "BTCUSDT",
    "Interval": "4h",
    "Expression": "close > 20000"
}
```

#### 删除指定警报
```
/alert delete <Id>
```

#### 清空所有警报
```
/alert empty
```
## 警报管理
### 调用格式
- interval 只支持 1d 4h
```
/market <symbol> <interval>
```
### 调用示例

```
/market SOLUSDT 1d
```
```
/market ETHUSDT 4h
```

## 部署说明

### Docker部署
1. 构建镜像:
```bash
cd Trading && docker build -t trading-api:latest -f "src/Trading.API/Dockerfile" .
```

2. 使用deploy.sh部署:
```bash
./deploy.sh
```