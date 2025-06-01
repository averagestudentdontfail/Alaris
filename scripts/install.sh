#!/bin/bash

# Local IB Gateway Installation Script for Alaris Project
# Installs IB Gateway into external/gateway/ alongside other dependencies

set -e  # Exit on any error

# Colors for output
GREEN='\033[0;32m'
RED='\033[0;31m'
YELLOW='\033[1;33m'
BLUE='\033[0;34m'
NC='\033[0m' # No Color

log_info() { echo -e "${GREEN}[INFO]${NC} $1"; }
log_error() { echo -e "${RED}[ERROR]${NC} $1"; }
log_warn() { echo -e "${YELLOW}[WARN]${NC} $1"; }
log_note() { echo -e "${BLUE}[NOTE]${NC} $1"; }

# Project configuration
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PROJECT_ROOT="$(cd "$SCRIPT_DIR" && git rev-parse --show-toplevel 2>/dev/null || echo "$SCRIPT_DIR")"
EXTERNAL_DIR="$PROJECT_ROOT/external"
GATEWAY_DIR="$EXTERNAL_DIR/gateway"
JAVA_HOME_PATH="/usr/lib/jvm/java-11-openjdk-amd64"

# IB Gateway configuration
IB_GATEWAY_VERSION="latest-standalone"
IB_GATEWAY_URL="https://download2.interactivebrokers.com/installers/ibgateway/${IB_GATEWAY_VERSION}/ibgateway-${IB_GATEWAY_VERSION}-linux-x64.sh"

echo "=== Local IB Gateway Installation for Alaris Project ==="
echo
log_info "Installing IB Gateway into: $GATEWAY_DIR"
log_info "Project Root: $PROJECT_ROOT"
echo

