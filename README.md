# OracleViewer

**OracleViewer** is a .NET Core Razor Pages application that connects to Oracle databases and displays schema objects such as **tables**, **stored procedures**, **packages**, and **package bodies**. It reads configuration values from `appsettings.json` and allows users to search, browse, and view object definitions with ease.

## 🚀 Features

- 🔗 Connect to Oracle DB using settings from `appsettings.json`
- 📋 View database objects: Tables, Procedures, Packages, Package Bodies
- 🔍 Search and filter by object name or keywords
- 📄 Display full DDL and source code
- 🧭 Clean, paginated Razor Pages UI
- 🧪 Easily extendable for more schema types

## 🛠️ Tech Stack

- .NET 8 (Razor Pages)
- C#
- Oracle.ManagedDataAccess
- Oracle Database

## ⚙️ Setup Instructions

1. **Clone the repository**:
   ```bash
   git clone https://github.com/mohamedibrahim4/OracleViewer.git
   cd OracleViewer
