FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY . .
ARG TARGETARCH
RUN case "${TARGETARCH}" in \
      amd64) runtime_identifier=linux-x64 ;; \
      arm64) runtime_identifier=linux-arm64 ;; \
      *) echo "Unsupported target architecture: ${TARGETARCH}" >&2; exit 1 ;; \
    esac \
 && dotnet publish src/InvoiceCapture.Worker/InvoiceCapture.Worker.csproj -c Release \
      -r "${runtime_identifier}" --self-contained false -o /app \
      -p:PublishReadyToRun=true \
      -p:PublishReadyToRunShowWarnings=true \
      -p:PublishTrimmed=false
FROM mcr.microsoft.com/dotnet/runtime:10.0
USER $APP_UID
WORKDIR /app
COPY --from=build /app .
ENTRYPOINT ["dotnet", "InvoiceCapture.Worker.dll"]
