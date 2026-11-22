# syntax=docker/dockerfile:1

# ============ Tahap 1: membangun aplikasi React ============
FROM node:22-alpine AS build
WORKDIR /app

# Paket diinstal lebih dulu: selama package*.json tidak berubah, lapisan ini dipakai ulang dari cache
COPY package.json package-lock.json ./
RUN npm ci

COPY . .

# Alamat API TERTANAM ke dalam JavaScript saat build (bukan dibaca saat jalan).
# Mengganti alamat API berarti membangun ulang image.
ARG VITE_API_URL
ENV VITE_API_URL=$VITE_API_URL
RUN test -n "$VITE_API_URL" || (echo "Build argument VITE_API_URL is required" && exit 1)

# "npm run build" menjalankan pemeriksaan TypeScript dulu: error tipe menggagalkan build image
RUN npm run build

# Menyusun konfigurasi nginx: __API_ORIGIN__ diganti asal (origin) API, untuk aturan CSP
RUN API_ORIGIN="$(node -e "console.log(new URL(process.env.VITE_API_URL).origin)")" \
    && sed "s|__API_ORIGIN__|${API_ORIGIN}|g" nginx.conf.template > /tmp/default.conf

# ============ Tahap 2: menyajikan berkas statis ============
FROM nginxinc/nginx-unprivileged:stable-alpine AS runtime

COPY --from=build /tmp/default.conf /etc/nginx/conf.d/default.conf
COPY --from=build /app/dist /usr/share/nginx/html

EXPOSE 8080