#!/bin/bash
# Post-installation setup script for Alaris Trading System
# This script is automatically configured by CMake

set -e

# Configuration variables (set by CMake)
readonly INSTALL_PREFIX="@CMAKE_INSTALL_PREFIX@"
readonly VERSION="@PROJECT_VERSION@"
readonly BUILD_TYPE="@CMAKE_BUILD_TYPE@"
readonly SYSTEM_NAME="@CMAKE_SYSTEM_NAME@"

# Colors for output
readonly RED='\033[0;31m'
readonly GREEN='\033[0;32m'
readonly YELLOW='\033[1;33m'
readonly BLUE='\033[0;34m'
readonly NC='\033[0m'

# Logging functions
log_info() {
    echo -e "${GREEN}[INFO]${NC} $1"
}

log_warn() {
    echo -e "${YELLOW}[WARN]${NC} $1"
}

log_error() {
    echo -e "${RED}[ERROR]${NC} $1"
}

print_header() {
    echo -e "${BLUE}=== $1 ===${NC}"
}

# Function to check if running as root
check_root() {
    if [[ $EUID -eq 0 ]]; then
        return 0
    else
        return 1
    fi
}

# Function to detect system
detect_system() {
    if [[ -f /etc/os-release ]]; then
        . /etc/os-release
        OS=$ID
        OS_VERSION=$VERSION_ID
    elif [[ -f /etc/redhat-release ]]; then
        OS="rhel"
        OS_VERSION=$(cat /etc/redhat-release | grep -o '[0-9]\+\.[0-9]\+' | head -1)
    elif [[ $(uname -s) == "Darwin" ]]; then
        OS="macos"
        OS_VERSION=$(sw_vers -productVersion)
    else
        OS="unknown"
        OS_VERSION="unknown"
    fi
    
    log_info "Detected system: $OS $OS_VERSION"
}

# Function to setup directories
setup_directories() {
    print_header "Setting up directories"
    
    local dirs=(
        "/var/log/alaris"
        "/var/lib/alaris"
        "/etc/alaris"
        "/dev/shm/alaris"
    )
    
    for dir in "${dirs[@]}"; do
        if [[ ! -d "$dir" ]]; then
            log_info "Creating directory: $dir"
            if check_root; then
                mkdir -p "$dir"
                chmod 755 "$dir"
            else
                log_warn "Cannot create $dir (requires root privileges)"
            fi
        else
            log_info "Directory already exists: $dir"
        fi
    done
}

# Function to setup user and group
setup_user() {
    print_header "Setting up user and group"
    
    if ! check_root; then
        log_warn "Skipping user setup (requires root privileges)"
        return
    fi
    
    # Create alaris group if it doesn't exist
    if ! getent group alaris >/dev/null 2>&1; then
        log_info "Creating alaris group"
        groupadd -r alaris
    else
        log_info "Group 'alaris' already exists"
    fi
    
    # Create alaris user if it doesn't exist
    if ! getent passwd alaris >/dev/null 2>&1; then
        log_info "Creating alaris user"
        useradd -r -g alaris -d /var/lib/alaris -s /bin/bash \
                -c "Alaris Trading System" alaris
    else
        log_info "User 'alaris' already exists"
    fi
    
    # Set ownership of directories
    chown -R alaris:alaris /var/log/alaris /var/lib/alaris /dev/shm/alaris 2>/dev/null || true
}

# Function to setup shared memory
setup_shared_memory() {
    print_header "Setting up shared memory"
    
    if [[ "$SYSTEM_NAME" == "Linux" ]]; then
        # Check current shared memory limits
        local shmmax=$(cat /proc/sys/kernel/shmmax 2>/dev/null || echo "0")
        local shmall=$(cat /proc/sys/kernel/shmall 2>/dev/null || echo "0")
        
        log_info "Current shared memory limits:"
        log_info "  shmmax: $(numfmt --to=iec $shmmax)"
        log_info "  shmall: $shmall pages"
        
        # Recommend increasing limits if they're too low
        local recommended_shmmax=$((1024 * 1024 * 1024))  # 1GB
        if [[ $shmmax -lt $recommended_shmmax ]]; then
            log_warn "Shared memory limit is low. Consider increasing:"
            log_warn "  echo 'kernel.shmmax = $recommended_shmmax' >> /etc/sysctl.conf"
        fi
    fi
    
    # Create shared memory directory
    mkdir -p /dev/shm/alaris 2>/dev/null || true
    chmod 777 /dev/shm/alaris 2>/dev/null || true
}

