# Alaris - High-Performance Derivatives Trading System

Alaris is a production-grade derivatives trading system designed for volatility arbitrage using American options. The system employs a process-isolated architecture with deterministic execution guarantees.

## 🏗️ Quick Start

### Prerequisites
- Ubuntu 20.04+ (recommended)
- CMake 3.20+
- GCC 9.0+ with C++20 support
- QuantLib development libraries
- .NET 8 SDK (for Lean process)

### Build System
```bash
# Install dependencies (Ubuntu/Debian)
sudo apt-get update
sudo apt-get install -y build-essential cmake libboost-all-dev \
    libquantlib0-dev dotnet-sdk-8.0

# Build Alaris
./scripts/build.sh

# Run tests
cd build && make test
```

### Configuration
1. Edit `config/lean_process.yaml` with your Interactive Brokers credentials
2. Update `config/quantlib_process.yaml` for your system

### Start Trading
```bash
# Start QuantLib process
./scripts/start_alaris.sh

# In another terminal, start Lean process
cd src/csharp && dotnet run
```

## 📊 Monitoring
- System logs: `logs/`
- Performance metrics via shared memory
- Real-time health monitoring

## 🔧 Architecture
- **QuantLib Process (C++)**: Option pricing, volatility models, signal generation
- **Lean Process (C#)**: Market data, order execution, risk management
- **Shared Memory IPC**: Lock-free communication between processes

## ⚠️ Risk Warning
This is sophisticated trading software. Always test thoroughly with paper trading before using real capital.

For detailed documentation, see `docs/` directory.

## 🐳 Production Deployment

### Quick Production Setup
```bash
# Deploy to production
./scripts/deploy_production.sh

# Deploy for development
./scripts/deploy_development.sh
```

### Manual Docker Commands
```bash
# Build and start all services
docker-compose up --build -d

# View running services
docker-compose ps

# View logs
docker-compose logs -f quantlib-process
docker-compose logs -f lean-process

# Stop all services
docker-compose down

# Full cleanup
docker-compose down -v --remove-orphans
```

### Monitoring & Observability

#### Grafana Dashboards
- **URL**: http://localhost:3000
- **Credentials**: admin/alaris123
- **Dashboards**: 
  - Alaris Trading System (Main dashboard)
  - System performance metrics
  - Trading P&L tracking

#### Prometheus Metrics
- **URL**: http://localhost:9090
- **Metrics collected**:
  - System health (up/down status)
  - Trading performance (P&L, signals)
  - Process performance (latency, throughput)
  - Resource utilization

### Production Considerations

#### System Requirements
- **CPU**: 4+ cores (2 isolated for QuantLib process)
- **Memory**: 8GB+ RAM
- **Storage**: 100GB+ SSD for logs and data
- **Network**: Low-latency connection to Interactive Brokers

#### Security
- Run containers with non-root users
- Secure sensitive configuration files
- Monitor access to trading APIs
- Regular security updates

#### Backup & Recovery
- Database backups (if using persistent storage)
- Configuration backup
- Log archival strategy
- Disaster recovery procedures

### Interactive Brokers Configuration

#### Required Setup
1. **IB Gateway or TWS**: Must be running and configured
2. **API Permissions**: Enable API access in IB account
3. **Account Configuration**: Update `config/lean_process.yaml`:

```yaml
brokerage:
  gateway_host: "127.0.0.1"
  gateway_port: 4001        # 4001 for live, 7497 for paper
  account: "YOUR_ACCOUNT"   # Your IB account number
```

#### Testing Connection
```bash
# Test IB Gateway connectivity
nc -zv localhost 4001

# Check Lean process logs for connection status
docker-compose logs lean-process | grep -i "interactive"
```

### Troubleshooting

#### Common Issues

**1. QuantLib process fails to start**
```bash
# Check container logs
docker-compose logs quantlib-process

# Check shared memory permissions
ls -la /dev/shm/alaris/

# Verify QuantLib installation in container
docker-compose exec quantlib-process ldd /opt/alaris/bin/quantlib_process
```

**2. Lean process connection issues**
```bash
# Verify IB Gateway is running
netstat -an | grep 4001

# Check container networking
docker-compose exec lean-process ping quantlib-process

# Test IB connection
docker-compose logs lean-process | grep -E "(connection|error)"
```

**3. Monitoring not working**
```bash
# Check Prometheus targets
curl http://localhost:9090/api/v1/targets

# Restart monitoring stack
docker-compose restart prometheus grafana

# Check Grafana logs
docker-compose logs grafana
```

#### Performance Optimization

**1. System Tuning**
```bash
# Set CPU governor to performance
echo performance | sudo tee /sys/devices/system/cpu/cpu*/cpufreq/scaling_governor

# Increase shared memory limits
echo 'kernel.shmmax = 68719476736' | sudo tee -a /etc/sysctl.conf

# Apply changes
sudo sysctl -p
```

**2. Container Optimization**
```bash
# Monitor container resource usage
docker stats

# Adjust container resources in docker-compose.yml
# Increase CPU/memory limits for better performance
```

### Maintenance

#### Regular Tasks
- **Daily**: Check system health via Grafana
- **Weekly**: Review trading performance and logs
- **Monthly**: Update containers and dependencies
- **Quarterly**: Full system backup and DR test

#### Log Management
```bash
# Rotate logs
docker-compose exec quantlib-process logrotate /etc/logrotate.conf

# Archive old logs
tar -czf alaris-logs-$(date +%Y%m%d).tar.gz logs/

# Clean up old containers
docker system prune -f
```