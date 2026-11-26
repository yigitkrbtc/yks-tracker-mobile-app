#!/usr/bin/env bash
# One-off server preparation on Ubuntu 24.04 (ADR-0022 §1, docs/isletim.md §2). Run as root after SSH is key-only.
# Idempotent: safe to run again. Usage: sudo ./bootstrap.sh
set -euo pipefail

if [[ $(id -u) -ne 0 ]]; then
  echo "Run as root (sudo)." >&2
  exit 1
fi

export DEBIAN_FRONTEND=noninteractive
timedatectl set-timezone UTC
apt-get update -q

# Docker Engine and the compose plugin from Docker's apt repository.
if ! command -v docker >/dev/null; then
  apt-get install -y -q ca-certificates curl
  install -m 0755 -d /etc/apt/keyrings
  curl -fsSL https://download.docker.com/linux/ubuntu/gpg -o /etc/apt/keyrings/docker.asc
  chmod a+r /etc/apt/keyrings/docker.asc
  # shellcheck source=/dev/null
  codename=$(. /etc/os-release && echo "$VERSION_CODENAME")
  echo "deb [arch=$(dpkg --print-architecture) signed-by=/etc/apt/keyrings/docker.asc] https://download.docker.com/linux/ubuntu $codename stable" \
    > /etc/apt/sources.list.d/docker.list
  apt-get update -q
  apt-get install -y -q docker-ce docker-ce-cli containerd.io docker-compose-plugin
fi

# Firewall: SSH, HTTP, HTTPS (TCP and UDP for HTTP/3). Docker-published ports bypass ufw, hence "only Caddy
# publishes ports" in compose.prod.yml.
apt-get install -y -q ufw unattended-upgrades age jq
# Allow the port sshd really listens on before enabling the firewall, or this session is cut off.
ssh_port=$(sshd -T 2>/dev/null | awk '/^port /{print $2; exit}')
ssh_port=${ssh_port:-22}
ufw default deny incoming
ufw default allow outgoing
ufw limit "$ssh_port/tcp"
ufw allow 80/tcp
ufw allow 443/tcp
ufw allow 443/udp
ufw --force enable

# Security updates only; reboot if needed at 04:30 UTC (after the 03:30 backup).
cat > /etc/apt/apt.conf.d/52nirengi-unattended <<'CONF'
// Ubuntu security updates are allowed by default; Docker's repository is not, so add it (engine, containerd).
Unattended-Upgrade::Allowed-Origins { "Docker:${distro_codename}"; };
Unattended-Upgrade::Automatic-Reboot "true";
Unattended-Upgrade::Automatic-Reboot-Time "04:30";
CONF
cat > /etc/apt/apt.conf.d/20auto-upgrades <<'CONF'
APT::Periodic::Update-Package-Lists "1";
APT::Periodic::Unattended-Upgrade "1";
CONF

# Layout: everything under /opt/nirengi; secrets readable by root only (files get per-container owners).
install -d -m 0755 /opt/nirengi /opt/nirengi/bin /opt/nirengi/sql
install -d -m 0700 /opt/nirengi/secrets
install -d -m 0500 -o 1654 /opt/nirengi/secrets/api /opt/nirengi/secrets/migrate   # APP_UID of the API image
install -d -m 0500 -o 70 /opt/nirengi/secrets/postgres                            # postgres (alpine)
install -d -m 0750 /var/backups/nirengi

# The CI deploy user: not in the docker group (that is root-equivalent); may only run deploy.sh via sudo.
if ! id deploy >/dev/null 2>&1; then
  useradd --create-home --shell /bin/bash deploy
fi
install -d -m 0700 -o deploy -g deploy /home/deploy/.ssh
# Checked before it is installed: a broken sudoers file would lock the administrator out (root login is off).
sudoers=$(mktemp)
echo 'deploy ALL=(root) NOPASSWD: /opt/nirengi/bin/deploy.sh' > "$sudoers"
visudo -cf "$sudoers"
install -m 0440 -o root -g root "$sudoers" /etc/sudoers.d/nirengi-deploy
rm -f "$sudoers"
# deploy.sh runs as root through sudo: nothing under bin/ may be writable by anyone else.
chown -R root:root /opt/nirengi/bin
chmod -R go-w /opt/nirengi/bin

echo "Done. Next: docs/isletim.md §2 step 3 (deploy key), 4 (GHCR login), 5 (secrets)."
