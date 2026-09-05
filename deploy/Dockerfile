FROM node:22-alpine
WORKDIR /app
RUN npm install ws@8
COPY server.mjs .
COPY site site
CMD ["node", "server.mjs"]
