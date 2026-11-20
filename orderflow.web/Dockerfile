# Etapa 1: Compilación de producción con Node 22
FROM node:22-alpine AS build
WORKDIR /app

ARG VITE_API_URL=http://localhost:5080
ENV VITE_API_URL=$VITE_API_URL

COPY package*.json ./
RUN npm ci

COPY . .
RUN npm run build

# Etapa 2: Servidor web Nginx liviano
FROM nginx:alpine AS final
EXPOSE 80
COPY --from=build /app/dist /usr/share/nginx/html