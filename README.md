# E-Commerce-API-V1

A scalable, maintainable, and extensible E-Commerce API built with ASP.NET Core, following SOLID principles, the repository pattern, and clean code practices.

## Table of Contents

- [Features](#features)
- [Architecture](#architecture)
- [Project Structure](#project-structure)
- [Getting Started](#getting-started)
- [Technologies Used](#technologies-used)
- [Contributing](#contributing)
- [License](#license)

## Features

- User registration and authentication
- Product, category, and collection management
- Shopping cart and order processing
- Address and warehouse management
- Soft delete and restore for entities
- Logging and error handling for all major operations

## Architecture

This project is designed with maintainability, testability, and extensibility in mind, using:

- **SOLID Principles:** Each class and module has a clear, single responsibility. Extension is favored over modification, and abstractions are leveraged throughout.
- **Repository Pattern:** Data access is abstracted via generic and specialized repositories.
- **Dependency Injection:** All services and repositories are injected, enhancing testability and modularity.
- **Separation of Concerns:** Clear distinction between data access, business logic, and API layers.

## Project Structure

```
/Repository         # Data access layer, repositories for all entities
/Models             # Entity and DTO classes
/Services           # Business logic and service classes
/Controllers        # ASP.NET Core API controllers
/Context            # DbContext and EF Core setup
/Interfaces         # Repository and service interfaces
/Helpers            # Utility classes (if any)
Startup.cs          # Application entry, DI, configuration
```

## Getting Started

1. **Clone the repository**
   ```bash
   git clone https://github.com/omargamal1121/E-Commerce-API-V1.git
   cd E-Commerce-API-V1
   ```

2. **Set up the database**
   - Configure your connection string in `appsettings.json`.
   - Run EF Core migrations:
     ```bash
     dotnet ef database update
     ```

3. **Run the application**
   ```bash
   dotnet run
   ```
   The API should be running at `https://localhost:5001` or as configured.

## Technologies Used

- ASP.NET Core
- Entity Framework Core
- SQL Server (or your preferred RDBMS)
- Dapper
- StackExchange.Redis (for caching)
- Swagger (for API documentation)
- Microsoft Logging

## Contributing

Contributions are welcome! Please fork the repository and submit a pull request. For major changes, open an issue first to discuss your ideas.

## License

This project is licensed under the MIT License.

---

**Author:** [omargamal1121](https://github.com/omargamal1121)