# Function to setup huge pages
setup_huge_pages() {
    print_header "Setting up huge pages"
    
    if [[ "$SYSTEM_NAME" != "Linux" ]]; then
        log_info "Huge pages not supported on $SYSTEM_NAME"
        return
    fi
    
    # Check if huge pages are available
    if [[ -f /proc/meminfo ]]; then
        local hugepages_total=$(grep HugePages_Total /proc/meminfo | awk '{print $2}')
        local hugepagesize=$(grep Hugepagesize /proc/meminfo | awk '{print $2}')
        
        if [[ $hugepages_total -gt 0 ]]; then
            log_info "Huge pages configured: $hugepages_total pages of ${hugepagesize}KB"
        else
            log_warn "Huge pages not configured"
            log_warn "To enable huge pages for better performance:"
            log_warn "  echo 1024 > /proc/sys/vm/nr_hugepages"
            log_warn "  Or add 'vm.nr_hugepages = 1024' to /etc/sysctl.conf"
        fi
    fi
}

# Function to setup real-time configuration
setup_realtime() {
    print_header "Setting up real-time configuration"
    
    if [[ "$SYSTEM_NAME" != "Linux" ]]; then
        log_info "Real-time configuration specific to Linux"
        return
    fi
    
    if ! check_root; then
        log_warn "Skipping real-time setup (requires root privileges)"
        return
    fi
    
    # Set up limits for alaris user
    local limits_file="/etc/security/limits.d/99-alaris.conf"
    
    if [[ ! -f "$limits_file" ]]; then
        log_info "Creating real-time limits configuration"
        cat > "$limits_file" << EOF
# Real-time limits for Alaris Trading System
alaris soft rtprio 99
alaris hard rtprio 99
alaris soft memlock unlimited
alaris hard memlock unlimited
alaris soft nofile 65536
alaris hard nofile 65536
EOF
        log_info "Created: $limits_file"
    else
        log_info "Real-time limits already configured"
    fi
}

# Function to install systemd service
install_systemd_service() {
    print_header "Installing systemd service"
    
    if [[ "$SYSTEM_NAME" != "Linux" ]] || ! command -v systemctl >/dev/null 2>&1; then
        log_info "Systemd not available"
        return
    fi
    
    if ! check_root; then
        log_warn "Skipping systemd service installation (requires root privileges)"
        return
    fi
    
    local service_file="/etc/systemd/system/alaris-quantlib.service"
    
    if [[ ! -f "$service_file" ]]; then
        log_info "Installing systemd service"
        cat > "$service_file" << EOF
[Unit]
Description=Alaris QuantLib Trading Process
Documentation=file://${INSTALL_PREFIX}/share/doc/alaris/README.md
After=network.target
Wants=network.target

[Service]
Type=simple
User=alaris
Group=alaris
WorkingDirectory=/var/lib/alaris
ExecStart=${INSTALL_PREFIX}/bin/quantlib_process ${INSTALL_PREFIX}/etc/alaris/quantlib_process.yaml
ExecReload=/bin/kill -HUP \$MAINPID
Restart=always
RestartSec=10
StandardOutput=journal
StandardError=journal
SyslogIdentifier=alaris-quantlib

# Performance and security settings
LimitRTPRIO=99
LimitMEMLOCK=infinity
LimitNOFILE=65536
PrivateTmp=true
ProtectSystem=strict
ProtectHome=true
NoNewPrivileges=true

# Environment
Environment=ALARIS_CONFIG_FILE=${INSTALL_PREFIX}/etc/alaris/quantlib_process.yaml
Environment=ALARIS_LOG_LEVEL=INFO

[Install]
WantedBy=multi-user.target
EOF
        
        # Reload systemd and enable service
        systemctl daemon-reload
        log_info "Systemd service installed: $service_file"
        log_info "To enable: systemctl enable alaris-quantlib"
        log_info "To start:  systemctl start alaris-quantlib"
    else
        log_info "Systemd service already installed"
    fi
}

