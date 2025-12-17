
docker run --rm -it -d -p 18888:18888 -p 4317:18889 --name aspire-dashboard -e ASPIRE_DASHBOARD_UNSECURED_ALLOW_ANONYMOUS=true mcr.microsoft.com/dotnet/aspire-dashboard:latest