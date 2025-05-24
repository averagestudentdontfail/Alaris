# docker/Dockerfile.lean
# Multi-stage Dockerfile for Alaris Lean Process

# Build arguments
ARG DOTNET_VERSION=8.0
ARG CONFIGURATION=Release
ARG BASE_IMAGE=mcr.microsoft.com/dotnet/sdk:${DOTNET_VERSION}

# =============================================================================
# Build Stage
# =============================================================================
FROM ${BASE_IMAGE} as build

# Build arguments
ARG CONFIGURATION
ARG DOTNET_VERSION

# Install system dependencies
RUN apt-get update && apt-get install -y \
    git \
    curl \
    unzip \
    && rm -rf /var/lib/apt/lists/*

# Set up build environment
ENV DOTNET_CLI_TELEMETRY_OPTOUT=1
ENV DOTNET_SKIP_FIRST_TIME_EXPERIENCE=1
ENV DOTNET_NOLOGO=1
ENV NUGET_XMLDOC_MODE=skip

# Create build user
RUN groupadd -r alaris && useradd -r -g alaris -u 1000 alaris
RUN mkdir -p /workspace /app && chown -R alaris:alaris /workspace /app

# Switch to build user
USER alaris
WORKDIR /workspace

# Copy project files
COPY --chown=alaris:alaris src/csharp/ ./src/csharp/
COPY --chown=alaris:alaris external/lean/ ./external/lean/

# Restore dependencies
WORKDIR /workspace/src/csharp
RUN dotnet restore Alaris.Lean.csproj

# Build the application
RUN dotnet build Alaris.Lean.csproj \
    --configuration ${CONFIGURATION} \
    --no-restore \
    --verbosity minimal

# Publish the application
RUN dotnet publish Alaris.Lean.csproj \
    --configuration ${CONFIGURATION} \
    --no-build \
    --output /app \
    --verbosity minimal \
    --self-contained false

# =============================================================================
# Runtime Stage
# =============================================================================
FROM mcr.microsoft.com/dotnet/aspnet:${DOTNET_VERSION} as runtime

# Runtime arguments
ARG DOTNET_VERSION

# Install runtime dependencies
RUN apt-get update && apt-get install -y \
    # System tools
    curl \
    wget \
    procps \
    htop \
    \
    # Networking tools
    iproute2 \
    iputils-ping \
    netcat-openbsd \
    \
    # Monitoring tools
    sysstat \
    \
    && rm -rf /var/lib/apt/lists/*

# Create runtime user and directories
RUN groupadd -r alaris && useradd -r -g alaris -u 1000 -s /bin/bash alaris
RUN mkdir -p /opt/alaris/{config,data,logs} \
    && mkdir -p /var/log/alaris \
    && mkdir -p /dev/shm/alaris \
    && chown -R alaris:alaris /opt/alaris /var/log/alaris

# Copy built application
COPY --from=build --chown=alaris:alaris /app /opt/alaris/

# Copy configuration files
COPY --chown=alaris:alaris config/lean_process.yaml /opt/alaris/config/

# Set up environment variables
ENV ASPNETCORE_ENVIRONMENT=Production
ENV DOTNET_RUNNING_IN_CONTAINER=true
ENV DOTNET_EnableDiagnostics=0

# .NET performance optimizations
ENV COMPlus_gcServer=1
ENV COMPlus_gcConcurrent=1
ENV COMPlus_GCRetainVM=1
ENV COMPlus_gcTrimCommitOnLowMemory=1

# Alaris-specific environment
ENV ALARIS_CONFIG_FILE="/opt/alaris/config/lean_process.yaml"
ENV ALARIS_LOG_LEVEL="INFO"

# Create health check script
RUN cat > /opt/alaris/health_check.sh << 'EOF'
#!/bin/bash
# Check if .NET process is responsive
dotnet --info > /dev/null 2>&1
if [ $? -eq 0 ]; then
    echo "Lean process healthy"
    exit 0
else
    echo "Lean process unhealthy"
    exit 1
fi
EOF

RUN chmod +x /opt/alaris/health_check.sh

# Health check
HEALTHCHECK --interval=30s --timeout=10s --start-period=30s --retries=3 \
    CMD ["/opt/alaris/health_check.sh"]

# Switch to runtime user
USER alaris
WORKDIR /opt/alaris

# Create startup script
RUN cat > /opt/alaris/start.sh << 'EOF'
#!/bin/bash
set -e

echo "🚀 Starting Alaris Lean Process..."
echo ".NET Runtime Info:"
dotnet --info
echo ""

echo "Configuration File: ${ALARIS_CONFIG_FILE}"
if [ -f "${ALARIS_CONFIG_FILE}" ]; then
    echo "✅ Configuration file found"
else
    echo "❌ Configuration file not found: ${ALARIS_CONFIG_FILE}"
    exit 1
fi

echo ""
echo "Environment Variables:"
echo "  ALARIS_LOG_LEVEL: ${ALARIS_LOG_LEVEL}"
echo "  DOTNET_RUNNING_IN_CONTAINER: ${DOTNET_RUNNING_IN_CONTAINER}"
echo "  COMPlus_gcServer: ${COMPlus_gcServer}"
echo ""

echo "Starting Lean process..."
exec dotnet /opt/alaris/Alaris.Lean.dll
EOF

RUN chmod +x /opt/alaris/start.sh

# Expose ports for monitoring (if needed)
EXPOSE 8081

# Set default command
CMD ["/opt/alaris/start.sh"]

# =============================================================================
# Development Stage
# =============================================================================
FROM build as development

USER root

# Install development tools
RUN apt-get update && apt-get install -y \
    # Debugging tools
    gdb \
    strace \
    \
    # Editors
    vim \
    nano \
    \
    # Additional utilities
    tree \
    jq \
    \
    && rm -rf /var/lib/apt/lists/*

# Install additional .NET tools
RUN dotnet tool install --global dotnet-counters
RUN dotnet tool install --global dotnet-dump
RUN dotnet tool install --global dotnet-trace

USER alaris

# Development environment variables
ENV ASPNETCORE_ENVIRONMENT=Development
ENV DOTNET_EnableDiagnostics=1
ENV ALARIS_LOG_LEVEL=DEBUG
ENV COMPlus_gcServer=0  # Use workstation GC for development

# Keep source code for development
WORKDIR /workspace

# Development command (keep container running)
CMD ["tail", "-f", "/dev/null"]

# =============================================================================
# Production Stage (final)
# =============================================================================
FROM runtime as production

# Production-specific optimizations
USER root

# Remove development tools to reduce attack surface
RUN apt-get update && apt-get remove -y \
    curl \
    wget \
    && apt-get autoremove -y \
    && rm -rf /var/lib/apt/lists/*

# Configure system for production
RUN echo "net.core.rmem_max = 268435456" >> /etc/sysctl.conf \
    && echo "net.core.wmem_max = 268435456" >> /etc/sysctl.conf

USER alaris

# Production environment variables
ENV ASPNETCORE_ENVIRONMENT=Production
ENV DOTNET_EnableDiagnostics=0
ENV ALARIS_LOG_LEVEL=INFO

# Production .NET optimizations
ENV COMPlus_TieredCompilation=1
ENV COMPlus_ReadyToRun=1
ENV COMPlus_gcServer=1
ENV COMPlus_GCRetainVM=1

# Labels for container identification
LABEL maintainer="Alaris Trading Systems"
LABEL version="1.0.0"
LABEL description="Alaris Lean Process - .NET trading algorithm interface"
LABEL org.opencontainers.image.source="https://github.com/alaris-trading/alaris"
LABEL org.opencontainers.image.vendor="Alaris Trading Systems"
LABEL org.opencontainers.image.title="Alaris Lean Process"
LABEL org.opencontainers.image.description="QuantConnect Lean-based trading algorithm interface for Alaris"

# =============================================================================
# Testing Stage
# =============================================================================
FROM build as testing

USER alaris
WORKDIR /workspace/src/csharp

# Run tests
RUN dotnet test Alaris.Lean.csproj \
    --configuration Release \
    --no-build \
    --verbosity normal \
    --logger "trx;LogFileName=test_results.trx" \
    --collect:"XPlat Code Coverage"

# Save test results
RUN mkdir -p /tmp/test-results \
    && cp TestResults/* /tmp/test-results/ 2>/dev/null || true

# Test stage outputs results for CI/CD pipeline
CMD ["echo", "Tests completed. Check /tmp/test-results for output."]