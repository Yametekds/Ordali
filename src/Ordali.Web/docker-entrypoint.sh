#!/bin/sh
# Points the app at the API given in API_BASE_URL. Without it the image runs in demo mode.
set -e
settings=/usr/share/nginx/html/appsettings.json
printf '{\n  "Api": {\n    "BaseUrl": "%s"\n  }\n}\n' "${API_BASE_URL:-}" > "$settings"
# The precompressed copies would still hold the old value.
rm -f "$settings.gz" "$settings.br"
