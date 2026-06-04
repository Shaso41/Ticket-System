# 1. Aşama: SDK imajı ile projeyi derleme
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build-env
WORKDIR /app

# Proje dosyalarını kopyala ve restore et
COPY *.csproj ./
RUN dotnet restore

# Tüm kodları (SQLite .db dosyan dahil) kopyala ve publish et
COPY . ./
RUN dotnet publish -c Release -o out

# 2. Aşama: Çalıştırma ortamı
FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
COPY --from=build-env /app/out .

# Render'ın port ayarı
ENV ASPNETCORE_URLS=http://+:10000
EXPOSE 10000

# ProjeAdiniz.dll kısmını kendi projenin adı neyse onunla değiştir!
ENTRYPOINT ["dotnet", "TicketSistemi.dll"]