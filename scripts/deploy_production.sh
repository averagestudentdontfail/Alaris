#!/bin/bash
# scripts/deploy_production.sh
# Production deployment script for Alaris Trading System

set -e

# Colors for output
readonly RED='\033[0;31m'
readonly GREEN='\033[0;32m'
readonly YELLOW='\033[1;33m'
readonly BLUE='\033[0;34m'
readonly NC='\033[0m'

# Script directory
readonly SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
readonly PROJECT_ROOT="$(dirname "$SCRIPT_DIR")"

# Configuration
readonly DEPLOYMENT_TYPE="production"
readonly BUILD_TYPE="Release"
readonly INSTALL_PREFIX="/opt/alaris"
readonly DATA_DIR="/var/lib/alaris"
readonly LOG_DIR="/var/log/alaris"
readonly CONFIG_DIR="/etc/alaris"

# Helper functions
log_info() { echo -e "${GREEN}[INFO]${NC} $1"; }
log_warn() { echo -e "${YELLOW}[WARN]${NC} $1"; }
log_error() { echo -e "${RED}[ERROR]${NC} $1"; }
log_header() { echo -e "${BLUE}=== $1 ===${NC}"; }

# Check if running as root
check_root() {
    if [[ $EUID -ne 0 ]]; then
        log_error "This script must be run as root for production deployment"
        exit 1
    fi
}

# Install system dependencies
install_dependencies() {
    log_header "Installing System Dependencies"
    
    apt-get update
    apt-get install -y \
        build-essential \
        cmake \
        ninja-build \
        libboost-all-dev \
        libyaml-cpp-dev \
        libssl-dev \
        git \
        systemd \
        logrotate \
        docker.io \
        docker-compose \
        prometheus-node-exporter
    
    # Install .NET 8 SDK
    if ! command -v dotnet &> /dev/null; then
        log_info "Installing .NET 8 SDK..."
        wget https://packages.microsoft.com/config/ubuntu/$(lsb_release -rs)/packages-microsoft-prod.deb -O packages-microsoft-prod.deb
        dpkg -i packages-microsoft-prod.deb
        rm packages-microsoft-prod.deb
        apt-get update
        apt-get install -y dotnet-sdk-8.0
    fi
    
    log_info "Dependencies installed successfully"
}

# Setup system optimizations
setup_system_optimizations() {
    log_header "Configuring System Optimizations"
    
    # CPU performance governor
    echo performance | tee /sys/devices/system/cpu/cpu*/cpufreq/scaling_governor
    
    # Disable CPU frequency scaling
    systemctl disable ondemand
    
    # Kernel parameters for real-time performance
    cat >> /etc/sysctl.conf << EOF

# Alaris Trading System Optimizations
kernel.sched_rt_runtime_us = -1
kernel.sched_rt_period_us = 1000000
vm.swappiness = 1
vm.dirty_ratio = 5
vm.dirty_background_ratio = 2
kernel.numa_balancing = 0
kernel.shmmax = 68719476736
kernel.shmall = 4294967296
net.core.rmem_max = 268435456
net.core.wmem_max = 268435456
net.ipv4.tcp_rmem = 4096 87380 268435456
net.ipv4.tcp_wmem = 4096 65536 268435456
net.core.netdev_max_backlog = 30000
net.ipv4.tcp_congestion_control = cubic
EOF
    
    sysctl -p
    
    # Huge pages
    echo 1024 > /proc/sys/vm/nr_hugepages
    
    # CPU isolation (isolate cores 2-3 for QuantLib process)
    if ! grep -q "isolcpus" /etc/default/grub; then
        sed -i 's/GRUB_CMDLINE_LINUX_DEFAULT="\(.*\)"/GRUB_CMDLINE_LINUX_DEFAULT="\1 isolcpus=2,3"/' /etc/default/grub
        update-grub
        log_warn "CPU isolation configured. Reboot required for full effect."
    fi
    
    log_info "System optimizations configured"
}

