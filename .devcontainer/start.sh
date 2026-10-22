#!/bin/sh
# Starts the stack. In Codespaces the browser reaches each port on its own URL,
# so the web gets the API's forwarded URL and the API accepts the web's origin.
set -e
if [ -n "$CODESPACE_NAME" ]; then
  domain="${GITHUB_CODESPACES_PORT_FORWARDING_DOMAIN:-app.github.dev}"
  export API_BASE_URL="https://$CODESPACE_NAME-8080.$domain"
  export WEB_ORIGIN="https://$CODESPACE_NAME-8081.$domain"
fi
docker compose up -d --build
if [ -n "$CODESPACE_NAME" ]; then
  # The web calls the API from the browser, which only works if the API port is public.
  gh codespace ports visibility 8080:public -c "$CODESPACE_NAME" || echo "Make port 8080 public in the Ports tab so the web can reach the API."
fi
