# MyDigialLibrary

[![Azure](https://img.shields.io/badge/Azure-0089D6?logo=microsoft-azure&logoColor=white)](https://azure.microsoft.com/)
[![Azure Blob Storage](https://img.shields.io/badge/Azure%20Blob%20Storage-3366CC?logo=microsoft-azure&logoColor=white)](https://docs.microsoft.com/en-us/azure/storage/blobs/)
[![Azure SQL](https://img.shields.io/badge/Azure%20SQL-0078D4?logo=microsoft-sql-server&logoColor=white)](https://docs.microsoft.com/en-us/azure/azure-sql/)
[![Azure Queue](https://img.shields.io/badge/Azure%20Queue-01355E?logo=microsoft-azure&logoColor=white)](https://docs.microsoft.com/en-us/azure/storage/queues/storage-queues-introduction)
[![.NET](https://img.shields.io/badge/.NET-512BD4?logo=dotnet&logoColor=white)](https://dotnet.microsoft.com/)
[![C#](https://img.shields.io/badge/C%23-239120?logo=c-sharp&logoColor=white)](https://docs.microsoft.com/en-us/dotnet/csharp/)
[![Bootstrap](https://img.shields.io/badge/Bootstrap-563d7c?logo=bootstrap&logoColor=white)](https://getbootstrap.com/)

A modern, cloud-native replacement for Calibre. Built for the web with Azure PaaS services.

---

## Features

- User authentication with Azure AD or custom provider
- Ebook upload and secure storage (Azure Blob)
- Metadata extraction with queue-based background processing
- Library browsing, search, and filtering
- Responsive, accessible Bootstrap UI
- Book download and bookshelf management
- Admin dashboard and audit logging

## Stack

- Frontend: Bootstrap 5
- Backend: ASP.NET Core (C#)
- Storage: Azure Blob Storage, Azure SQL
- Queue Processing: Azure Queue Storage

## Get Started

```bash
# Clone repo (update with your actual repo URL)
git clone https://github.com/your-org/MyDigialLibrary.git
cd MyDigialLibrary

# See /docs/ for full setup instructions (coming soon!)
```

## Repo Structure

```
/src          # Core app code (backend & frontend)
/docs         # Documentation, setup, feature lists
/.github      # GitHub workflows, issue templates
/tests        # Automated test code
```

## License

[MIT License](LICENSE)

---

_Initial repository structure generated with Copilot for levikirkland_
