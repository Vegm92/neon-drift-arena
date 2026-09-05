FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
RUN curl -fsSL https://deb.nodesource.com/setup_22.x | bash - \
 && apt-get install -y --no-install-recommends nodejs \
 && rm -rf /var/lib/apt/lists/*
RUN dotnet tool install --global fable --version 5.15.0
ENV PATH="${PATH}:/root/.dotnet/tools"
WORKDIR /app
COPY package.json package-lock.json ./
RUN npm ci
COPY . .
RUN dotnet fable src -o build && npx vite build

FROM node:22-alpine
WORKDIR /app
RUN npm install ws@8
COPY deploy/server.mjs .
COPY --from=build /app/dist site
CMD ["node", "server.mjs"]