# Function to setup log rotation
setup_log_rotation() {
    print_header "Setting up log rotation"
    
    if ! check_root; then
        log_warn "Skipping log rotation setup (requires root privileges)"
        return
    fi
    
    local logrotate_file="/etc/logrotate.d/alaris"
    
    if [[ ! -f "$logrotate_file" ]]; then
        log_info "Installing logrotate configuration"
        cat > "$logrotate_file" << EOF
/var/log/alaris/*.log {
    daily
    rotate 30
    compress
    delaycompress
    missingok
    notifempty
    create 644 alaris alaris
    postrotate
        /bin/systemctl reload alaris-quantlib 2>/dev/null || true
    endscript
}
EOF
        log_info "Created: $logrotate_file"
    else
        log_info "Log rotation already configured"
    fi
}

# Function to validate installation
validate_installation() {
    print_header "Validating installation"
    
    local errors=0
    
    # Check if main executable exists and is executable
    if [[ -x "${INSTALL_PREFIX}/bin/quantlib_process" ]]; then
        log_info "✓ Main executable found"
    else
        log_error "✗ Main executable not found or not executable"
        errors=$((errors + 1))
    fi
    
    # Check if configuration file exists
    if [[ -f "${INSTALL_PREFIX}/etc/alaris/quantlib_process.yaml" ]]; then
        log_info "✓ Configuration file found"
    else
        log_error "✗ Configuration file not found"
        errors=$((errors + 1))
    fi
    
    # Check if system info utility works
    if "${INSTALL_PREFIX}/bin/alaris_sysinfo" --health-check >/dev/null 2>&1; then
        log_info "✓ System health check passed"
    else
        log_warn "! System health check failed or returned warnings"
    fi
    
    # Check configuration validator
    if "${INSTALL_PREFIX}/bin/alaris_config_validator" "${INSTALL_PREFIX}/etc/alaris/quantlib_process.yaml" >/dev/null 2>&1; then
        log_info "✓ Configuration validation passed"
    else
        log_error "✗ Configuration validation failed"
        errors=$((errors + 1))
    fi
    
    if [[ $errors -eq 0 ]]; then
        log_info "Installation validation completed successfully"
        return 0
    else
        log_error "Installation validation failed with $errors error(s)"
        return 1
    fi
}

# Function to show next steps
show_next_steps() {
    print_header "Next Steps"
    
    echo "Alaris Trading System has been installed successfully!"
    echo ""
    echo "Configuration:"
    echo "  Edit: ${INSTALL_PREFIX}/etc/alaris/quantlib_process.yaml"
    echo "  Validate: ${INSTALL_PREFIX}/bin/alaris_config_validator <config_file>"
    echo ""
    echo "System Information:"
    echo "  Check system: ${INSTALL_PREFIX}/bin/alaris_sysinfo"
    echo "  Health check: ${INSTALL_PREFIX}/bin/alaris_sysinfo --health-check"
    echo ""
    echo "Running the System:"
    echo "  Manual: ${INSTALL_PREFIX}/bin/quantlib_process <config_file>"
    
    if command -v systemctl >/dev/null 2>&1 && check_root; then
        echo "  Service: systemctl start alaris-quantlib"
    fi
    
    echo ""
    echo "Logs:"
    echo "  Location: /var/log/alaris/"
    
    if command -v journalctl >/dev/null 2>&1; then
        echo "  Journal: journalctl -u alaris-quantlib -f"
    fi
    
    echo ""
    echo "Documentation:"
    echo "  ${INSTALL_PREFIX}/share/doc/alaris/"
    echo ""
}

# Main installation function
main() {
    print_header "Alaris Trading System Post-Installation Setup"
    echo "Version: $VERSION"
    echo "Build Type: $BUILD_TYPE"
    echo "Install Prefix: $INSTALL_PREFIX"
    echo ""
    
    detect_system
    setup_directories
    setup_user
    setup_shared_memory
    setup_huge_pages
    setup_realtime
    install_systemd_service
    setup_log_rotation
    
    echo ""
    if validate_installation; then
        show_next_steps
        exit 0
    else
        log_error "Post-installation setup completed with errors"
        exit 1
    fi
}

# Run main function
main "$@"