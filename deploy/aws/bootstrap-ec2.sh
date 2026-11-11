#!/usr/bin/env bash
# One-time server setup for Amazon Linux 2023. Run as ec2-user:  bash bootstrap-ec2.sh
set -euo pipefail

echo "==> Packages"
sudo dnf install -y docker git cronie
sudo systemctl enable --now docker crond
sudo usermod -aG docker "$USER"

echo "==> Docker Compose plugin"
ARCH="$(uname -m)"
sudo mkdir -p /usr/local/lib/docker/cli-plugins
sudo curl -fsSL "https://github.com/docker/compose/releases/download/v2.29.7/docker-compose-linux-${ARCH}" \
  -o /usr/local/lib/docker/cli-plugins/docker-compose
sudo chmod +x /usr/local/lib/docker/cli-plugins/docker-compose

echo "==> 2 GB swap (building .NET and Angular on a small instance needs it)"
if ! swapon --show | grep -q /swapfile; then
  sudo dd if=/dev/zero of=/swapfile bs=1M count=2048 status=none
  sudo chmod 600 /swapfile
  sudo mkswap /swapfile >/dev/null
  sudo swapon /swapfile
  echo '/swapfile none swap sw 0 0' | sudo tee -a /etc/fstab >/dev/null
fi

echo "==> Data directory"
sudo mkdir -p /srv/mieu/pgdata /srv/mieu/backups
sudo chown -R "$USER":"$USER" /srv/mieu

echo "Done. Log out and back in (docker group), then follow docs/AWS-DEPLOY.md step 6."
