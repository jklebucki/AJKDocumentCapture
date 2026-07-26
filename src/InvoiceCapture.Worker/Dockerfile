FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY . .
RUN dotnet restore InvoiceCapture.slnx \
 && dotnet publish src/InvoiceCapture.Worker/InvoiceCapture.Worker.csproj -c Release -o /app --no-restore
FROM mcr.microsoft.com/dotnet/runtime:10.0
USER $APP_UID
WORKDIR /app
COPY --from=build /app .
ENTRYPOINT ["dotnet", "InvoiceCapture.Worker.dll"]
