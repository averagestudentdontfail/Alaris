#!/bin/bash
# Development deployment for Alaris

set -e

echo "🔧 Deploying Alaris for Development"
echo "==================================="

# Build first
echo "🔨 Building system..."
./scripts/build.sh

# Setup shared memory
echo "🔧 Setting up shared memory..."
sudo mkdir -p /dev/shm/alaris
sudo chmod 777 /dev/shm/alaris

# Start with Docker Compose
echo "🐳 Starting development environment..."
docker-compose -f docker-compose.yml up --build -d

echo ""
echo "✅ Development environment ready!"
echo ""
echo "🔍 Check status: docker-compose ps"
echo "📊 View logs: docker-compose logs -f"
echo "🌐 Grafana: http://localhost:3000"
