# Alaris Deployment Guide

## Overview
This guide covers deploying Alaris in various environments, from development to production.

## Architecture
- **QuantLib Process**: C++ process handling pricing and strategy
- **Lean Process**: C# process handling market data and execution
- **Shared Memory**: Lock-free IPC between processes
- **Monitoring**: Prometheus + Grafana for observability

## Prerequisites

### System Requirements
- Ubuntu 20.04+ (recommended)
- Docker 20.10+
- Docker Compose 2.0+
- 8GB+ RAM
- 4+ CPU cores

### Development Requirements
- CMake 3.20+
- GCC 9+ with C++20 support
- .NET 8 SDK
- QuantLib development libraries

## Deployment Types

### 1. Development Deployment
Quick setup for development and testing:

```bash
./scripts/deploy_development.sh
```

This will:
- Build the system locally
- Start services with Docker Compose
- Enable development-friendly logging
- Setup monitoring dashboards

### 2. Production Deployment
Production-ready deployment with full monitoring:

```bash
./scripts/deploy_production.sh
```

This will:
- Build optimized containers
- Configure production settings
- Setup comprehensive monitoring
- Enable health checks and auto-restart

## Configuration

### QuantLib Process (`config/quantlib_process.yaml`)
Key settings for production:

```yaml
process:
  priority: 80              # Real-time priority
  cpu_affinity: [2, 3]      # Isolated CPU cores
  memory_lock: true         # Lock memory pages

strategy:
  vol_arbitrage:
    entry_threshold: 0.05   # Risk parameters
    risk_limit: 0.10
```

### Lean Process (`config/lean_process.yaml`)
Configure Interactive Brokers connection:

```yaml
brokerage:
  type: "InteractiveBrokers"
  gateway_host: "127.0.0.1"
  gateway_port: 4001
  account: "YOUR_ACCOUNT_ID"

risk_management:
  max_position_size: 0.05   # 5% per position
  max_daily_loss: 0.02      # 2% daily loss limit
```

## Monitoring Setup

### Grafana
- **URL**: http://localhost:3000
- **Login**: admin/alaris123
- **Dashboards**: System health, trading performance

### Prometheus
- **URL**: http://localhost:9090
- **Metrics**: Real-time system and trading metrics

## Security Considerations

### Container Security
- Non-root users in containers
- Limited capabilities and privileges
- Secure secret management

### Network Security
- Internal Docker networks
- Firewall rules for external access
- Encrypted connections where possible

### Trading Security
- Secure API credentials
- Access logging and monitoring
- Regular security updates

## Backup & Recovery

### Critical Data
- Configuration files
- Trading logs
- Performance metrics
- System state

### Backup Strategy
```bash
# Daily backup
tar -czf backup-$(date +%Y%m%d).tar.gz config/ logs/ data/

# Database backup (if applicable)
docker-compose exec postgres pg_dump trading_db > backup.sql
```

### Recovery Procedures
1. Stop all services
2. Restore configuration files
3. Restore data from backup
4. Restart services
5. Verify system health

## Performance Tuning

### System Level
```bash
# CPU performance governor
echo performance | sudo tee /sys/devices/system/cpu/cpu*/cpufreq/scaling_governor

# Memory tuning
echo 'vm.swappiness=1' | sudo tee -a /etc/sysctl.conf

# Network tuning
echo 'net.core.rmem_max=268435456' | sudo tee -a /etc/sysctl.conf
```

### Container Level
- CPU affinity and isolation
- Memory limits and reservations
- Resource constraints

### Application Level
- QuantLib grid parameters
- Memory pool sizing
- Execution frequency tuning

## Troubleshooting

### Common Issues
1. **Container startup failures**: Check logs and dependencies
2. **IB connection issues**: Verify gateway status and credentials
3. **Performance problems**: Monitor resource usage and tuning
4. **Memory issues**: Check shared memory configuration

### Diagnostic Commands
```bash
# System status
docker-compose ps
docker-compose logs -f

# Resource usage
docker stats
htop

# Network connectivity
nc -zv localhost 4001  # IB Gateway
curl http://localhost:9090  # Prometheus
```

### Log Locations
- QuantLib: `/var/log/alaris/quantlib.log`
- Lean: `/var/log/alaris/lean.log`
- System: Docker Compose logs

## Maintenance

### Regular Tasks
- Monitor system health daily
- Review trading performance weekly
- Update dependencies monthly
- Full system backup quarterly

### Updates
```bash
# Update containers
docker-compose pull
docker-compose up -d

# Update system packages
sudo apt update && sudo apt upgrade
```

### Log Rotation
```bash
# Setup logrotate for Alaris logs
sudo cp configs/logrotate.conf /etc/logrotate.d/alaris
```

This completes the deployment setup for Alaris trading system.