# Function to verify project structure
verify_project_structure() {
    log_info "Verifying Alaris project structure..."
    
    if [ ! -d "$EXTERNAL_DIR" ]; then
        log_error "External directory not found: $EXTERNAL_DIR"
        log_error "Please run this script from your Alaris project root"
        exit 1
    fi
    
    log_info "✅ Found external directory: $EXTERNAL_DIR"
    
    # Show current external dependencies
    echo "   Current external dependencies:"
    if [ -d "$EXTERNAL_DIR" ]; then
        for dir in "$EXTERNAL_DIR"/*; do
            if [ -d "$dir" ]; then
                echo "   • $(basename "$dir")"
            fi
        done
    fi
    echo
}

# Function to install prerequisites
install_prerequisites() {
    log_info "Installing prerequisites..."
    
    # Update package list
    sudo apt update
    
    # Install Java (required for IB Gateway)
    log_info "Installing Java 11..."
    sudo apt install -y openjdk-11-jdk
    
    # Install X11 utilities (for GUI)
    log_info "Installing X11 components..."
    sudo apt install -y xorg x11-apps
    
    # Install additional utilities
    log_info "Installing additional utilities..."
    sudo apt install -y wget curl unzip
    
    # Verify Java installation
    if command -v java &> /dev/null; then
        JAVA_VERSION=$(java -version 2>&1 | head -1)
        log_info "✅ Java installed: $JAVA_VERSION"
    else
        log_error "❌ Java installation failed"
        exit 1
    fi
}

# Function to configure X11 for WSL
configure_x11_wsl() {
    if grep -q "microsoft" /proc/version 2>/dev/null; then
        log_info "Configuring X11 for WSL..."
        
        # Check if already configured
        if ! grep -q "DISPLAY.*resolv.conf" ~/.bashrc 2>/dev/null; then
            {
                echo ""
                echo "# X11 Configuration for WSL (IB Gateway)"
                echo "export DISPLAY=\$(cat /etc/resolv.conf | grep nameserver | awk '{print \$2}'):0"
                echo "export LIBGL_ALWAYS_INDIRECT=1"
            } >> ~/.bashrc
            log_info "✅ X11 configuration added to ~/.bashrc"
        else
            log_info "✅ X11 already configured in ~/.bashrc"
        fi
        
        # Apply configuration to current session
        export DISPLAY=$(cat /etc/resolv.conf | grep nameserver | awk '{print $2}'):0
        export LIBGL_ALWAYS_INDIRECT=1
        
        log_warn "📋 WSL GUI REQUIREMENT:"
        echo "   Install VcXsrv on Windows: https://sourceforge.net/projects/vcxsrv/"
        echo "   Or use Windows 11 WSLg (built-in GUI support)"
        echo
    fi
}

# Function to download and install IB Gateway locally
install_ib_gateway_local() {
    log_info "Creating gateway directory..."
    mkdir -p "$GATEWAY_DIR"
    cd "$GATEWAY_DIR"
    
    # Download IB Gateway installer
    log_info "Downloading IB Gateway for Linux..."
    log_info "URL: $IB_GATEWAY_URL"
    
    if wget -O "ibgateway-installer.sh" "$IB_GATEWAY_URL"; then
        log_info "✅ IB Gateway downloaded successfully"
        chmod +x "ibgateway-installer.sh"
    else
        log_error "❌ Failed to download IB Gateway"
        log_error "You can manually download from: https://www.interactivebrokers.com/en/trading/ib-api.php"
        exit 1
    fi
    
    # Create custom installation directory
    INSTALL_TARGET="$GATEWAY_DIR/ibgateway"
    mkdir -p "$INSTALL_TARGET"
    
    log_info "Installing IB Gateway to: $INSTALL_TARGET"
    
    # Install IB Gateway with custom target directory
    # Note: IB Gateway installer may still create ~/Jts, so we'll move it
    log_info "Running IB Gateway installer..."
    
    # Try silent installation first
    if ./ibgateway-installer.sh -q; then
        log_info "✅ Silent installation completed"
    else
        log_warn "Silent installation failed, trying interactive..."
        ./ibgateway-installer.sh
    fi
    
    # Move installation from default location to our project directory
    if [ -d "$HOME/Jts" ]; then
        log_info "Moving IB Gateway from ~/Jts to project directory..."
        cp -r "$HOME/Jts"/* "$INSTALL_TARGET/"
        rm -rf "$HOME/Jts"
        log_info "✅ IB Gateway moved to: $INSTALL_TARGET"
    elif [ -d "$HOME/IBJts" ]; then
        log_info "Moving IB Gateway from ~/IBJts to project directory..."
        cp -r "$HOME/IBJts"/* "$INSTALL_TARGET/"
        rm -rf "$HOME/IBJts"
        log_info "✅ IB Gateway moved to: $INSTALL_TARGET"
    else
        log_warn "Default installation directory not found, checking current directory..."
        if [ -f "./ibgateway" ]; then
            log_info "✅ IB Gateway installed locally"
        else
            log_error "❌ IB Gateway installation not found"
            exit 1
        fi
    fi
    
    # Verify installation
    if [ -f "$INSTALL_TARGET/ibgateway" ]; then
        log_info "✅ IB Gateway successfully installed to: $INSTALL_TARGET"
    else
        log_error "❌ IB Gateway executable not found at: $INSTALL_TARGET"
        exit 1
    fi
}

# Function to create project-specific startup scripts
create_project_scripts() {
    log_info "Creating project-specific startup scripts..."
    
    # Create startup script in project root
    cat > "$PROJECT_ROOT/start_ib_gateway.sh" << EOF
#!/bin/bash

# IB Gateway startup script for Alaris Project
# This script starts IB Gateway from the local installation

# Colors
GREEN='\033[0;32m'
RED='\033[0;31m'
YELLOW='\033[1;33m'
NC='\033[0m'

log_info() { echo -e "\${GREEN}[INFO]\${NC} \$1"; }
log_error() { echo -e "\${RED}[ERROR]\${NC} \$1"; }
log_warn() { echo -e "\${YELLOW}[WARN]\${NC} \$1"; }

# Project paths
PROJECT_ROOT="\$(cd "\$(dirname "\${BASH_SOURCE[0]}")" && pwd)"
GATEWAY_DIR="\$PROJECT_ROOT/external/gateway/ibgateway"

# Set Java environment
export JAVA_HOME="$JAVA_HOME_PATH"

# Set X11 display for WSL (if applicable)
if grep -q "microsoft" /proc/version 2>/dev/null; then
    export DISPLAY=\$(cat /etc/resolv.conf | grep nameserver | awk '{print \$2}'):0
    export LIBGL_ALWAYS_INDIRECT=1
    log_info "WSL detected - X11 configured"
fi

# Verify IB Gateway installation
if [ ! -f "\$GATEWAY_DIR/ibgateway" ]; then
    log_error "IB Gateway not found at: \$GATEWAY_DIR/ibgateway"
    log_error "Please run: ./scripts/install_ib_gateway.sh"
    exit 1
fi

# Change to IB Gateway directory
cd "\$GATEWAY_DIR"

# Display startup information
log_info "Starting IB Gateway..."
log_info "Project: \$PROJECT_ROOT"
log_info "Gateway: \$GATEWAY_DIR"
log_info "Java: \$JAVA_HOME"
log_info "Display: \$DISPLAY"

# Start IB Gateway
log_info "Launching IB Gateway GUI..."
./ibgateway

EOF
    
    chmod +x "$PROJECT_ROOT/start_ib_gateway.sh"
    log_info "✅ Created: $PROJECT_ROOT/start_ib_gateway.sh"
    
    # Create convenient script in scripts directory
    cat > "$PROJECT_ROOT/scripts/start_ib_gateway.sh" << EOF
#!/bin/bash
# Convenience script to start IB Gateway from scripts directory
"\$(dirname "\$0")/../start_ib_gateway.sh"
EOF
    
    chmod +x "$PROJECT_ROOT/scripts/start_ib_gateway.sh"
    log_info "✅ Created: $PROJECT_ROOT/scripts/start_ib_gateway.sh"
}

# Function to update Alaris configuration
update_alaris_config() {
    log_info "Updating Alaris configuration for localhost..."
    
    # Update lean_process.yaml
    LEAN_CONFIG="$PROJECT_ROOT/config/lean_process.yaml"
    if [ -f "$LEAN_CONFIG" ]; then
        # Backup original
        cp "$LEAN_CONFIG" "${LEAN_CONFIG}.backup.$(date +%Y%m%d_%H%M%S)"
        
        # Update gateway_host to localhost
        sed -i 's/gateway_host: .*/gateway_host: "127.0.0.1"/' "$LEAN_CONFIG"
        log_info "✅ Updated $LEAN_CONFIG to use localhost"
    else
        log_warn "Configuration file $LEAN_CONFIG not found"
    fi
    
    # Update run.sh
    RUN_SCRIPT="$PROJECT_ROOT/scripts/run.sh"
    if [ -f "$RUN_SCRIPT" ]; then
        # Backup original
        cp "$RUN_SCRIPT" "${RUN_SCRIPT}.backup.$(date +%Y%m%d_%H%M%S)"
        
        # Update IB_GATEWAY_HOSTS to use localhost
        sed -i 's/IB_GATEWAY_HOSTS=.*/IB_GATEWAY_HOSTS=("localhost" "127.0.0.1")/' "$RUN_SCRIPT"
        log_info "✅ Updated $RUN_SCRIPT to use localhost"
    else
        log_warn "Run script $RUN_SCRIPT not found"
    fi
}

