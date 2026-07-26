FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY . .
RUN dotnet restore InvoiceCapture.slnx \
 && dotnet publish src/InvoiceCapture.Web/InvoiceCapture.Web.csproj -c Release -o /app --no-restore
FROM mcr.microsoft.com/dotnet/aspnet:10.0
USER $APP_UID
WORKDIR /app
COPY --from=build /app .
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080
ENTRYPOINT ["dotnet", "InvoiceCapture.Web.dll"]