# Create user and directories
create_user_and_directories() {
    log_header "Creating User and Directories"
    
    # Create alaris user
    if ! id -u alaris &>/dev/null; then
        useradd -r -s /bin/bash -d /var/lib/alaris -m alaris
        log_info "Created alaris user"
    fi
    
    # Create directories
    mkdir -p "$INSTALL_PREFIX"/{bin,lib,etc,share/doc}
    mkdir -p "$DATA_DIR"/{cache,state}
    mkdir -p "$LOG_DIR"/{quantlib,lean,monitoring}
    mkdir -p "$CONFIG_DIR"
    mkdir -p /dev/shm/alaris
    
    # Set permissions
    chown -R alaris:alaris "$INSTALL_PREFIX"
    chown -R alaris:alaris "$DATA_DIR"
    chown -R alaris:alaris "$LOG_DIR"
    chown -R alaris:alaris "$CONFIG_DIR"
    chmod 777 /dev/shm/alaris
    
    # Set up security limits for alaris user
    cat > /etc/security/limits.d/99-alaris.conf << EOF
alaris soft rtprio 99
alaris hard rtprio 99
alaris soft memlock unlimited
alaris hard memlock unlimited
alaris soft nofile 65536
alaris hard nofile 65536
alaris soft stack unlimited
alaris hard stack unlimited
EOF
    
    log_info "User and directories created"
}

# Build the project
build_project() {
    log_header "Building Alaris Trading System"
    
    cd "$PROJECT_ROOT"
    
    # Clean build directory
    rm -rf build
    
    # Configure and build
    sudo -u alaris bash << EOF
    cd "$PROJECT_ROOT"
    mkdir -p build
    cd build
    
    cmake .. \
        -DCMAKE_BUILD_TYPE=Release \
        -DCMAKE_INSTALL_PREFIX="$INSTALL_PREFIX" \
        -DCMAKE_CXX_COMPILER_LAUNCHER=ccache \
        -DBUILD_TESTS=OFF \
        -DENABLE_SANITIZERS=OFF \
        -DENABLE_COVERAGE=OFF \
        -GNinja
    
    ninja -j$(nproc)
EOF
    
    log_info "Build completed successfully"
}