# Function to create local test script
create_test_script() {
    log_info "Creating connection test script..."
    
    cat > "$PROJECT_ROOT/test_ib_gateway.sh" << 'EOF'
#!/bin/bash

# Test script for local IB Gateway connection in Alaris project

# Colors
GREEN='\033[0;32m'
RED='\033[0;31m'
YELLOW='\033[1;33m'
NC='\033[0m'

log_info() { echo -e "${GREEN}[INFO]${NC} $1"; }
log_error() { echo -e "${RED}[ERROR]${NC} $1"; }
log_warn() { echo -e "${YELLOW}[WARN]${NC} $1"; }

PROJECT_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
GATEWAY_DIR="$PROJECT_ROOT/external/gateway/ibgateway"

echo "=== Alaris IB Gateway Connection Test ==="
echo "Project: $PROJECT_ROOT"
echo

# Test 1: Check if IB Gateway is installed locally
echo "1. Checking local IB Gateway installation..."
if [ -f "$GATEWAY_DIR/ibgateway" ]; then
    log_info "✅ IB Gateway found at: $GATEWAY_DIR"
else
    log_error "❌ IB Gateway not found at: $GATEWAY_DIR"
    echo "   Install with: ./scripts/install_ib_gateway.sh"
fi

# Test 2: Check if IB Gateway process is running
echo "2. Checking if IB Gateway is running..."
if pgrep -f "ibgateway\|gateway" > /dev/null; then
    log_info "✅ IB Gateway process found"
    echo "   Running processes:"
    ps aux | grep -E "(ibgateway|gateway)" | grep -v grep | head -2
else
    log_warn "❌ IB Gateway process not found"
    echo "   Start with: ./start_ib_gateway.sh"
fi

# Test 3: Check if port 4002 is listening
echo "3. Checking if port 4002 is listening..."
if lsof -i :4002 &>/dev/null || netstat -ln 2>/dev/null | grep -q ":4002 "; then
    log_info "✅ Port 4002 is listening"
    echo "   Listener details:"
    lsof -i :4002 2>/dev/null || netstat -ln | grep ":4002 "
else
    log_warn "❌ Port 4002 is not listening"
    echo "   Configure API in IB Gateway: Configure → Settings → API"
fi

# Test 4: Test connection to IB Gateway
echo "4. Testing connection to localhost:4002..."
if timeout 5 nc -z localhost 4002 2>/dev/null; then
    log_info "✅ Successfully connected to IB Gateway!"
    echo "   🎉 Ready to run Alaris trading system!"
else
    log_error "❌ Cannot connect to IB Gateway on localhost:4002"
fi

echo
echo "=== SUMMARY ==="
if timeout 5 nc -z localhost 4002 2>/dev/null; then
    log_info "🎯 ALL TESTS PASSED - Run your trading system:"
    echo "   ./scripts/run.sh"
else
    log_warn "❌ Setup incomplete. Next steps:"
    echo "   1. Start IB Gateway: ./start_ib_gateway.sh"
    echo "   2. Login with account: DUE407919"
    echo "   3. Configure API: Port 4002, enable socket clients"
    echo "   4. Test again: ./test_ib_gateway.sh"
fi
echo

EOF
    
    chmod +x "$PROJECT_ROOT/test_ib_gateway.sh"
    log_info "✅ Created: $PROJECT_ROOT/test_ib_gateway.sh"
}

