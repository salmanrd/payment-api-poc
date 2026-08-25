FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY PaymentApi.sln .
COPY src/PaymentApi/PaymentApi.csproj src/PaymentApi/
RUN dotnet restore src/PaymentApi/PaymentApi.csproj
COPY src/PaymentApi src/PaymentApi
RUN dotnet publish src/PaymentApi/PaymentApi.csproj -c Release -o /app --no-restore
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app
COPY --from=build /app .
USER $APP_UID
ENV PORT=8080
EXPOSE 8080
ENTRYPOINT ["dotnet", "PaymentApi.dll"]
