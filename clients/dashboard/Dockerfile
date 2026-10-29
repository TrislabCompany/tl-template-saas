# The dashboard image. Stage 1 builds dist/ with npm. Stage 2 is unprivileged nginx
# serving dist/ on port 8080 as a non-root user, and proxying /api to the api
# app and /storage to the stamp's storage service. Both base images are pinned
# by digest; Renovate bumps them from roadmap phase 12 on. The build context
# is clients/dashboard (platform.yaml).

FROM node:22.23.4-alpine@sha256:fe4f6d74e20370acb04ad889077d14d0f64a9c8ea6a42bc60cddf1e844699914 AS build
WORKDIR /app
COPY package.json package-lock.json ./
RUN npm ci
COPY . .
RUN npm run build

FROM nginxinc/nginx-unprivileged:1.30.6-alpine@sha256:f5116ed4606f3eb015dd4a8e28cb9b351476dc0ad4bc6e9f93b19cec130cdf03
# Removes what the app never uses, so the image has less to scan and patch:
# curl, and the dynamic modules nginx doesn't load here. Then creates the
# folders docker-entrypoint.sh writes /config.json and the upstreams into,
# owned by the nginx user.
USER root
RUN apk del --no-network curl nginx-module-geoip nginx-module-image-filter nginx-module-njs nginx-module-xslt     && install -d -o nginx -g nginx /usr/share/nginx/config /etc/nginx/upstream
USER nginx
COPY docker/nginx.conf /etc/nginx/conf.d/default.conf
COPY docker/config.json.template /etc/nginx/config.json.template
COPY --chmod=755 docker/docker-entrypoint.sh /docker-entrypoint.d/40-config.sh
COPY --from=build /app/dist /usr/share/nginx/html
EXPOSE 8080