# Function to create .gitignore entry
update_gitignore() {
    log_info "Updating .gitignore for IB Gateway..."
    
    GITIGNORE="$PROJECT_ROOT/.gitignore"
    
    # Add IB Gateway entries to .gitignore if not already present
    if [ -f "$GITIGNORE" ]; then
        if ! grep -q "external/gateway" "$GITIGNORE" 2>/dev/null; then
            {
                echo ""
                echo "# IB Gateway installation (binary)"
                echo "external/gateway/"
                echo ""
                echo "# IB Gateway logs and temporary files"
                echo "*.log"
                echo "ibgateway-installer.sh"
            } >> "$GITIGNORE"
            log_info "✅ Updated .gitignore"
        else
            log_info "✅ .gitignore already contains gateway entries"
        fi
    else
        log_warn ".gitignore not found - consider adding external/gateway/ to .gitignore"
    fi
}

# Function to display final instructions
show_final_instructions() {
    echo
    log_info "=== LOCAL IB GATEWAY INSTALLATION COMPLETE ==="
    echo
    log_info "📁 INSTALLATION DIRECTORY:"
    echo "   $GATEWAY_DIR"
    echo
    log_info "🚀 QUICK START:"
    echo "   1. Start IB Gateway: ./start_ib_gateway.sh"
    echo "   2. Login: DUE407919 (Paper Trading)"
    echo "   3. Configure API: Port 4002, Socket Clients enabled"
    echo "   4. Test: ./test_ib_gateway.sh"
    echo "   5. Run Alaris: ./scripts/run.sh"
    echo
    log_info "📋 PROJECT STRUCTURE:"
    echo "   external/"
    echo "   ├── lean/              # Lean submodule"
    echo "   ├── quant/             # QuantLib submodule"
    echo "   ├── yaml-cpp/          # YAML library submodule"
    echo "   └── gateway/           # IB Gateway (local install)"
    echo "       └── ibgateway/     # IB Gateway executable & files"
    echo
    log_info "✅ ADVANTAGES:"
    echo "   • Self-contained project (all dependencies local)"
    echo "   • No home directory clutter"
    echo "   • Easy to backup/restore entire trading system"
    echo "   • Portable between environments"
    echo "   • Git-friendly (gateway/ in .gitignore)"
    echo
    
    if grep -q "microsoft" /proc/version 2>/dev/null; then
        log_warn "🖥️  WSL USERS:"
        echo "   Install VcXsrv on Windows for IB Gateway GUI"
        echo "   Or use Windows 11 WSLg (built-in)"
        echo
    fi
    
    log_info "🗂️  SCRIPTS CREATED:"
    echo "   • ./start_ib_gateway.sh - Start IB Gateway"
    echo "   • ./scripts/start_ib_gateway.sh - Convenience script"
    echo "   • ./test_ib_gateway.sh - Connection test"
    echo
}

# Main installation function
main() {
    log_info "Installing IB Gateway locally into Alaris project..."
    echo
    
    # Verify we're in the right place
    verify_project_structure
    
    # Check if user wants to proceed
    read -p "Install IB Gateway to external/gateway/? (y/N): " -n 1 -r
    echo
    if [[ ! $REPLY =~ ^[Yy]$ ]]; then
        log_info "Installation cancelled"
        exit 0
    fi
    
    # Install prerequisites
    install_prerequisites
    
    # Configure X11 for WSL if needed
    configure_x11_wsl
    
    # Install IB Gateway locally
    install_ib_gateway_local
    
    # Create project-specific scripts
    create_project_scripts
    
    # Update Alaris configuration
    update_alaris_config
    
    # Create test script
    create_test_script
    
    # Update .gitignore
    update_gitignore
    
    # Display final instructions
    show_final_instructions
    
    log_info "🎉 Local installation completed successfully!"
    echo
    log_info "Next: ./start_ib_gateway.sh"
}

# Run main function
main "$@"