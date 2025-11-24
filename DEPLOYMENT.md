# Deployment Guide

This document provides guidance for deploying MyDigitalLibrary to Azure.

## Azure App Service Configuration

The application is deployed to Azure App Service using GitHub Actions. The deployment workflow is defined in `.github/workflows/main_mydigitallibrary.yml`.

### App Service Name

The Azure App Service name is **mydigitallibrary** (lowercase). This must match the name configured in the Azure Portal.

## Configuration and Secrets

### Local Development

During local development, you can use `dotnet user-secrets` to manage sensitive configuration values like connection strings and API keys:

```bash
dotnet user-secrets set "ConnectionStrings:DefaultConnection" "your-connection-string"
dotnet user-secrets set "AzureStorage:ConnectionString" "your-storage-connection-string"
```

### Azure Deployment

**Important:** Secrets and connection strings configured with `dotnet user-secrets` are only stored locally and are **not** published to Azure during deployment.

For cloud deployment, you must configure these settings in the Azure Portal:

1. Navigate to your App Service in the Azure Portal
2. Go to **Settings > Configuration**
3. Add your connection strings under the **Connection strings** section
4. Add other application settings under the **Application settings** section

This ensures:
- Secrets remain secure and are not included in the deployment package
- Configuration is managed separately for each environment
- The application works correctly in the cloud

### Required Configuration

Common settings that need to be configured in Azure App Settings or Connection Strings:

- **Connection Strings:**
  - `DefaultConnection` - Azure SQL Database connection string
  - `AzureStorage:ConnectionString` - Azure Blob Storage connection string
  - `AzureQueue:ConnectionString` - Azure Queue Storage connection string (if different from Blob Storage)

- **Application Settings:**
  - Authentication and authorization settings
  - Azure-specific configuration values
  - Any other environment-specific settings

## Deployment Process

The GitHub Actions workflow automatically:
1. Builds the .NET application
2. Publishes the build artifacts
3. Deploys to Azure App Service using Azure credentials stored as GitHub Secrets

No manual intervention is required for deployment once the workflow is configured.

## Troubleshooting

If the application fails to start after deployment:
1. Check that all required App Settings and Connection Strings are configured in Azure
2. Review the App Service logs in the Azure Portal
3. Verify that the App Service name in the workflow matches your Azure resource
