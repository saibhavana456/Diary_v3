# Use the official .NET 8 SDK image to build the application
FROM harbormz.unionbankofindia.co.in/base-image/dotnet-8.0-sdk-secure:v3 AS build-env

# Set the working directory inside the container
WORKDIR /app

# Create non-root group and user
RUN addgroup -g 1000 mygroup && adduser -u 1000 -G mygroup -S myuser

RUN echo "myuser ALL =(ALL) NOPASSWD: ALL" >> /etc/sudoers

# Copy solution, projects, source code, NuGet config and packages
COPY . .

# Give ownership to non-root user
RUN chown -R myuser:mygroup /app && chmod -R 755 /app

# Switch to non-root user
USER myuser
# Restore dependencies using the nuget.config file
RUN dotnet restore My_Diary_V3.sln --configfile /app/nuget.config --source /app/packages

# Build the application
RUN dotnet publish src/MyDiary.Web/MyDiary.Web.csproj --configuration Release --output /app/out --no-restore

# Use the official .NET 8 runtime image for the runtime environment
FROM harbormz.unionbankofindia.co.in/base-image/dotnet-8.0-asp-secure:v3

# Set the working directory inside the container
WORKDIR /app

ENV DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=false

# Create non-root group and user
RUN addgroup -g 1000 mygroup && adduser -u 1000 -G mygroup -S myuser

RUN echo "myuser ALL =(ALL) NOPASSWD: ALL" >> /etc/sudoers

# Copy published application
COPY --from=build-env /app/out/ ./

#COPY --from=build-env /app/src/MyDiary.Web/fonts/ /usr/share/fonts/truetype/custom/
#RUN chmod -R 755 /usr/share/fonts

# Set ownership and permissions
RUN chown -R myuser:mygroup /app && chmod -R 755 /app

# Run application as non-root user
USER myuser

# Application port
EXPOSE 8080

# Define the entry point for the application
ENTRYPOINT ["dotnet", "MyDiary.Web.dll"]