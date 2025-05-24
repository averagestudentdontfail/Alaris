#!/bin/bash
# Production deployment script for Alaris

set -e

echo "🚀 Deploying Alaris to Production"
echo "================================="

# Check prerequisites
if ! command -v docker &> /dev/null; then
    echo "❌ Docker is required"
    exit 1
fi

if ! command -v docker-compose &> /dev/null; then
    echo "❌ Docker Compose is required"  
    exit 1
fi

# Create production directories
echo "📁 Setting up production directories..."
sudo mkdir -p /opt/alaris/{config,logs,data}
sudo mkdir -p /dev/shm/alaris
sudo chmod 777 /dev/shm/alaris

# Copy configurations
echo "📄 Copying configurations..."
sudo cp -r config/* /opt/alaris/config/
sudo cp -r monitoring /opt/alaris/

# Set permissions
sudo chown -R 1000:1000 /opt/alaris/logs
sudo chmod 755 /opt/alaris/config

# Build and deploy
echo "🐳 Building containers..."
docker-compose build --no-cache

echo "🚀 Starting services..."
docker-compose up -d

# Wait for health checks
echo "⏳ Waiting for services to be healthy..."
timeout 300 bash -c '
    while true; do
        if docker-compose ps | grep -E "(quantlib|lean)" | grep -v "Up (healthy)" | grep -q "Up"; then
            echo "Services still starting..."
            sleep 5
        else
            break
        fi
    done
'

# Verify deployment
echo "🔍 Verifying deployment..."
docker-compose ps

echo ""
echo "✅ Production deployment complete!"
echo ""
echo "🌐 Access URLs:"
echo "   Grafana:    http://localhost:3000 (admin/alaris123)"
echo "   Prometheus: http://localhost:9090"
echo ""
echo "📊 Monitor with:"
echo "   docker-compose logs -f"
echo "   docker-compose ps"
