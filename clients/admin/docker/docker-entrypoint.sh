#!/bin/sh
# Runs at container start, before nginx starts, from /docker-entrypoint.d/ of
# the nginx image. One image runs in every Environment this way. admin and
# dashboard use the same script.
#
# 1. Writes /config.json from config.json.template: each ${PLATFORM_*} and
#    ${PUBLIC_*} in it becomes that setting's value, JSON-escaped, or "" when
#    it is unset. No other setting can reach the browser, so no secret does.
#    The app reads it in src/env.ts.
# 2. Writes the /api and /storage upstreams into the nginx configuration:
#    API_UPSTREAM and STORAGE_UPSTREAM when they are set (docker-compose.yml
#    sets http://api:8080 and http://storage:9000), otherwise the stamp's
#    http://ca-${PLATFORM_STAMP}-api and http://ca-${PLATFORM_STAMP}-storage:9000.
#    Stops with an error when neither is there.
set -eu

template=/etc/nginx/config.json.template
out=/usr/share/nginx/config/config.json

awk '
  function quote(s) {
    gsub(/\\/, "\\\\", s)
    gsub(/"/, "\\\"", s)
    gsub(/\n/, "\\n", s)
    gsub(/\r/, "\\r", s)
    gsub(/\t/, "\\t", s)
    return s
  }
  {
    line = $0
    while (match(line, /\$\{[A-Za-z0-9_]+\}/)) {
      name = substr(line, RSTART + 2, RLENGTH - 3)
      if (name !~ /^(PLATFORM|PUBLIC)_/) {
        print "config.json.template names " name ": only PLATFORM_* and PUBLIC_* settings may reach the browser" > "/dev/stderr"
        exit 1
      }
      line = substr(line, 1, RSTART - 1) quote(ENVIRON[name]) substr(line, RSTART + RLENGTH)
    }
    print line
  }
' "$template" > "$out"

echo "$0: wrote $out"

# Only a scheme, a host name and a port: the path stays the request's own.
write_upstream() {
  name=$1 upstream=$2
  if ! printf '%s' "$upstream" | grep -Eq '^https?://[A-Za-z0-9.-]+(:[0-9]+)?$'; then
    echo "$0: error: the /$name upstream '$upstream' isn't http(s)://<host>[:<port>]" >&2
    exit 1
  fi
  printf 'proxy_pass %s;\n' "$upstream" > "/etc/nginx/upstream/$name.conf"
  echo "$0: /$name goes to $upstream"
}

if [ -n "${PLATFORM_STAMP:-}" ]; then
  api=${API_UPSTREAM:-http://ca-${PLATFORM_STAMP}-api}
  storage=${STORAGE_UPSTREAM:-http://ca-${PLATFORM_STAMP}-storage:9000}
elif [ -n "${API_UPSTREAM:-}" ] && [ -n "${STORAGE_UPSTREAM:-}" ]; then
  api=$API_UPSTREAM
  storage=$STORAGE_UPSTREAM
else
  echo "$0: error: set API_UPSTREAM and STORAGE_UPSTREAM, or PLATFORM_STAMP in a stamp, so /api and /storage reach the API and the storage" >&2
  exit 1
fi

write_upstream api "$api"
write_upstream storage "$storage"
