#!/usr/bin/env bash
set -euo pipefail
# Run on the configured deployment host from the release directory.
# Required arguments are immutable image references from this build.
: "${1:?API image digest required}" "${2:?Admin image digest required}"
export API_IMAGE="$1" ADMIN_IMAGE="$2"
test -f .env.runtime
# The operator owns database backup/restore; additive upgrade only, never reset demo.
test -n "${BACKUP_CONFIRMED_AT:-}" || { echo 'Set BACKUP_CONFIRMED_AT after verifying a restorable database backup.'; exit 1; }
docker compose pull
old_api=$(docker inspect --format '{{.Config.Image}}' "$(docker compose ps -q api)" 2>/dev/null || true)
old_admin=$(docker inspect --format '{{.Config.Image}}' "$(docker compose ps -q admin)" 2>/dev/null || true)
docker compose run --rm --no-deps --entrypoint dotnet api database/Database.dll upgrade /app/sql
docker compose up -d --remove-orphans
healthy=false
for attempt in $(seq 1 24); do
 if curl -fsS http://127.0.0.1:8080/health/ready >/dev/null && curl -fsS http://127.0.0.1:8081/health/live >/dev/null; then healthy=true; break; fi
 sleep 5
done
if [ "$healthy" != true ]; then
 if [ -n "$old_api" ] && [ -n "$old_admin" ]; then API_IMAGE="$old_api" ADMIN_IMAGE="$old_admin" docker compose up -d; fi
 echo 'Readiness failed; previous application images restored where available. Database schema remains additive.'; exit 1
fi
printf 'API_IMAGE=%s\nADMIN_IMAGE=%s\n' "$API_IMAGE" "$ADMIN_IMAGE" > .release-images