# Install the system
install_system() {
    log_header "Installing Alaris Trading System"
    
    cd "$PROJECT_ROOT/build"
    ninja install
    
    # Install configuration files
    cp -r "$PROJECT_ROOT/config"/* "$CONFIG_DIR/"
    
    # Update configuration paths
    sed -i "s|/var/log/alaris|$LOG_DIR|g" "$CONFIG_DIR"/*.yaml
    sed -i "s|/opt/alaris|$INSTALL_PREFIX|g" "$CONFIG_DIR"/*.yaml
    
    chown -R alaris:alaris "$CONFIG_DIR"
    
    log_info "Installation completed"
}

# Setup systemd services
setup_systemd_services() {
    log_header "Setting up Systemd Services"
    
    # QuantLib service
    cat > /etc/systemd/system/alaris-quantlib.service << EOF
[Unit]
Description=Alaris QuantLib Trading Process
After=network.target
Wants=network.target

[Service]
Type=simple
User=alaris
Group=alaris
WorkingDirectory=$DATA_DIR
ExecStart=$INSTALL_PREFIX/bin/quantlib_process $CONFIG_DIR/quantlib_process.yaml
ExecReload=/bin/kill -HUP \$MAINPID
Restart=always
RestartSec=10
StandardOutput=journal
StandardError=journal
SyslogIdentifier=alaris-quantlib

# Performance settings
CPUAffinity=2 3
LimitRTPRIO=99
LimitMEMLOCK=infinity
LimitNOFILE=65536
PrivateTmp=true
ProtectSystem=strict
ProtectHome=true
NoNewPrivileges=true
ReadWritePaths=$LOG_DIR $DATA_DIR /dev/shm/alaris

# Environment
Environment="ALARIS_CONFIG_FILE=$CONFIG_DIR/quantlib_process.yaml"
Environment="ALARIS_LOG_LEVEL=INFO"

[Install]
WantedBy=multi-user.target
EOF

    # Lean service
    cat > /etc/systemd/system/alaris-lean.service << EOF
[Unit]
Description=Alaris Lean Trading Process
After=network.target alaris-quantlib.service
Wants=network.target
Requires=alaris-quantlib.service

[Service]
Type=simple
User=alaris
Group=alaris
WorkingDirectory=$INSTALL_PREFIX/bin/lean
ExecStart=/usr/bin/dotnet $INSTALL_PREFIX/bin/lean/Alaris.Lean.dll
Restart=always
RestartSec=10
StandardOutput=journal
StandardError=journal
SyslogIdentifier=alaris-lean

# Performance settings
LimitNOFILE=65536
PrivateTmp=true
ProtectSystem=strict
ProtectHome=true
NoNewPrivileges=true
ReadWritePaths=$LOG_DIR $DATA_DIR /dev/shm/alaris

# Environment
Environment="DOTNET_RUNNING_IN_CONTAINER=false"
Environment="DOTNET_EnableDiagnostics=0"
Environment="COMPlus_gcServer=1"
Environment="ALARIS_CONFIG_FILE=$CONFIG_DIR/lean_process.yaml"
Environment="ALARIS_LOG_LEVEL=INFO"

[Install]
WantedBy=multi-user.target
EOF

    systemctl daemon-reload
    systemctl enable alaris-quantlib.service
    systemctl enable alaris-lean.service
    
    log_info "Systemd services configured"
}

# Setup log rotation
setup_log_rotation() {
    log_header "Setting up Log Rotation"
    
    cat > /etc/logrotate.d/alaris << EOF
$LOG_DIR/*.log {
    daily
    rotate 30
    compress
    delaycompress
    missingok
    notifempty
    create 644 alaris alaris
    sharedscripts
    postrotate
        systemctl reload alaris-quantlib 2>/dev/null || true
        systemctl reload alaris-lean 2>/dev/null || true
    endscript
}
EOF
    
    log_info "Log rotation configured"
}

# Setup monitoring
setup_monitoring() {
    log_header "Setting up Monitoring Stack"
    
    cd "$PROJECT_ROOT"
    
    # Create monitoring directories
    mkdir -p "$DATA_DIR"/{prometheus,grafana,redis}
    chown -R 65534:65534 "$DATA_DIR"/prometheus
    chown -R 472:472 "$DATA_DIR"/grafana
    
    # Start monitoring services
    docker-compose up -d prometheus grafana redis
    
    # Setup Prometheus Node Exporter systemd service
    systemctl enable prometheus-node-exporter
    systemctl start prometheus-node-exporter
    
    log_info "Monitoring stack deployed"
}

# Perform system validation
validate_deployment() {
    log_header "Validating Deployment"
    
    local errors=0
    
    # Check binaries
    if [[ -x "$INSTALL_PREFIX/bin/quantlib_process" ]]; then
        log_info "✓ QuantLib process executable found"
    else
        log_error "✗ QuantLib process executable not found"
        ((errors++))
    fi
    
    if [[ -f "$INSTALL_PREFIX/bin/lean/Alaris.Lean.dll" ]]; then
        log_info "✓ Lean process assembly found"
    else
        log_error "✗ Lean process assembly not found"
        ((errors++))
    fi
    
    # Validate configuration
    if "$INSTALL_PREFIX/bin/alaris_config_validator" "$CONFIG_DIR/quantlib_process.yaml"; then
        log_info "✓ QuantLib configuration valid"
    else
        log_error "✗ QuantLib configuration invalid"
        ((errors++))
    fi
    
    # Check system health
    if "$INSTALL_PREFIX/bin/alaris_sysinfo" --health-check; then
        log_info "✓ System health check passed"
    else
        log_warn "! System health check warnings"
    fi
    
    # Check services
    if systemctl is-enabled alaris-quantlib.service &>/dev/null; then
        log_info "✓ QuantLib service enabled"
    else
        log_error "✗ QuantLib service not enabled"
        ((errors++))
    fi
    
    if [[ $errors -eq 0 ]]; then
        log_info "Deployment validation PASSED"
        return 0
    else
        log_error "Deployment validation FAILED with $errors errors"
        return 1
    fi
}

# Show deployment summary
show_summary() {
    log_header "Deployment Summary"
    
    echo "Installation Directory: $INSTALL_PREFIX"
    echo "Configuration Directory: $CONFIG_DIR"
    echo "Data Directory: $DATA_DIR"
    echo "Log Directory: $LOG_DIR"
    echo ""
    echo "Services:"
    echo "  - alaris-quantlib.service"
    echo "  - alaris-lean.service"
    echo ""
    echo "Monitoring:"
    echo "  - Grafana: http://localhost:3000 (admin/alaris123)"
    echo "  - Prometheus: http://localhost:9090"
    echo ""
    echo "Commands:"
    echo "  Start services: systemctl start alaris-quantlib alaris-lean"
    echo "  View logs: journalctl -fu alaris-quantlib"
    echo "  Check status: systemctl status alaris-quantlib alaris-lean"
    echo ""
    echo "IMPORTANT: A system reboot is recommended to apply all optimizations"
}

# Main deployment function
main() {
    log_header "Alaris Trading System - Production Deployment"
    
    check_root
    install_dependencies
    setup_system_optimizations
    create_user_and_directories
    build_project
    install_system
    setup_systemd_services
    setup_log_rotation
    setup_monitoring
    
    if validate_deployment; then
        show_summary
        log_info "Production deployment completed successfully!"
    else
        log_error "Production deployment completed with errors"
        exit 1
    fi
}

# Run main function
main "$@"