# syntax=docker/dockerfile:1

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY Imockey/Imockey.csproj Imockey/
RUN dotnet restore Imockey/Imockey.csproj
COPY Imockey/ Imockey/
RUN dotnet publish Imockey/Imockey.csproj -c Release -o /app --no-restore -p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0
ARG TARGETARCH
ARG LITESTREAM_VERSION=0.3.13

ADD https://github.com/benbjohnson/litestream/releases/download/v${LITESTREAM_VERSION}/litestream-v${LITESTREAM_VERSION}-linux-${TARGETARCH}.tar.gz /tmp/litestream.tar.gz
RUN tar -xzf /tmp/litestream.tar.gz -C /usr/local/bin \
    && rm /tmp/litestream.tar.gz \
    && mkdir /data \
    && chown $APP_UID /data

WORKDIR /app
COPY --from=build /app .
COPY litestream.yml /etc/litestream.yml
COPY --chmod=755 docker-entrypoint.sh /usr/local/bin/docker-entrypoint.sh

# Azure Container Apps terminates TLS at the ingress; forwarded headers keep the request scheme as https
# (secure cookies, GitHub OAuth callback URL).
ENV Database__Path=/data/mocks.db \
    ASPNETCORE_FORWARDEDHEADERS_ENABLED=true

USER $APP_UID
EXPOSE 8080
ENTRYPOINT ["docker-entrypoint.sh"]
